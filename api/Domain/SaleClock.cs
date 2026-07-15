namespace TheBlock.Api.Domain;

/// <summary>
/// The single source of "now" for the sale.
/// </summary>
/// <remarks>
/// <para>
/// Holds the two components of the offset between real time and sale time:
/// the anchor, computed once when the database is seeded and persisted with
/// it, and the scrub, a dev-only nudge used to demonstrate lots opening and
/// closing on demand. They are kept apart so the scrub can be reset without
/// disturbing the anchor, but every reader sees only their sum.
/// </para>
/// <para>
/// Because both are stored rather than recomputed, <see cref="SaleNow"/>
/// advances exactly as fast as real time.
/// </para>
/// </remarks>
public sealed class SaleClock(IClock clock)
{
    private long _anchorTicks;
    private long _scrubTicks;

    /// <summary>The persisted offset placing the sale timeline against real time.</summary>
    public TimeSpan Anchor => TimeSpan.FromTicks(Interlocked.Read(ref _anchorTicks));

    /// <summary>The dev-only offset applied on top of the anchor.</summary>
    public TimeSpan Scrub => TimeSpan.FromTicks(Interlocked.Read(ref _scrubTicks));

    /// <summary>The total offset between real time and sale time.</summary>
    public TimeSpan Offset => Anchor + Scrub;

    /// <summary>Real wall-clock time. Only for audit trails and skew correction.</summary>
    public DateTimeOffset RealNow => clock.UtcNow;

    /// <summary>The current instant on the sale timeline. Everything derives from this.</summary>
    public DateTimeOffset SaleNow => clock.UtcNow - Offset;

    public void SetAnchor(TimeSpan anchor) => Interlocked.Exchange(ref _anchorTicks, anchor.Ticks);

    public void SetScrub(TimeSpan scrub) => Interlocked.Exchange(ref _scrubTicks, scrub.Ticks);

    /// <summary>
    /// Moves a real instant onto the sale timeline, for storage.
    /// </summary>
    /// <remarks>
    /// Bids are stored in sale time so that a re-anchor moves lots and their
    /// bids together. A bid placed five minutes before a lot closed stays five
    /// minutes before that close under any anchor, and still renders as having
    /// happened at the moment the buyer clicked.
    /// </remarks>
    public DateTimeOffset ToSaleTime(DateTimeOffset real) => real - Offset;

    /// <summary>
    /// Moves a sale instant back onto real time, for display.
    /// </summary>
    public DateTimeOffset ToRealTime(DateTimeOffset sale) => sale + Offset;
}
