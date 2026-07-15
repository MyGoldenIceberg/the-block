using Microsoft.EntityFrameworkCore;
using TheBlock.Api.Data;
using TheBlock.Api.Domain;

namespace TheBlock.Api.Api;

public static class LotEndpoints
{
    public static void MapLotEndpoints(this IEndpointRouteBuilder routes)
    {
        var lots = routes.MapGroup("/api/lots");

        lots.MapGet("/", GetLots);
        lots.MapGet("/{id}", GetLot);
    }

    /// <summary>
    /// The whole catalogue.
    /// </summary>
    /// <remarks>
    /// All 200 lots in one response, with the client filtering and sorting
    /// locally. At this size that is both faster and simpler than round-
    /// tripping every facet change, and browsing is the one thing here that
    /// genuinely does not need a server. At 200,000 lots this would move
    /// behind keyset pagination; bidding is what the server is for.
    /// </remarks>
    private static async Task<IResult> GetLots(AuctionDbContext database, SaleClock clock, CancellationToken cancellationToken)
    {
        var views = await StandingBidsAsync(database, clock.SaleNow, cancellationToken: cancellationToken);
        var summaries = views.Select(view => LotMapper.ToSummary(view, clock)).ToList();

        return Results.Ok(new SaleEnvelope<IReadOnlyList<LotSummary>>(clock.RealNow, summaries));
    }

    private static async Task<IResult> GetLot(string id, AuctionDbContext database, SaleClock clock, CancellationToken cancellationToken)
    {
        var views = await StandingBidsAsync(database, clock.SaleNow, id, cancellationToken);
        var view = views.FirstOrDefault();

        return view is null
            ? Results.NotFound()
            : Results.Ok(new SaleEnvelope<LotDetail>(clock.RealNow, LotMapper.ToDetail(view, clock)));
    }

    /// <summary>
    /// Each lot with the bidding that stands against it at <paramref name="saleNow"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A lot's price is not a column. It is the highest bid placed at or
    /// before the current sale time, which is what lets the seeded script
    /// reveal bidding as the clock reaches it, and what lets the clock be
    /// wound backward without leaving a stale total behind.
    /// </para>
    /// <para>
    /// Two queries rather than a correlated subquery per lot: aggregate the
    /// standing bids once, then match them to their lots. It is the difference
    /// between one grouped scan and 200 subqueries.
    /// </para>
    /// </remarks>
    public static async Task<IReadOnlyList<LotView>> StandingBidsAsync(
        AuctionDbContext database,
        DateTimeOffset saleNow,
        string? lotId = null,
        CancellationToken cancellationToken = default)
    {
        var lots = database.Lots.AsNoTracking();
        var bids = database.Bids.Where(bid => bid.PlacedAtSale <= saleNow);

        if (lotId is not null)
        {
            lots = lots.Where(lot => lot.Id == lotId);
            bids = bids.Where(bid => bid.LotId == lotId);
        }

        var standing = await bids
            .GroupBy(bid => bid.LotId)
            .Select(group => new
            {
                LotId = group.Key,
                High = group.Max(bid => bid.Amount),
                Count = group.Count(),
            })
            .ToDictionaryAsync(row => row.LotId, cancellationToken);

        var catalogue = await lots.OrderBy(lot => lot.OpensAt).ToListAsync(cancellationToken);

        return catalogue
            .Select(lot => standing.TryGetValue(lot.Id, out var row)
                ? new LotView(lot, row.High, row.Count)
                : new LotView(lot, null, 0))
            .ToList();
    }
}
