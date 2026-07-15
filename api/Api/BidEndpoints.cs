using Microsoft.EntityFrameworkCore;
using TheBlock.Api.Data;
using TheBlock.Api.Domain;

namespace TheBlock.Api.Api;

public sealed record PlaceBidRequest(decimal Amount);

public sealed record BidRefused(string Reason, decimal MinimumNextBid, decimal? CurrentBid);

public static class BidEndpoints
{
    /// <summary>
    /// Identifies the buyer. There are no accounts here, so the browser makes
    /// one up and keeps it. Enough to tell a buyer which bids are theirs.
    /// </summary>
    public const string BuyerHeader = "X-Buyer-Id";

    public static void MapBidEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/lots/{id}/bids", PlaceBid);
    }

    /// <summary>
    /// Takes a bid on a lot, or explains why not.
    /// </summary>
    /// <remarks>
    /// The client proposes an amount; it does not get to decide whether that
    /// amount is a bid. Everything that makes this an auction rather than a
    /// number field -- the increment, whether the lot is still open, who is
    /// already winning -- is settled here.
    /// </remarks>
    private static async Task<IResult> PlaceBid(
        string id,
        PlaceBidRequest request,
        HttpContext http,
        AuctionDbContext database,
        SaleClock clock,
        CancellationToken cancellationToken)
    {
        if (!http.Request.Headers.TryGetValue(BuyerHeader, out var header) || string.IsNullOrWhiteSpace(header))
        {
            return Results.BadRequest(new BidRefused("unidentified", 0m, null));
        }

        var buyerId = header.ToString();
        var lot = await database.Lots.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (lot is null)
        {
            return Results.NotFound();
        }

        var saleNow = clock.SaleNow;
        var leading = await database.Bids
            .Where(bid => bid.LotId == id && bid.PlacedAtSale <= saleNow)
            .OrderByDescending(bid => bid.Amount)
            .FirstOrDefaultAsync(cancellationToken);

        var status = LotLifecycle.Evaluate(lot.OpensAt, lot.Extension, lot.ReservePrice, leading?.Amount, saleNow);
        var verdict = BidPolicy.Validate(status, lot.StartingBid, leading?.Amount, request.Amount);

        if (!verdict.Accepted)
        {
            var reason = verdict.Rejection is BidRejection.LotClosed ? "lotClosed" : "belowMinimum";
            return Results.BadRequest(new BidRefused(reason, verdict.MinimumNextBid, leading?.Amount));
        }

        // Bidding against yourself only moves your own money.
        if (leading?.BidderId == buyerId)
        {
            return Results.BadRequest(new BidRefused("alreadyLeading", verdict.MinimumNextBid, leading.Amount));
        }

        database.Bids.Add(new Bid
        {
            LotId = id,
            Amount = request.Amount,
            BidderId = buyerId,

            // Stored on the sale timeline, so a re-anchor carries this bid
            // along with the lot it belongs to. It still renders back at the
            // moment the buyer clicked.
            PlacedAtSale = clock.ToSaleTime(clock.RealNow),
            CreatedAtRealUtc = clock.RealNow,
            IsSeeded = false,
        });

        // Touching the lot is what serialises bidding on it. Two buyers who
        // read the same high bid cannot both win: the second UPDATE matches no
        // row, and EF raises rather than quietly accepting a bid that was
        // valid a moment ago and is not any more. The insert rolls back with
        // it, since SaveChanges is one transaction.
        lot.Version++;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            var fresh = await Standing(database, clock, id, cancellationToken);

            return Results.Conflict(new BidRefused(
                "outbid",
                BidPolicy.MinimumNextBid(lot.StartingBid, fresh?.HighBid),
                fresh?.HighBid));
        }

        var updated = await Standing(database, clock, id, cancellationToken);

        return Results.Ok(new SaleEnvelope<LotSummary>(clock.RealNow, LotMapper.ToSummary(updated!, clock)));
    }

    private static async Task<LotView?> Standing(
        AuctionDbContext database, SaleClock clock, string id, CancellationToken cancellationToken)
    {
        var views = await LotEndpoints.StandingBidsAsync(database, clock.SaleNow, id, cancellationToken);
        return views.FirstOrDefault();
    }
}
