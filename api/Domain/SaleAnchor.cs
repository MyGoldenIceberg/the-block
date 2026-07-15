namespace TheBlock.Api.Domain;

/// <summary>
/// Positions the dataset's fixed sale timeline against real time.
/// </summary>
/// <remarks>
/// <para>
/// The dataset's auction_start values span a fixed 6.5-day window that was
/// already months in the past by the time the JSON was frozen. Rendered
/// literally, every lot has ended and the marketplace is a graveyard. The
/// challenge brief permits normalising these timestamps relative to "now".
/// </para>
/// <para>
/// The normalisation works by pulling <em>now</em> backward onto the sale
/// timeline rather than pushing 200 lots forward onto the real one. Sale time
/// is therefore <c>realNow - offset</c> where the offset is a stored value,
/// which means sale time advances in lockstep with real time by construction:
/// a lot two hours from closing is one hour from closing an hour later.
/// Deriving the offset from "now" on each read instead would freeze the world,
/// holding every lot at a constant distance forever.
/// </para>
/// </remarks>
public static class SaleAnchor
{
    /// <summary>
    /// The offset to subtract from real time to land on sale time.
    /// </summary>
    public static TimeSpan ComputeOffset(IReadOnlyCollection<DateTimeOffset> opens, DateTimeOffset realNow) =>
        realNow - ComputeTarget(opens);

    /// <summary>
    /// The instant on the sale timeline that should read as "now" when the
    /// anchor is set.
    /// </summary>
    public static DateTimeOffset ComputeTarget(IReadOnlyCollection<DateTimeOffset> opens)
    {
        ArgumentNullException.ThrowIfNull(opens);
        if (opens.Count == 0)
        {
            throw new ArgumentException("Cannot anchor a sale with no lots.", nameof(opens));
        }

        var windowStart = opens.Min();
        var windowEnd = opens.Max();

        // The sale runs until the last lot closes, not until the last one opens.
        var saleSpan = (windowEnd - windowStart) + AuctionRules.LotDuration;
        var nominal = windowStart + (saleSpan * AuctionRules.AnchorFraction);

        // Snapping to a close, rather than landing wherever the fraction fell,
        // guarantees a lot hammers shortly after the anchor is set. Snapping
        // *forward* additionally guarantees the sale keeps moving: lots only
        // open between 09:00 and 20:00, so over half the timeline is an
        // overnight dead zone, and the nominal target usually lands inside one.
        // Snapping to the nearest close would step backward into that gap and
        // leave the world frozen until morning.
        var closes = opens
            .Select(open => open + AuctionRules.LotDuration)
            .Distinct()
            .Order()
            .ToList();

        var snapped = closes.FirstOrDefault(close => close >= nominal, closes[^1]);

        return snapped - AuctionRules.AnchorLead;
    }

    /// <summary>
    /// Whether the sale has decayed far enough that it should be re-anchored.
    /// </summary>
    public static bool IsStale(IReadOnlyCollection<DateTimeOffset> opens, DateTimeOffset saleNow)
    {
        if (opens.Count == 0)
        {
            return false;
        }

        var stillToOpen = opens.Count(open => saleNow < open);
        return (double)stillToOpen / opens.Count < AuctionRules.StalePreviewShare;
    }
}
