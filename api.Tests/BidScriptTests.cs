using TheBlock.Api.Data;
using TheBlock.Api.Domain;

namespace TheBlock.Api.Tests;

public class BidScriptTests
{
    private static readonly IReadOnlyList<VehicleRecord> Vehicles = VehicleDataset.Load();

    private static readonly DateTimeOffset AnchoredNow = SaleAnchor.ComputeTarget(
        Vehicles.Select(vehicle => VehicleDataset.ParseSaleTime(vehicle.AuctionStart)).ToList());

    private static DateTimeOffset OpensAt(VehicleRecord vehicle) =>
        VehicleDataset.ParseSaleTime(vehicle.AuctionStart);

    private static List<Bid> Script(VehicleRecord vehicle) =>
        BidScript.Generate(vehicle, OpensAt(vehicle), AnchoredNow);

    /// <summary>The lot's price and bid count as a buyer would see them at a given moment.</summary>
    private static (decimal? High, int Count) Visible(VehicleRecord vehicle, DateTimeOffset saleNow)
    {
        var standing = Script(vehicle).Where(bid => bid.PlacedAtSale <= saleNow).ToList();
        return (standing.Count == 0 ? null : standing.Max(bid => bid.Amount), standing.Count);
    }

    // The dataset's bid_count and current_bid describe each lot before it
    // opened -- every one of the 200 was still upcoming when the file was
    // written. The script reproduces that state exactly, which is what turns
    // "88 lots hold bids they cannot have yet" into the pre-bid rule.
    [Fact]
    public void Every_lot_reproduces_its_recorded_pre_bid_state_exactly()
    {
        foreach (var vehicle in Vehicles)
        {
            var preBids = Script(vehicle).Where(bid => bid.PlacedAtSale < OpensAt(vehicle)).ToList();

            Assert.Equal(vehicle.BidCount, preBids.Count);

            if (vehicle.CurrentBid is { } recorded)
            {
                Assert.Equal(recorded, preBids.Max(bid => bid.Amount));
            }
            else
            {
                Assert.Empty(preBids);
            }
        }
    }

    // A lot that has not opened yet must still show the bids the dataset
    // credits it with, so its pre-bidding has to sit behind the anchored now
    // rather than behind its own open.
    [Fact]
    public void A_lot_that_has_not_opened_yet_still_shows_its_recorded_bids()
    {
        var previewLots = Vehicles
            .Where(vehicle => OpensAt(vehicle) > AnchoredNow && vehicle.BidCount > 0)
            .ToList();

        Assert.NotEmpty(previewLots);

        foreach (var vehicle in previewLots)
        {
            var (high, count) = Visible(vehicle, AnchoredNow);

            Assert.Equal(vehicle.BidCount, count);
            Assert.Equal(vehicle.CurrentBid, high);
        }
    }

    [Fact]
    public void Bidding_only_ever_climbs()
    {
        foreach (var vehicle in Vehicles)
        {
            var amounts = Script(vehicle).OrderBy(bid => bid.PlacedAtSale).Select(bid => bid.Amount).ToList();

            Assert.Equal(amounts.Order(), amounts);
            Assert.Equal(amounts.Distinct().Count(), amounts.Count);
        }
    }

    [Fact]
    public void No_bid_ever_undercuts_the_starting_bid()
    {
        foreach (var vehicle in Vehicles)
        {
            Assert.All(Script(vehicle), bid => Assert.True(bid.Amount >= vehicle.StartingBid));
        }
    }

    [Fact]
    public void Every_bid_lands_inside_its_lot_run()
    {
        foreach (var vehicle in Vehicles)
        {
            var closesAt = OpensAt(vehicle) + AuctionRules.LotDuration;

            Assert.All(Script(vehicle), bid => Assert.True(bid.PlacedAtSale < closesAt));
        }
    }

    // Nobody outbids the buy-now price; they would simply buy the lot.
    [Fact]
    public void Bidding_never_reaches_the_buy_now_price()
    {
        foreach (var vehicle in Vehicles.Where(vehicle => vehicle.BuyNowPrice is not null))
        {
            Assert.All(Script(vehicle), bid => Assert.True(bid.Amount < vehicle.BuyNowPrice));
        }
    }

    [Fact]
    public void The_script_tells_the_same_story_every_run()
    {
        var vehicle = Vehicles.First(candidate => candidate.BidCount > 3);

        var first = Script(vehicle).Select(bid => (bid.Amount, bid.PlacedAtSale));
        var second = Script(vehicle).Select(bid => (bid.Amount, bid.PlacedAtSale));

        Assert.Equal(first, second);
    }

    /// <summary>
    /// Taken at face value the dataset is a catastrophe of a sale: current_bid
    /// is drawn from 72-102% of reserve, so only 4 of 200 lots clear it and a
    /// results view would read 11% conversion against roughly 60% in real
    /// wholesale. Bidding across the run is what fixes that, and this is the
    /// test that says so.
    /// </summary>
    [Fact]
    public void The_sale_finishes_with_a_conversion_rate_a_wholesaler_would_recognise()
    {
        var outcomes = Vehicles
            .Select(vehicle =>
            {
                var closesAt = OpensAt(vehicle) + AuctionRules.LotDuration;
                var (high, _) = Visible(vehicle, closesAt);

                return LotLifecycle.Evaluate(OpensAt(vehicle), TimeSpan.Zero, vehicle.ReservePrice, high, closesAt).Outcome;
            })
            .ToList();

        var sold = outcomes.Count(outcome => outcome is LotOutcome.Sold);
        var ifSale = outcomes.Count(outcome => outcome is LotOutcome.IfSale);
        var noSale = outcomes.Count(outcome => outcome is LotOutcome.NoSale);

        Assert.Equal(200, sold + ifSale + noSale);
        Assert.InRange(sold, 100, 140);
        Assert.InRange(noSale, 15, 50);
        Assert.True(ifSale > 0, "reserve-not-met has to stay a real outcome, not be engineered away");
    }
}
