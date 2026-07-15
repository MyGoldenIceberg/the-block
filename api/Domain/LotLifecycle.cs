namespace TheBlock.Api.Domain;

public enum LotState
{
    /// <summary>Not yet open. Buyers may pre-bid.</summary>
    Preview,

    /// <summary>Open for bidding.</summary>
    Live,

    /// <summary>Closed.</summary>
    Ended,
}

public enum ReserveStatus
{
    /// <summary>No reserve was set: the lot sells to the highest bidder.</summary>
    None,

    /// <summary>Bidding has reached the seller's reserve. The lot will sell.</summary>
    Met,

    /// <summary>Bidding is below the seller's reserve.</summary>
    NotMet,
}

public enum LotOutcome
{
    /// <summary>The lot has not closed yet.</summary>
    Pending,

    /// <summary>Closed without a single bid.</summary>
    NoSale,

    /// <summary>Closed at or above reserve, or with no reserve to clear.</summary>
    Sold,

    /// <summary>
    /// Closed below reserve. The high bid goes to the seller, who may still
    /// accept it. Known in the lane as an "IF" — an if-sale.
    /// </summary>
    IfSale,
}

/// <summary>
/// Where a lot sits in its run, and how it ended up.
/// </summary>
public sealed record LotStatus(
    LotState State,
    bool OnTheBlock,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    ReserveStatus Reserve,
    LotOutcome Outcome)
{
    /// <summary>
    /// Pre-bids are accepted before a lot opens, which is why the dataset can
    /// ship lots carrying bids whose auction_start has not arrived yet.
    /// </summary>
    public bool AcceptsBids => State is LotState.Preview or LotState.Live;
}

/// <summary>
/// Derives a lot's state from the sale clock.
/// </summary>
/// <remarks>
/// <para>
/// State is a pure function of sale time and is computed on read, never
/// stored. It therefore cannot drift out of step with the underlying data,
/// and it reverses cleanly when the dev scrubber moves the clock backward.
/// </para>
/// <para>
/// The dataset gives no auction_end, so a lot's run is a policy: it opens at
/// auction_start and closes <see cref="AuctionRules.LotDuration"/> later.
/// </para>
/// </remarks>
public static class LotLifecycle
{
    public static DateTimeOffset ClosesAt(DateTimeOffset opensAt, TimeSpan extension) =>
        opensAt + AuctionRules.LotDuration + extension;

    public static LotStatus Evaluate(
        DateTimeOffset opensAt,
        TimeSpan extension,
        decimal? reservePrice,
        decimal? highBid,
        DateTimeOffset saleNow)
    {
        var closesAt = ClosesAt(opensAt, extension);

        var state = saleNow < opensAt ? LotState.Preview
            : saleNow < closesAt ? LotState.Live
            : LotState.Ended;

        // Not a fourth state: a lot is on the block for the closing stretch of
        // its run, when bidding gets urgent and late bids extend the clock.
        var onTheBlock = state is LotState.Live && closesAt - saleNow <= AuctionRules.BlockWindow;

        var reserve = reservePrice is null ? ReserveStatus.None
            : highBid >= reservePrice ? ReserveStatus.Met
            : ReserveStatus.NotMet;

        var outcome = state is not LotState.Ended ? LotOutcome.Pending
            : highBid is null ? LotOutcome.NoSale
            : reserve is ReserveStatus.NotMet ? LotOutcome.IfSale
            : LotOutcome.Sold;

        return new LotStatus(state, onTheBlock, opensAt, closesAt, reserve, outcome);
    }
}
