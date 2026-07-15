using TheBlock.Api.Data;
using TheBlock.Api.Domain;

namespace TheBlock.Api.Tests;

/// <summary>
/// The anchor asserted against the real 200-lot dataset rather than a fixture.
/// </summary>
/// <remarks>
/// The README tells a reviewer what the sale looks like when they first open
/// it. These tests are what stop that claim from quietly going stale: change
/// the anchor fraction or the lot duration and they fail, which is the
/// reminder that the prose needs changing too.
/// </remarks>
public class SaleAnchorDatasetTests
{
    private static readonly IReadOnlyList<VehicleRecord> Vehicles = VehicleDataset.Load();

    private static List<DateTimeOffset> Opens() =>
        Vehicles.Select(vehicle => VehicleDataset.ParseSaleTime(vehicle.AuctionStart)).ToList();

    private static (int Preview, int Live, int Ended, int OnTheBlock) Composition(DateTimeOffset saleNow)
    {
        var statuses = Opens()
            .Select(open => LotLifecycle.Evaluate(open, TimeSpan.Zero, null, null, saleNow))
            .ToList();

        return (
            statuses.Count(status => status.State is LotState.Preview),
            statuses.Count(status => status.State is LotState.Live),
            statuses.Count(status => status.State is LotState.Ended),
            statuses.Count(status => status.OnTheBlock));
    }

    [Fact]
    public void The_dataset_is_the_two_hundred_lots_the_challenge_shipped()
    {
        Assert.Equal(200, Vehicles.Count);
    }

    [Fact]
    public void Every_lot_parses_onto_the_sale_timeline_as_utc()
    {
        Assert.All(Opens(), open => Assert.Equal(TimeSpan.Zero, open.Offset));
    }

    // Over half the dataset has no bids, and the value is null rather than
    // zero. Typing this as a plain number is the trap the README's own sample
    // vehicle sets, and it would render $0 across the inventory.
    [Fact]
    public void More_than_half_the_dataset_has_no_bids_at_all()
    {
        Assert.Equal(112, Vehicles.Count(vehicle => vehicle.CurrentBid is null));
        Assert.All(
            Vehicles.Where(vehicle => vehicle.CurrentBid is null),
            vehicle => Assert.Equal(0, vehicle.BidCount));
    }

    // The composition the README describes. If these numbers move, the prose
    // is wrong.
    [Fact]
    public void A_freshly_anchored_sale_opens_with_the_composition_the_readme_claims()
    {
        var (preview, live, ended, onTheBlock) = Composition(SaleAnchor.ComputeTarget(Opens()));

        Assert.Equal(118, preview);
        Assert.Equal(26, live);
        Assert.Equal(56, ended);
        Assert.Equal(1, onTheBlock);
    }

    // Why the anchor snaps forward. The nominal target lands at 20:40, inside
    // one of the overnight gaps that make up half the timeline; snapping to
    // the nearest close would step back to 20:00 and freeze the sale until
    // morning. Snapping forward lands at 09:00 and the sale runs all day.
    [Fact]
    public void The_sale_keeps_moving_through_its_first_twelve_hours()
    {
        var target = SaleAnchor.ComputeTarget(Opens());
        var window = Opens()
            .Select(open => open + AuctionRules.LotDuration)
            .Where(close => close >= target && close < target + TimeSpan.FromHours(12))
            .ToList();

        Assert.Equal(26, window.Count);
        Assert.Equal(12, window.Distinct().Count());
    }

    // Lots only open between 09:00 and 20:00, so a whole-day run is the only
    // duration that closes them inside the same band. A 12h or 36h run would
    // hammer lots in the middle of the night.
    [Fact]
    public void Every_lot_closes_inside_the_hours_the_dataset_opens_them()
    {
        var openHours = Opens().Select(open => open.Hour).ToHashSet();
        var closeHours = Opens().Select(open => (open + AuctionRules.LotDuration).Hour).ToHashSet();

        Assert.Equal(9, openHours.Min());
        Assert.Equal(20, openHours.Max());
        Assert.Equal(openHours, closeHours);
    }

    [Fact]
    public void The_shipped_sale_is_long_stale_and_would_be_a_graveyard_unanchored()
    {
        var opens = Opens();

        Assert.All(opens, open => Assert.True(open < DateTimeOffset.UtcNow));
        Assert.True(SaleAnchor.IsStale(opens, opens.Max() + AuctionRules.LotDuration));
    }
}
