namespace TheBlock.Api.Domain;

public enum BidRejection
{
    /// <summary>The bid stands.</summary>
    None,

    /// <summary>The lot has already closed.</summary>
    LotClosed,

    /// <summary>The bid is under the minimum the lot will take next.</summary>
    BelowMinimum,
}

public readonly record struct BidVerdict(BidRejection Rejection, decimal MinimumNextBid)
{
    public bool Accepted => Rejection is BidRejection.None;
}

/// <summary>
/// What a lot will accept as its next bid.
/// </summary>
/// <remarks>
/// An auction does not take arbitrary amounts. Bidding climbs a ladder of
/// increments that widens as the money does, so buyers compete in meaningful
/// steps rather than by outbidding each other a dollar at a time. This runs on
/// the server: the client proposes, but it does not get to decide what a valid
/// bid is.
/// </remarks>
public static class BidPolicy
{
    /// <summary>
    /// Bid increments by price band: below $10k, below $25k, and above.
    /// </summary>
    private static readonly (decimal Below, decimal Increment)[] Ladder =
    [
        (10_000m, 100m),
        (25_000m, 250m),
        (decimal.MaxValue, 500m),
    ];

    /// <summary>The step a lot climbs by at a given price.</summary>
    public static decimal IncrementAt(decimal price)
    {
        foreach (var (below, increment) in Ladder)
        {
            if (price < below)
            {
                return increment;
            }
        }

        return Ladder[^1].Increment;
    }

    /// <summary>
    /// The smallest bid a lot will take next.
    /// </summary>
    /// <remarks>
    /// A lot with no bids opens at its starting bid, and that exact amount is
    /// a valid first bid. This is also why a lot without bids must never be
    /// shown as costing nothing: it has a price, nobody has met it yet.
    /// </remarks>
    public static decimal MinimumNextBid(decimal startingBid, decimal? highBid) =>
        highBid is null ? startingBid : highBid.Value + IncrementAt(highBid.Value);

    public static BidVerdict Validate(LotStatus status, decimal startingBid, decimal? highBid, decimal amount)
    {
        var minimum = MinimumNextBid(startingBid, highBid);

        if (!status.AcceptsBids)
        {
            return new BidVerdict(BidRejection.LotClosed, minimum);
        }

        return amount < minimum
            ? new BidVerdict(BidRejection.BelowMinimum, minimum)
            : new BidVerdict(BidRejection.None, minimum);
    }
}
