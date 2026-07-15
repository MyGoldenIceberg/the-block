using System.Globalization;
using System.Text.Json;

namespace TheBlock.Api.Data;

/// <summary>
/// A vehicle exactly as the challenge dataset ships it.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a faithful mirror of the JSON rather than a tidied-up model:
/// this is the boundary where the file's shape is dealt with, and the rest of
/// the app never sees it.
/// </para>
/// <para>
/// <c>CurrentBid</c> is null on 112 of the 200 lots -- the README's sample
/// vehicle shows a number, which is misleading. It is nullable here because a
/// lot with no bids is not a lot worth nothing, and neither it nor
/// <c>BidCount</c> becomes a column: both are inputs to the seeded bid ladder,
/// which reproduces them exactly.
/// </para>
/// </remarks>
public sealed record VehicleRecord(
    string Id,
    string Vin,
    int Year,
    string Make,
    string Model,
    string? Trim,
    string BodyStyle,
    string ExteriorColor,
    string InteriorColor,
    string Engine,
    string Transmission,
    string Drivetrain,
    int OdometerKm,
    string FuelType,
    decimal ConditionGrade,
    string ConditionReport,
    List<string> DamageNotes,
    string TitleStatus,
    string Province,
    string City,
    string AuctionStart,
    decimal StartingBid,
    decimal? ReservePrice,
    decimal? BuyNowPrice,
    List<string> Images,
    string SellingDealership,
    string Lot,
    decimal? CurrentBid,
    int BidCount);

public static class VehicleDataset
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>Where the dataset lands next to the built assembly.</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "data", "vehicles.json");

    public static IReadOnlyList<VehicleRecord> Load(string? path = null)
    {
        var resolved = path ?? DefaultPath;
        using var stream = File.OpenRead(resolved);

        return JsonSerializer.Deserialize<List<VehicleRecord>>(stream, Options)
            ?? throw new InvalidOperationException($"No vehicles found in {resolved}.");
    }

    /// <summary>
    /// Reads a dataset timestamp onto the sale timeline.
    /// </summary>
    /// <remarks>
    /// The dataset's timestamps carry no zone ("2026-04-05T14:00:00"), so they
    /// are read as UTC. Inventing a real zone would be false precision: the
    /// sale timeline is offset from real time anyway, which strips these
    /// instants of any absolute meaning. What matters is that every timestamp
    /// in the database shares one offset -- the provider stores them as text,
    /// and mixed offsets would sort wrongly.
    /// </remarks>
    public static DateTimeOffset ParseSaleTime(string timestamp) =>
        DateTimeOffset.Parse(
            timestamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    /// <summary>
    /// The lane a lot is catalogued under, from its lot number ("A-0043").
    /// </summary>
    /// <remarks>
    /// A lane here partitions the catalogue; it is not a run order. The
    /// generator assigns lot numbers in blocks of fifty but draws each open
    /// time independently, so lane A opens A-0001 on Apr 5 and A-0002 on Mar
    /// 31. Nothing runs in lot sequence.
    /// </remarks>
    public static string LaneOf(string lotNumber) => lotNumber[..1];
}
