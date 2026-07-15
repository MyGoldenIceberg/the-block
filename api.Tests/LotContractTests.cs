using System.Text.Json;
using System.Text.Json.Serialization;
using TheBlock.Api.Api;
using TheBlock.Api.Data;
using TheBlock.Api.Domain;

namespace TheBlock.Api.Tests;

/// <summary>
/// What a buyer is allowed to be told.
/// </summary>
public class LotContractTests
{
    private const decimal Reserve = 31_337m;

    private static readonly DateTimeOffset RealNow = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Opens = SampleLot.Opens;

    private static readonly JsonSerializerOptions Wire = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static SaleClock Clock()
    {
        var clock = new SaleClock(new FakeClock(RealNow));
        clock.SetAnchor(RealNow - (Opens + TimeSpan.FromHours(1)));
        return clock;
    }

    private static Lot Vehicle() => SampleLot.Build(reservePrice: Reserve);

    // The reserve is the seller's, and sellers do not publish it. This is the
    // single clearest thing the server buys: on a frontend-only build the
    // number ships to the browser and sits in the network tab.
    [Fact]
    public void A_lot_never_carries_its_reserve_price_over_the_wire()
    {
        var summary = LotMapper.ToSummary(new LotView(Vehicle(), 22_800m, 16), Clock());

        var json = JsonSerializer.Serialize(summary, Wire);

        Assert.DoesNotContain("31337", json);
        Assert.DoesNotContain("reservePrice", json);
    }

    [Fact]
    public void A_lot_detail_never_carries_its_reserve_price_over_the_wire()
    {
        var detail = LotMapper.ToDetail(new LotView(Vehicle(), 22_800m, 16), Clock());

        var json = JsonSerializer.Serialize(detail, Wire);

        Assert.DoesNotContain("31337", json);
        Assert.DoesNotContain("reservePrice", json);
    }

    // Guards the boundary itself rather than one instance of it, so a field
    // added to a contract later cannot quietly reopen the leak.
    [Fact]
    public void No_buyer_facing_contract_exposes_a_reserve_amount()
    {
        var offenders = new[] { typeof(LotSummary), typeof(LotDetail) }
            .SelectMany(contract => contract.GetProperties())
            .Where(property =>
                property.Name.Contains("reserve", StringComparison.OrdinalIgnoreCase) &&
                property.PropertyType != typeof(ReserveStatus))
            .Select(property => property.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Buyers_are_told_whether_the_reserve_is_met_not_what_it_is()
    {
        var clock = Clock();

        var under = LotMapper.ToSummary(new LotView(Vehicle(), Reserve - 500m, 3), clock);
        var over = LotMapper.ToSummary(new LotView(Vehicle(), Reserve, 4), clock);

        Assert.Equal(ReserveStatus.NotMet, under.Reserve);
        Assert.Equal(ReserveStatus.Met, over.Reserve);
    }

    [Fact]
    public void A_lot_with_no_reserve_says_so()
    {
        var lot = Vehicle();
        lot.ReservePrice = null;

        var summary = LotMapper.ToSummary(new LotView(lot, 20_000m, 2), Clock());

        Assert.Equal(ReserveStatus.None, summary.Reserve);
    }

    // 112 of the 200 lots have no bids. A price of null must survive to the
    // client as null, because the UI has to say "opens at $14,500" rather than
    // pricing the vehicle at nothing.
    [Fact]
    public void A_lot_with_no_bids_is_priced_at_nothing_rather_than_zero()
    {
        var summary = LotMapper.ToSummary(new LotView(Vehicle(), null, 0), Clock());

        Assert.Null(summary.CurrentBid);
        Assert.Equal(0, summary.BidCount);
        Assert.Equal(14_500m, summary.MinimumNextBid);
        Assert.Contains("\"currentBid\":null", JsonSerializer.Serialize(summary, Wire));
    }

    // The client gets real instants and does ordinary date maths on them. The
    // offset between real time and the sale is the server's business; handing
    // it over would only invite the client to re-implement the anchoring.
    [Fact]
    public void Lot_times_are_returned_on_the_real_clock_not_the_sale_clock()
    {
        var clock = Clock();

        var summary = LotMapper.ToSummary(new LotView(Vehicle(), null, 0), clock);

        Assert.Equal(clock.ToRealTime(Opens), summary.OpensAt);
        Assert.Equal(RealNow - TimeSpan.FromHours(1), summary.OpensAt);
        Assert.Equal(summary.OpensAt + AuctionRules.LotDuration, summary.ClosesAt);
    }

    [Fact]
    public void Enums_cross_the_wire_as_names_rather_than_ordinals()
    {
        var json = JsonSerializer.Serialize(LotMapper.ToSummary(new LotView(Vehicle(), null, 0), Clock()), Wire);

        Assert.Contains("\"state\":\"live\"", json);
        Assert.Contains("\"outcome\":\"pending\"", json);
    }
}
