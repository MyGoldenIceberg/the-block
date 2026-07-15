using TheBlock.Api.Domain;

namespace TheBlock.Api.Tests;

public class SaleAnchorTests
{
    private static readonly DateTimeOffset Day0 = new(2026, 3, 31, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A stand-in with the dataset's shape: lots open only during sale hours,
    /// across a week, leaving a long overnight gap every night.
    /// </summary>
    private static List<DateTimeOffset> SaleWeek() =>
        Enumerable.Range(0, 7)
            .SelectMany(day => new[] { 9, 20 }.Select(hour => Day0.AddDays(day).AddHours(hour)))
            .ToList();

    [Fact]
    public void Anchoring_a_sale_with_no_lots_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => SaleAnchor.ComputeTarget([]));
    }

    [Fact]
    public void Target_is_the_lead_time_before_a_close()
    {
        var opens = SaleWeek();

        var target = SaleAnchor.ComputeTarget(opens);

        var closes = opens.Select(open => open + AuctionRules.LotDuration);
        Assert.Contains(target + AuctionRules.AnchorLead, closes);
    }

    // Snapping *forward* is what keeps the sale moving. Lots only open during
    // sale hours, so most of the timeline is an overnight dead zone and the
    // nominal target usually lands inside one. Snapping to the nearest close
    // would step backward into that gap, leaving nothing to happen until
    // morning.
    [Fact]
    public void Target_snaps_forward_past_an_overnight_gap_rather_than_back_into_it()
    {
        var opens = SaleWeek();
        var windowStart = opens.Min();
        var saleSpan = (opens.Max() - windowStart) + AuctionRules.LotDuration;
        var nominal = windowStart + (saleSpan * AuctionRules.AnchorFraction);

        var snappedClose = SaleAnchor.ComputeTarget(opens) + AuctionRules.AnchorLead;

        // The nominal target lands in the overnight gap, so this is a real test.
        Assert.Equal(20, nominal.Hour);
        Assert.True(snappedClose >= nominal, "anchor must not snap backward into the dead zone");
        Assert.Equal(9, snappedClose.Hour);
    }

    [Fact]
    public void Anchored_sale_time_lands_on_the_target()
    {
        var opens = SaleWeek();
        var realNow = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        var offset = SaleAnchor.ComputeOffset(opens, realNow);

        Assert.Equal(SaleAnchor.ComputeTarget(opens), realNow - offset);
    }

    [Fact]
    public void The_first_lot_hammers_shortly_after_the_anchor_is_set()
    {
        var opens = SaleWeek();
        var target = SaleAnchor.ComputeTarget(opens);

        var nextClose = opens
            .Select(open => open + AuctionRules.LotDuration)
            .Where(close => close >= target)
            .Min();

        Assert.Equal(AuctionRules.AnchorLead, nextClose - target);
    }

    [Fact]
    public void A_freshly_anchored_sale_has_lots_in_every_state()
    {
        var opens = SaleWeek();
        var saleNow = SaleAnchor.ComputeTarget(opens);

        Assert.Contains(opens, open => saleNow < open);
        Assert.Contains(opens, open => saleNow >= open && saleNow < open + AuctionRules.LotDuration);
        Assert.Contains(opens, open => saleNow >= open + AuctionRules.LotDuration);
    }

    [Fact]
    public void A_freshly_anchored_sale_is_not_stale()
    {
        var opens = SaleWeek();

        Assert.False(SaleAnchor.IsStale(opens, SaleAnchor.ComputeTarget(opens)));
    }

    [Fact]
    public void A_sale_that_has_run_past_its_end_is_stale()
    {
        var opens = SaleWeek();

        Assert.True(SaleAnchor.IsStale(opens, opens.Max() + AuctionRules.LotDuration));
    }

    [Fact]
    public void A_sale_with_no_lots_is_never_stale()
    {
        Assert.False(SaleAnchor.IsStale([], DateTimeOffset.UnixEpoch));
    }
}
