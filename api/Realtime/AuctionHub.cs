using Microsoft.AspNetCore.SignalR;

namespace TheBlock.Api.Realtime;

/// <summary>
/// The live channel a sale runs on.
/// </summary>
/// <remarks>
/// <para>
/// An auction is a real-time, many-party thing. A price a buyer is reading is
/// a claim about right now, and a stale one costs them the lot. This is the
/// part of the problem a client cannot solve for itself.
/// </para>
/// <para>
/// Updates go to every connection. With 200 lots and a handful of buyers that
/// is cheaper than tracking who is looking at what; a real sale would scope
/// them to the lots a client is actually watching.
/// </para>
/// </remarks>
public sealed class AuctionHub : Hub;

public static class AuctionEvents
{
    /// <summary>A lot's price or bid count moved.</summary>
    public const string LotChanged = "lotChanged";

    /// <summary>A lot opened, went on the block, or closed.</summary>
    public const string LotStateChanged = "lotStateChanged";

    /// <summary>The sale clock was moved. Clients should resync.</summary>
    public const string ClockChanged = "clockChanged";
}
