using TheBlock.Api.Domain;

namespace TheBlock.Api.Tests;

/// <summary>
/// A lot that can be won by bidding in its last second is a reflex test, not
/// an auction.
/// </summary>
public class AntiSnipeTests
{
    private static readonly DateTimeOffset Opens = new(2026, 4, 3, 9, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset Closes => Opens + AuctionRules.LotDuration;

    private static TimeSpan ExtensionAt(DateTimeOffset saleNow, TimeSpan alreadyHeld = default)
    {
        var status = LotLifecycle.Evaluate(Opens, alreadyHeld, null, 20_000m, saleNow);
        return LotLifecycle.ExtensionFor(status, saleNow);
    }

    [Fact]
    public void A_bid_early_in_the_run_does_not_move_the_close()
    {
        Assert.Equal(TimeSpan.Zero, ExtensionAt(Opens + TimeSpan.FromHours(3)));
    }

    [Fact]
    public void A_bid_just_before_the_block_opens_does_not_move_the_close()
    {
        Assert.Equal(TimeSpan.Zero, ExtensionAt(Closes - AuctionRules.BlockWindow - TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void A_bid_in_the_last_seconds_holds_the_lot_open()
    {
        var extension = ExtensionAt(Closes - TimeSpan.FromSeconds(10));

        Assert.Equal(AuctionRules.BlockExtension - TimeSpan.FromSeconds(10), extension);
    }

    // A bid with more than the extension still to run does not need holding
    // open: everyone already has longer than the extension would give them.
    [Fact]
    public void A_bid_with_plenty_of_time_left_on_the_block_does_not_move_the_close()
    {
        Assert.Equal(TimeSpan.Zero, ExtensionAt(Closes - AuctionRules.BlockWindow + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void An_extended_lot_always_has_the_full_extension_left_to_run()
    {
        var bidAt = Closes - TimeSpan.FromSeconds(5);

        var extension = ExtensionAt(bidAt);
        var status = LotLifecycle.Evaluate(Opens, extension, null, 20_000m, bidAt);

        Assert.Equal(AuctionRules.BlockExtension, status.ClosesAt - bidAt);
        Assert.Equal(LotState.Live, status.State);
    }

    [Fact]
    public void Bidding_again_and_again_keeps_holding_the_lot_open()
    {
        var held = TimeSpan.Zero;
        var bidAt = Closes - TimeSpan.FromSeconds(5);

        for (var round = 0; round < 3; round++)
        {
            held += ExtensionAt(bidAt, held);
            bidAt += AuctionRules.BlockExtension - TimeSpan.FromSeconds(5);
        }

        var status = LotLifecycle.Evaluate(Opens, held, null, 20_000m, bidAt);

        Assert.Equal(LotState.Live, status.State);
        Assert.True(held > AuctionRules.BlockExtension * 2, "each late bid should have bought more time");
    }

    [Fact]
    public void A_closed_lot_cannot_be_held_open()
    {
        Assert.Equal(TimeSpan.Zero, ExtensionAt(Closes));
    }
}
