namespace TheBlock.Api.Data;

public sealed class Bid
{
    public int Id { get; set; }
    public required string LotId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Who bid. An opaque id the browser generates; there are no accounts here.</summary>
    public required string BidderId { get; set; }

    /// <summary>
    /// When the bid was placed, on the sale timeline.
    /// </summary>
    /// <remarks>
    /// This is the coordinate the domain reads. Storing bids in sale time is
    /// what lets a re-anchor drag lots and their bids along together: a bid
    /// placed five minutes before a close stays five minutes before that
    /// close, on any timeline.
    /// </remarks>
    public DateTimeOffset PlacedAtSale { get; set; }

    /// <summary>
    /// When the bid really happened. Audit only; no domain logic reads this.
    /// </summary>
    public DateTimeOffset CreatedAtRealUtc { get; set; }

    /// <summary>
    /// True for bids that came with the seeded sale rather than from a buyer.
    /// </summary>
    public bool IsSeeded { get; set; }

    public Lot? Lot { get; set; }
}
