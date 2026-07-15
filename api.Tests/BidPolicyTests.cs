using TheBlock.Api.Domain;

namespace TheBlock.Api.Tests;

public class BidPolicyTests
{
    private static readonly DateTimeOffset Opens = new(2026, 4, 3, 9, 0, 0, TimeSpan.Zero);

    private static LotStatus StatusAt(DateTimeOffset saleNow) =>
        LotLifecycle.Evaluate(Opens, TimeSpan.Zero, reservePrice: null, highBid: null, saleNow);

    private static LotStatus Live => StatusAt(Opens + TimeSpan.FromHours(1));
    private static LotStatus Preview => StatusAt(Opens - TimeSpan.FromHours(1));
    private static LotStatus Ended => StatusAt(Opens + AuctionRules.LotDuration);

    [Theory]
    [InlineData(0, 100)]
    [InlineData(9_999, 100)]
    [InlineData(10_000, 250)]
    [InlineData(24_999, 250)]
    [InlineData(25_000, 500)]
    [InlineData(77_000, 500)]
    public void The_increment_widens_as_the_money_does(int price, int expected)
    {
        Assert.Equal(expected, BidPolicy.IncrementAt(price));
    }

    // Over half the dataset has no bids at all. Those lots are not free, and
    // the starting bid is a valid first bid rather than something to beat.
    [Fact]
    public void A_lot_with_no_bids_opens_at_its_starting_bid()
    {
        Assert.Equal(14_500m, BidPolicy.MinimumNextBid(startingBid: 14_500m, highBid: null));
    }

    [Theory]
    [InlineData(9_900, 10_000)]
    [InlineData(10_000, 10_250)]
    [InlineData(22_800, 23_050)]
    [InlineData(25_000, 25_500)]
    public void A_lot_with_bids_climbs_by_one_increment(int highBid, int expected)
    {
        Assert.Equal(expected, BidPolicy.MinimumNextBid(startingBid: 2_500m, highBid: highBid));
    }

    [Fact]
    public void The_starting_bid_is_accepted_on_a_lot_with_no_bids()
    {
        var verdict = BidPolicy.Validate(Live, startingBid: 14_500m, highBid: null, amount: 14_500m);

        Assert.True(verdict.Accepted);
    }

    [Fact]
    public void Bidding_under_the_starting_bid_is_refused()
    {
        var verdict = BidPolicy.Validate(Live, startingBid: 14_500m, highBid: null, amount: 14_499m);

        Assert.False(verdict.Accepted);
        Assert.Equal(BidRejection.BelowMinimum, verdict.Rejection);
        Assert.Equal(14_500m, verdict.MinimumNextBid);
    }

    // The point of running this on the server: nudging a dollar over the
    // current bid does not buy you the lot.
    [Fact]
    public void Bidding_inside_the_increment_is_refused()
    {
        var verdict = BidPolicy.Validate(Live, startingBid: 2_500m, highBid: 22_800m, amount: 22_801m);

        Assert.False(verdict.Accepted);
        Assert.Equal(BidRejection.BelowMinimum, verdict.Rejection);
        Assert.Equal(23_050m, verdict.MinimumNextBid);
    }

    [Fact]
    public void Bidding_exactly_one_increment_up_is_accepted()
    {
        var verdict = BidPolicy.Validate(Live, startingBid: 2_500m, highBid: 22_800m, amount: 23_050m);

        Assert.True(verdict.Accepted);
    }

    [Fact]
    public void Bidding_well_over_the_minimum_is_accepted()
    {
        var verdict = BidPolicy.Validate(Live, startingBid: 2_500m, highBid: 22_800m, amount: 30_000m);

        Assert.True(verdict.Accepted);
    }

    [Fact]
    public void Pre_bidding_on_a_lot_that_has_not_opened_is_accepted()
    {
        var verdict = BidPolicy.Validate(Preview, startingBid: 14_500m, highBid: null, amount: 14_500m);

        Assert.True(verdict.Accepted);
    }

    [Fact]
    public void Bidding_on_a_closed_lot_is_refused()
    {
        var verdict = BidPolicy.Validate(Ended, startingBid: 14_500m, highBid: null, amount: 20_000m);

        Assert.False(verdict.Accepted);
        Assert.Equal(BidRejection.LotClosed, verdict.Rejection);
    }

    [Fact]
    public void A_refused_bid_reports_the_amount_that_would_have_worked()
    {
        var verdict = BidPolicy.Validate(Live, startingBid: 2_500m, highBid: 9_950m, amount: 9_951m);

        Assert.False(verdict.Accepted);
        Assert.Equal(10_050m, verdict.MinimumNextBid);
    }
}
