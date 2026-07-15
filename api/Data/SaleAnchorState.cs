using System.ComponentModel.DataAnnotations.Schema;

namespace TheBlock.Api.Data;

/// <summary>
/// The persisted offset between real time and the sale timeline. One row.
/// </summary>
/// <remarks>
/// Persisted rather than recomputed per process so the sale does not lurch
/// backward every time the API restarts -- including on hot reload, which
/// would otherwise rewind the world mid-demo.
/// </remarks>
public sealed class SaleAnchorState
{
    public int Id { get; set; }

    /// <summary>
    /// Stored as ticks. SQLite has no interval type, and a number sorts and
    /// compares the way a duration should.
    /// </summary>
    public long OffsetTicks { get; set; }

    public DateTimeOffset AnchoredAtRealUtc { get; set; }

    /// <summary>
    /// Convenience over <see cref="OffsetTicks"/>. Not mapped: left to EF it
    /// would persist a second, redundant column holding a TimeSpan as text.
    /// </summary>
    [NotMapped]
    public TimeSpan Offset
    {
        get => TimeSpan.FromTicks(OffsetTicks);
        set => OffsetTicks = value.Ticks;
    }
}
