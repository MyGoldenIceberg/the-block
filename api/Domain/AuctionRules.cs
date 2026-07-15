namespace TheBlock.Api.Domain;

/// <summary>
/// The tunable rules of the sale, in one place.
/// </summary>
/// <remarks>
/// These are product decisions, not arbitrary constants, and the dataset
/// constrains most of them. See <see cref="SaleAnchor"/> for how the sale
/// timeline is positioned against real time.
/// </remarks>
public static class AuctionRules
{
    /// <summary>
    /// How long a lot stays open for bidding once it opens.
    /// </summary>
    /// <remarks>
    /// Must be a whole number of days. The dataset only ever opens lots
    /// between 09:00 and 20:00, so a 24h run closes them inside that same
    /// band. A 12h or 36h run would hammer lots in the middle of the night.
    /// </remarks>
    public static readonly TimeSpan LotDuration = TimeSpan.FromHours(24);

    /// <summary>
    /// How long before its close a lot counts as being "on the block".
    /// </summary>
    /// <remarks>
    /// The dataset cannot support a literal block: seven lots share the
    /// timestamp 2026-04-06T10:00, and lot order within a lane is unrelated
    /// to open time, so lots cannot be crossing a ramp in sequence. The sale
    /// is therefore timed-online, and the block metaphor is spent where it
    /// actually means something: the closing moment.
    /// </remarks>
    public static readonly TimeSpan BlockWindow = TimeSpan.FromMinutes(10);

    /// <summary>
    /// A bid inside <see cref="BlockWindow"/> pushes the close out by this
    /// much, so a lot cannot be won by sniping the last second.
    /// </summary>
    public static readonly TimeSpan BlockExtension = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How far through the sale the timeline sits when the anchor is set.
    /// </summary>
    /// <remarks>
    /// Chosen from the composition it produces rather than for its own sake:
    /// at 1/3 most of the catalogue is still ahead (a marketplace, not a
    /// graveyard) while enough lots are open to browse and enough have ended
    /// to show outcomes.
    /// </remarks>
    public const double AnchorFraction = 1.0 / 3.0;

    /// <summary>
    /// How long after the anchor is set the first lot should hammer.
    /// </summary>
    public static readonly TimeSpan AnchorLead = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Re-anchor a stale database once the share of lots still to open falls
    /// below this.
    /// </summary>
    /// <remarks>
    /// The sale only spans ~179h, so an untouched database is entirely ended
    /// within about four days. Without this guard a reviewer returning after
    /// a week is shown 200 dead lots.
    /// </remarks>
    public const double StalePreviewShare = 0.25;
}
