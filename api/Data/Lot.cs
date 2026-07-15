namespace TheBlock.Api.Data;

/// <summary>
/// A vehicle consigned to the sale, as stored.
/// </summary>
/// <remarks>
/// Times on this entity live on the sale timeline, not the real one. See
/// <see cref="Domain.SaleAnchor"/> for why the database keeps a single
/// coordinate system and the offset is applied only at the edges.
/// </remarks>
public sealed class Lot
{
    public required string Id { get; set; }
    public required string Vin { get; set; }
    public required string LotNumber { get; set; }

    /// <summary>The lane this lot is catalogued under: A, B, C or D.</summary>
    public required string Lane { get; set; }

    public int Year { get; set; }
    public required string Make { get; set; }
    public required string Model { get; set; }
    public string? Trim { get; set; }
    public required string BodyStyle { get; set; }
    public required string ExteriorColor { get; set; }
    public required string InteriorColor { get; set; }

    public required string Engine { get; set; }
    public required string Transmission { get; set; }
    public required string Drivetrain { get; set; }
    public required string FuelType { get; set; }
    public int OdometerKm { get; set; }

    public decimal ConditionGrade { get; set; }
    public required string ConditionReport { get; set; }
    public List<string> DamageNotes { get; set; } = [];
    public required string TitleStatus { get; set; }

    public required string Province { get; set; }
    public required string City { get; set; }
    public required string SellingDealership { get; set; }
    public List<string> Images { get; set; } = [];

    /// <summary>When bidding opens, on the sale timeline. The dataset's auction_start, verbatim.</summary>
    public DateTimeOffset OpensAt { get; set; }

    /// <summary>
    /// Time added to this lot's run by late bidding.
    /// </summary>
    /// <remarks>
    /// Stored as a duration rather than a close timestamp so it survives a
    /// re-anchor untouched: durations mean the same thing on any timeline.
    /// </remarks>
    public TimeSpan Extension { get; set; }

    public decimal StartingBid { get; set; }

    /// <summary>
    /// The seller's reserve.
    /// </summary>
    /// <remarks>
    /// Never leaves the server. Buyers are told whether bidding has cleared
    /// the reserve, never what it is. This is the main reason the prototype
    /// has a backend at all: on a frontend-only build this number would ship
    /// to the browser and sit in the devtools network tab.
    /// </remarks>
    public decimal? ReservePrice { get; set; }

    public decimal? BuyNowPrice { get; set; }

    /// <summary>
    /// Guards this lot's bidding against concurrent writes.
    /// </summary>
    /// <remarks>
    /// The lot is the aggregate root for its own bids. Placing a bid bumps
    /// this, so two buyers bidding the same lot at the same instant cannot
    /// both read the same high bid and both win: the second write finds a
    /// stale version and is rejected.
    /// </remarks>
    public int Version { get; set; }

    public List<Bid> Bids { get; set; } = [];
}
