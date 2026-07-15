using Microsoft.AspNetCore.SignalR;
using TheBlock.Api.Api;
using TheBlock.Api.Data;
using TheBlock.Api.Domain;

namespace TheBlock.Api.Realtime;

/// <summary>
/// Watches the sale and tells buyers what changed.
/// </summary>
/// <remarks>
/// <para>
/// Purely a messenger. A lot's state and price are pure functions of the sale
/// clock, computed whenever they are read, so this service decides nothing --
/// it notices. If it stops, the app is still correct; buyers just have to
/// refresh to see it.
/// </para>
/// <para>
/// It polls every second. 200 lots against one grouped aggregate is trivial,
/// and sleeping until the next known event instead would have to be woken by
/// every bid, every extension and every scrub -- more machinery than a
/// one-second tick is worth here.
/// </para>
/// </remarks>
public sealed class SaleNotifier(
    IServiceScopeFactory scopes,
    SaleClock clock,
    IHubContext<AuctionHub> hub,
    ILogger<SaleNotifier> logger) : BackgroundService
{
    private readonly Dictionary<string, LotPulse> _seen = [];

    /// <summary>The part of a lot worth telling anyone about.</summary>
    private readonly record struct LotPulse(
        decimal? CurrentBid, int BidCount, LotState State, bool OnTheBlock, LotOutcome Outcome);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PulseAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                // A failed pass costs one second of notifications, not the sale.
                logger.LogError(exception, "Could not sweep the sale for changes.");
            }
        }
    }

    private async Task PulseAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();

        var views = await LotEndpoints.StandingBidsAsync(database, clock.SaleNow, cancellationToken: cancellationToken);
        var priming = _seen.Count == 0;

        foreach (var view in views)
        {
            var summary = LotMapper.ToSummary(view, clock);
            var pulse = new LotPulse(
                summary.CurrentBid, summary.BidCount, summary.State, summary.OnTheBlock, summary.Outcome);

            var known = _seen.TryGetValue(summary.Id, out var before);
            if (known && before == pulse)
            {
                continue;
            }

            _seen[summary.Id] = pulse;

            // The first sweep is how the notifier learns the sale. Announcing
            // all 200 lots as news would be a lie.
            if (priming || !known)
            {
                continue;
            }

            var movedOn = before.State != pulse.State || before.OnTheBlock != pulse.OnTheBlock;
            var envelope = new SaleEnvelope<LotSummary>(clock.RealNow, summary);
            var message = movedOn ? AuctionEvents.LotStateChanged : AuctionEvents.LotChanged;

            await hub.Clients.All.SendAsync(message, envelope, cancellationToken);
        }
    }

    /// <summary>
    /// Forgets the sale, so the next sweep re-learns it rather than reporting
    /// a scrubbed clock as hundreds of individual bids.
    /// </summary>
    public void Forget() => _seen.Clear();
}
