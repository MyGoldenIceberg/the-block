using TheBlock.Api.Data;
using TheBlock.Api.Domain;

namespace TheBlock.Api.Api;

/// <summary>
/// What the sale looks like to a buyer right now.
/// </summary>
/// <remarks>
/// Every response carries <see cref="ServerTime"/>. Countdowns cannot be
/// round-tripped per second, so the client runs them off its own clock -- and
/// a laptop ninety seconds fast would show time left on a lot the server has
/// already closed. The client measures its skew against this once and every
/// countdown reads through it.
/// </remarks>
public sealed record SaleEnvelope<T>(DateTimeOffset ServerTime, T Data);

/// <summary>
/// A lot as it appears in the catalogue.
/// </summary>
/// <remarks>
/// <para>
/// There is no reserve price on this record, and that is the point. Buyers are
/// told whether bidding has cleared the reserve, never what it is -- sellers
/// do not publish that. On a frontend-only build the number would ship to the
/// browser and sit in the devtools network tab, visible to anyone who looked.
/// </para>
/// <para>
/// Times are real instants with an offset, not sale-timeline instants. The
/// offset that separates the two never leaves the server; handing it over
/// would only invite the client to re-implement the anchoring.
/// </para>
/// </remarks>
public sealed record LotSummary(
    string Id,
    string LotNumber,
    string Lane,
    int Year,
    string Make,
    string Model,
    string? Trim,
    string BodyStyle,
    int OdometerKm,
    decimal ConditionGrade,
    string TitleStatus,
    string Province,
    string City,
    string SellingDealership,
    string? Thumbnail,
    int DamageCount,
    LotState State,
    bool OnTheBlock,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    decimal StartingBid,
    decimal? CurrentBid,
    int BidCount,
    decimal MinimumNextBid,
    ReserveStatus Reserve,
    decimal? BuyNowPrice,
    LotOutcome Outcome);

public sealed record LotDetail(
    LotSummary Lot,
    string Vin,
    string ExteriorColor,
    string InteriorColor,
    string Engine,
    string Transmission,
    string Drivetrain,
    string FuelType,
    string ConditionReport,
    IReadOnlyList<string> DamageNotes,
    IReadOnlyList<string> Images);

/// <summary>
/// A lot together with the bidding that stands against it at a point in time.
/// </summary>
public sealed record LotView(Lot Lot, decimal? HighBid, int BidCount);

public static class LotMapper
{
    public static LotSummary ToSummary(LotView view, SaleClock clock)
    {
        var lot = view.Lot;
        var status = LotLifecycle.Evaluate(lot.OpensAt, lot.Extension, lot.ReservePrice, view.HighBid, clock.SaleNow);

        return new LotSummary(
            Id: lot.Id,
            LotNumber: lot.LotNumber,
            Lane: lot.Lane,
            Year: lot.Year,
            Make: lot.Make,
            Model: lot.Model,
            Trim: lot.Trim,
            BodyStyle: lot.BodyStyle,
            OdometerKm: lot.OdometerKm,
            ConditionGrade: lot.ConditionGrade,
            TitleStatus: lot.TitleStatus,
            Province: lot.Province,
            City: lot.City,
            SellingDealership: lot.SellingDealership,
            Thumbnail: lot.Images.FirstOrDefault(),
            DamageCount: lot.DamageNotes.Count,
            State: status.State,
            OnTheBlock: status.OnTheBlock,
            OpensAt: clock.ToRealTime(status.OpensAt),
            ClosesAt: clock.ToRealTime(status.ClosesAt),
            StartingBid: lot.StartingBid,
            CurrentBid: view.HighBid,
            BidCount: view.BidCount,
            MinimumNextBid: BidPolicy.MinimumNextBid(lot.StartingBid, view.HighBid),
            Reserve: status.Reserve,
            BuyNowPrice: lot.BuyNowPrice,
            Outcome: status.Outcome);
    }

    public static LotDetail ToDetail(LotView view, SaleClock clock) => new(
        Lot: ToSummary(view, clock),
        Vin: view.Lot.Vin,
        ExteriorColor: view.Lot.ExteriorColor,
        InteriorColor: view.Lot.InteriorColor,
        Engine: view.Lot.Engine,
        Transmission: view.Lot.Transmission,
        Drivetrain: view.Lot.Drivetrain,
        FuelType: view.Lot.FuelType,
        ConditionReport: view.Lot.ConditionReport,
        DamageNotes: view.Lot.DamageNotes,
        Images: view.Lot.Images);
}
