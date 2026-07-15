using TheBlock.Api.Domain;

namespace TheBlock.Api.Tests;

public class LotLifecycleTests
{
    private static readonly DateTimeOffset Opens = new(2026, 4, 3, 9, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset Closes => Opens + AuctionRules.LotDuration;

    private static LotStatus At(
        DateTimeOffset saleNow,
        decimal? reservePrice = null,
        decimal? highBid = null,
        TimeSpan extension = default) =>
        LotLifecycle.Evaluate(Opens, extension, reservePrice, highBid, saleNow);

    [Fact]
    public void A_lot_is_in_preview_before_it_opens()
    {
        Assert.Equal(LotState.Preview, At(Opens - TimeSpan.FromSeconds(1)).State);
    }

    [Fact]
    public void A_lot_opens_exactly_at_its_open_time()
    {
        Assert.Equal(LotState.Live, At(Opens).State);
    }

    [Fact]
    public void A_lot_is_live_up_to_the_instant_before_it_closes()
    {
        Assert.Equal(LotState.Live, At(Closes - TimeSpan.FromSeconds(1)).State);
    }

    [Fact]
    public void A_lot_ends_exactly_at_its_close_time()
    {
        Assert.Equal(LotState.Ended, At(Closes).State);
    }

    // Pre-bidding is what reconciles the dataset with itself: 88 lots ship
    // with bids already on them despite opening in the future.
    [Fact]
    public void Pre_bids_are_accepted_before_a_lot_opens()
    {
        Assert.True(At(Opens - TimeSpan.FromHours(6)).AcceptsBids);
    }

    [Fact]
    public void Bids_are_accepted_while_a_lot_is_live()
    {
        Assert.True(At(Opens + TimeSpan.FromHours(6)).AcceptsBids);
    }

    [Fact]
    public void Bids_are_refused_once_a_lot_has_ended()
    {
        Assert.False(At(Closes).AcceptsBids);
    }

    [Fact]
    public void A_lot_is_on_the_block_for_the_closing_stretch_of_its_run()
    {
        Assert.True(At(Closes - AuctionRules.BlockWindow).OnTheBlock);
        Assert.True(At(Closes - TimeSpan.FromSeconds(1)).OnTheBlock);
    }

    [Fact]
    public void A_lot_is_not_on_the_block_before_the_closing_stretch()
    {
        Assert.False(At(Closes - AuctionRules.BlockWindow - TimeSpan.FromSeconds(1)).OnTheBlock);
    }

    [Fact]
    public void An_ended_lot_is_not_on_the_block()
    {
        Assert.False(At(Closes).OnTheBlock);
    }

    [Fact]
    public void A_lot_in_preview_is_not_on_the_block()
    {
        Assert.False(At(Opens - TimeSpan.FromSeconds(1)).OnTheBlock);
    }

    [Fact]
    public void An_extension_holds_a_lot_open_past_its_scheduled_close()
    {
        var extension = TimeSpan.FromMinutes(2);

        var status = At(Closes, extension: extension);

        Assert.Equal(LotState.Live, status.State);
        Assert.Equal(Closes + extension, status.ClosesAt);
        Assert.True(status.OnTheBlock);
    }

    [Theory]
    [InlineData(null, null, ReserveStatus.None)]
    [InlineData(null, 5000, ReserveStatus.None)]
    [InlineData(20000, null, ReserveStatus.NotMet)]
    [InlineData(20000, 19999, ReserveStatus.NotMet)]
    [InlineData(20000, 20000, ReserveStatus.Met)]
    [InlineData(20000, 20001, ReserveStatus.Met)]
    public void Reserve_status_compares_the_high_bid_against_the_reserve(
        int? reserve, int? highBid, ReserveStatus expected)
    {
        var status = At(Opens, reserve, highBid);

        Assert.Equal(expected, status.Reserve);
    }

    [Fact]
    public void A_running_lot_has_no_outcome_yet()
    {
        Assert.Equal(LotOutcome.Pending, At(Opens, 20000, 25000).Outcome);
        Assert.Equal(LotOutcome.Pending, At(Opens - TimeSpan.FromHours(1)).Outcome);
    }

    [Fact]
    public void A_lot_that_drew_no_bids_is_a_no_sale()
    {
        Assert.Equal(LotOutcome.NoSale, At(Closes, reservePrice: 20000).Outcome);
        Assert.Equal(LotOutcome.NoSale, At(Closes, reservePrice: null).Outcome);
    }

    // 60 of the 200 lots ship without a reserve, which is a selling point
    // rather than an omission: they sell to the highest bidder.
    [Fact]
    public void A_lot_with_no_reserve_sells_to_whoever_bid_highest()
    {
        Assert.Equal(LotOutcome.Sold, At(Closes, reservePrice: null, highBid: 4000).Outcome);
    }

    [Fact]
    public void A_lot_that_cleared_its_reserve_is_sold()
    {
        Assert.Equal(LotOutcome.Sold, At(Closes, reservePrice: 20000, highBid: 20000).Outcome);
    }

    // The dominant ended state, by construction: the dataset draws current_bid
    // from 72-102% of reserve, so most lots close short of it. Wholesale calls
    // this an "IF" -- the high bid sits with the seller for approval -- which
    // is an outcome, not a failure.
    [Fact]
    public void A_lot_that_closed_short_of_its_reserve_goes_to_the_seller()
    {
        Assert.Equal(LotOutcome.IfSale, At(Closes, reservePrice: 20000, highBid: 19500).Outcome);
    }
}
