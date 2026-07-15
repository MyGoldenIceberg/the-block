using TheBlock.Api.Domain;

namespace TheBlock.Api.Tests;

public class SaleClockTests
{
    private static readonly DateTimeOffset RealStart = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SaleStart = new(2026, 4, 3, 8, 55, 0, TimeSpan.Zero);

    private static (SaleClock Clock, FakeClock Wall) Anchored()
    {
        var wall = new FakeClock(RealStart);
        var clock = new SaleClock(wall);
        clock.SetAnchor(RealStart - SaleStart);
        return (clock, wall);
    }

    [Fact]
    public void Sale_time_starts_at_the_anchored_instant()
    {
        var (clock, _) = Anchored();

        Assert.Equal(SaleStart, clock.SaleNow);
    }

    // The whole design rests on this. If the offset were derived from "now" on
    // each read rather than stored, sale time would stand still and every lot
    // would sit at a constant distance from closing forever.
    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(60 * 24)]
    public void Sale_time_advances_in_lockstep_with_real_time(int minutes)
    {
        var (clock, wall) = Anchored();
        var elapsed = TimeSpan.FromMinutes(minutes);

        wall.Advance(elapsed);

        Assert.Equal(SaleStart + elapsed, clock.SaleNow);
    }

    [Fact]
    public void Scrubbing_moves_sale_time_without_disturbing_the_anchor()
    {
        var (clock, _) = Anchored();
        var anchor = clock.Anchor;

        clock.SetScrub(TimeSpan.FromHours(-3));

        Assert.Equal(SaleStart + TimeSpan.FromHours(3), clock.SaleNow);
        Assert.Equal(anchor, clock.Anchor);
    }

    [Fact]
    public void Resetting_the_scrub_restores_the_anchored_timeline()
    {
        var (clock, _) = Anchored();

        clock.SetScrub(TimeSpan.FromHours(-9));
        clock.SetScrub(TimeSpan.Zero);

        Assert.Equal(SaleStart, clock.SaleNow);
    }

    [Fact]
    public void Sale_and_real_time_round_trip()
    {
        var (clock, _) = Anchored();

        var round = clock.ToRealTime(clock.ToSaleTime(RealStart));

        Assert.Equal(RealStart, round);
    }

    // A bid is stored in sale time so a re-anchor carries it along with its
    // lot, but the buyer must still see it as having happened when they
    // clicked. Both properties have to hold at once.
    [Fact]
    public void A_bid_stored_in_sale_time_renders_back_at_the_moment_it_was_placed()
    {
        var (clock, wall) = Anchored();
        wall.Advance(TimeSpan.FromMinutes(90));
        var clickedAt = clock.RealNow;

        var stored = clock.ToSaleTime(clickedAt);

        Assert.Equal(clickedAt, clock.ToRealTime(stored));
        Assert.Equal(clock.SaleNow, stored);
    }

    // The payoff for storing bids in sale time: re-anchoring a stale database
    // drags lots and their bids along together, so a bid placed five minutes
    // before a close is still five minutes before that close afterwards.
    [Fact]
    public void Re_anchoring_moves_a_lot_and_its_bids_together()
    {
        var (clock, _) = Anchored();
        var lotClosesAtSale = clock.SaleNow + TimeSpan.FromMinutes(5);
        var bidAtSale = clock.ToSaleTime(clock.RealNow);

        var closeBefore = clock.ToRealTime(lotClosesAtSale);
        var bidBefore = clock.ToRealTime(bidAtSale);

        // Thirty days on, a restart re-anchors the stale sale back into the
        // present, which widens the offset by exactly that much.
        clock.SetAnchor(clock.Anchor + TimeSpan.FromDays(30));

        var closeAfter = clock.ToRealTime(lotClosesAtSale);
        var bidAfter = clock.ToRealTime(bidAtSale);

        Assert.Equal(TimeSpan.FromDays(30), closeAfter - closeBefore);
        Assert.Equal(closeAfter - closeBefore, bidAfter - bidBefore);
        Assert.Equal(TimeSpan.FromMinutes(5), closeAfter - bidAfter);
    }
}
