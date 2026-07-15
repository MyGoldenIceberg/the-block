namespace TheBlock.Api.Domain;

/// <summary>
/// The wall clock, injected so it can be faked in tests.
/// </summary>
/// <remarks>
/// Nothing outside <see cref="SaleClock"/> should read the wall clock. A call
/// site that reaches for <c>DateTimeOffset.UtcNow</c> directly is invisible to
/// the sale anchor and to the dev scrubber, and will silently disagree with
/// every other part of the app about what time it is.
/// </remarks>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
