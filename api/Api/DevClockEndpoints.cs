using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TheBlock.Api.Data;
using TheBlock.Api.Domain;
using TheBlock.Api.Realtime;

namespace TheBlock.Api.Api;

public sealed record ClockReport(
    DateTimeOffset SaleNow,
    double ScrubSeconds,
    int Preview,
    int Live,
    int Ended,
    int OnTheBlock,
    double? NextCloseInSeconds);

public sealed record RunLotRequest(string LotId);

/// <summary>
/// Moves the sale clock, for demonstrating it.
/// </summary>
/// <remarks>
/// <para>
/// A sale spread over a week only closes a lot about once an hour, and nobody
/// watching a demo has an hour. This exists so a lot can be taken to its close
/// on demand and actually watched: it goes on the block, the seeded bidding
/// lands, a late bid holds it open, and it resolves.
/// </para>
/// <para>
/// Development only, and gated on the server rather than in the client -- a
/// hidden button is not a permission check.
/// </para>
/// </remarks>
public static class DevClockEndpoints
{
    /// <summary>Close enough to watch it happen, far enough to say something first.</summary>
    private static readonly TimeSpan RunLotLead = TimeSpan.FromSeconds(90);

    public static void MapDevClockEndpoints(this IEndpointRouteBuilder routes, IWebHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        var clock = routes.MapGroup("/api/dev/clock");

        clock.MapGet("/", Report);
        clock.MapPost("/run-lot", RunLot);
        clock.MapPost("/reset", Reset);
    }

    private static async Task<IResult> Report(AuctionDbContext database, SaleClock clock, CancellationToken cancellationToken) =>
        Results.Ok(new SaleEnvelope<ClockReport>(clock.RealNow, await Snapshot(database, clock, cancellationToken)));

    /// <summary>
    /// Winds the sale to just before a chosen lot closes.
    /// </summary>
    private static async Task<IResult> RunLot(
        RunLotRequest request,
        AuctionDbContext database,
        SaleClock clock,
        SaleNotifier notifier,
        IHubContext<AuctionHub> hub,
        CancellationToken cancellationToken)
    {
        var lot = await database.Lots
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.LotId, cancellationToken);

        if (lot is null)
        {
            return Results.NotFound();
        }

        var target = LotLifecycle.ClosesAt(lot.OpensAt, lot.Extension) - RunLotLead;

        // Sale time runs behind real time by the offset, so moving it forward
        // means taking the scrub down.
        clock.SetScrub(clock.Scrub - (target - clock.SaleNow));

        await Resync(database, clock, notifier, hub, cancellationToken);

        return Results.Ok(new SaleEnvelope<ClockReport>(clock.RealNow, await Snapshot(database, clock, cancellationToken)));
    }

    private static async Task<IResult> Reset(
        AuctionDbContext database,
        SaleClock clock,
        SaleNotifier notifier,
        IHubContext<AuctionHub> hub,
        CancellationToken cancellationToken)
    {
        clock.SetScrub(TimeSpan.Zero);
        await Resync(database, clock, notifier, hub, cancellationToken);

        return Results.Ok(new SaleEnvelope<ClockReport>(clock.RealNow, await Snapshot(database, clock, cancellationToken)));
    }

    /// <summary>
    /// Moving the clock moves the whole sale at once. Telling clients a few
    /// hundred lots each changed would be noise, so they are asked to look
    /// again instead, and the notifier forgets what it knew rather than
    /// reporting the difference bid by bid.
    /// </summary>
    private static async Task Resync(
        AuctionDbContext database,
        SaleClock clock,
        SaleNotifier notifier,
        IHubContext<AuctionHub> hub,
        CancellationToken cancellationToken)
    {
        notifier.Forget();

        var envelope = new SaleEnvelope<ClockReport>(clock.RealNow, await Snapshot(database, clock, cancellationToken));
        await hub.Clients.All.SendAsync(AuctionEvents.ClockChanged, envelope, cancellationToken);
    }

    private static async Task<ClockReport> Snapshot(
        AuctionDbContext database, SaleClock clock, CancellationToken cancellationToken)
    {
        var saleNow = clock.SaleNow;
        var lots = await database.Lots
            .AsNoTracking()
            .Select(lot => new { lot.OpensAt, lot.Extension })
            .ToListAsync(cancellationToken);

        var statuses = lots
            .Select(lot => LotLifecycle.Evaluate(lot.OpensAt, lot.Extension, null, null, saleNow))
            .ToList();

        var nextClose = statuses
            .Where(status => status.ClosesAt > saleNow)
            .Select(status => status.ClosesAt)
            .DefaultIfEmpty()
            .Min();

        return new ClockReport(
            SaleNow: clock.ToRealTime(saleNow),
            ScrubSeconds: clock.Scrub.TotalSeconds,
            Preview: statuses.Count(status => status.State is LotState.Preview),
            Live: statuses.Count(status => status.State is LotState.Live),
            Ended: statuses.Count(status => status.State is LotState.Ended),
            OnTheBlock: statuses.Count(status => status.OnTheBlock),
            NextCloseInSeconds: nextClose == default ? null : (nextClose - saleNow).TotalSeconds);
    }
}
