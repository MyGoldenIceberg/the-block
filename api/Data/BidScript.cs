using TheBlock.Api.Domain;

namespace TheBlock.Api.Data;

/// <summary>
/// Writes each lot's bidding as a script laid out along the sale timeline.
/// </summary>
/// <remarks>
/// <para>
/// A lot's price is the highest bid standing at the current sale time, so the
/// whole of a lot's bidding can be written up front and simply revealed as the
/// clock reaches it. That is what makes the sale feel inhabited: bids land
/// while a buyer watches, including overnight when nothing opens or closes.
/// Without it the live channel has nothing to carry -- there are no other
/// users -- and two browser windows passing one bid between them is not a
/// marketplace.
/// </para>
/// <para>
/// It also makes the reveal reversible. Scrub the clock backward and bids
/// un-happen, because nothing was ever stored as a running total.
/// </para>
/// <para>
/// The script is deterministic per lot, so the same database always tells the
/// same story.
/// </para>
/// </remarks>
public static class BidScript
{
    /// <summary>
    /// How far ahead of a lot opening buyers start pre-bidding.
    /// </summary>
    /// <remarks>
    /// Wide enough to reach back before the anchored "now" for every lot in
    /// the catalogue, so a lot that has not opened yet still shows the bids
    /// the dataset says it has.
    /// </remarks>
    private static readonly TimeSpan PreviewWindow = TimeSpan.FromDays(7);

    /// <summary>Share of lots with no bidding history that attract none at all.</summary>
    private const double SilentShare = 0.30;

    public static List<Bid> Generate(VehicleRecord vehicle, DateTimeOffset opensAt, DateTimeOffset anchoredNow)
    {
        var random = new Random(StableSeed(vehicle.Id));
        var bids = new List<Bid>();
        var preBidHigh = vehicle.CurrentBid;

        // Every bid the dataset credits a lot with is a pre-bid: it was
        // written when all 200 lots were still upcoming. Reproducing them
        // before the lot opens is what turns "88 lots hold bids they cannot
        // have" from a data fault into the pre-bid rule.
        if (vehicle.BidCount > 0 && preBidHigh is { } currentBid)
        {
            var amounts = PreBidLadder(vehicle.StartingBid, currentBid, vehicle.BidCount);

            // Ends strictly before the lot opens, or at the anchored "now" for
            // a lot that has not opened yet -- otherwise its pre-bids would
            // sit in the future and the lot would show as having none.
            var closed = opensAt <= anchoredNow ? opensAt - TimeSpan.FromSeconds(1) : anchoredNow;
            var times = SpreadTimes(opensAt - PreviewWindow, closed, amounts.Count, random, bias: 1.0);

            bids.AddRange(amounts.Select((amount, index) => Seeded(vehicle.Id, amount, times[index], random)));
        }

        bids.AddRange(RunBids(vehicle, opensAt, preBidHigh, random));

        return bids;
    }

    /// <summary>
    /// Bidding across a lot's run, from where pre-bidding left it up to where
    /// it finally lands.
    /// </summary>
    private static IEnumerable<Bid> RunBids(
        VehicleRecord vehicle, DateTimeOffset opensAt, decimal? preBidHigh, Random random)
    {
        var hammer = HammerTarget(vehicle, preBidHigh, random);
        if (hammer is not { } target)
        {
            return [];
        }

        var floor = preBidHigh ?? vehicle.StartingBid;
        var amounts = RunLadder(floor, target, openingBid: preBidHigh is null ? vehicle.StartingBid : null);
        if (amounts.Count == 0)
        {
            return [];
        }

        // Bidding crowds the close, so the tail of the ladder is held back for
        // the block itself and a lot goes out in a flurry rather than in
        // silence. Spread evenly, roughly twenty bids across a 24-hour run is
        // one every seventy minutes, and the closing minutes -- the only part
        // anyone actually watches -- would be empty.
        var closesAt = opensAt + AuctionRules.LotDuration;
        var blockOpens = closesAt - AuctionRules.BlockWindow;

        var onTheBlock = Math.Min(amounts.Count, Math.Max(3, amounts.Count / 3));
        var earlier = amounts.Count - onTheBlock;

        var times = new List<DateTimeOffset>();
        if (earlier > 0)
        {
            times.AddRange(SpreadTimes(opensAt, blockOpens, earlier, random, bias: 2.0));
        }

        // Hard toward the hammer. Bidding does not merely lean late, it piles
        // into the last moments, and spread evenly across the block window
        // even this tail is one bid every ninety seconds.
        times.AddRange(SpreadTimes(blockOpens, closesAt - TimeSpan.FromSeconds(15), onTheBlock, random, bias: 4.5));

        return amounts.Select((amount, index) => Seeded(vehicle.Id, amount, times[index], random));
    }

    /// <summary>
    /// Where a lot's bidding finally lands, or null if nobody bids on it.
    /// </summary>
    /// <remarks>
    /// The dataset draws current_bid from 72-102% of reserve, so taken at face
    /// value only 4 of 200 lots ever clear it and a results view would read as
    /// 11% conversion against roughly 60% in real wholesale -- which looks
    /// like a bug rather than a sale. Bidding across the run is what closes
    /// that gap, so the target is drawn around the reserve rather than under
    /// it.
    /// </remarks>
    private static decimal? HammerTarget(VehicleRecord vehicle, decimal? preBidHigh, Random random)
    {
        var floor = preBidHigh ?? vehicle.StartingBid;

        // A lot that already drew pre-bids has proven interest. One that drew
        // none may well draw none at all, which is a no-sale.
        if (preBidHigh is null && random.NextDouble() < SilentShare)
        {
            return null;
        }

        var target = vehicle.ReservePrice is { } reserve
            ? reserve * Between(0.88m, 1.18m, random)
            : floor * Between(1.05m, 1.60m, random);

        // Nobody bids past the buy-now price: they would just buy it.
        if (vehicle.BuyNowPrice is { } buyNow)
        {
            target = Math.Min(target, buyNow - BidPolicy.IncrementAt(buyNow));
        }

        target = RoundTo(target, 50m);

        return target < floor + BidPolicy.IncrementAt(floor) ? null : target;
    }

    /// <summary>
    /// The bids that carried a lot to its pre-bid price.
    /// </summary>
    /// <remarks>
    /// Walks down from the recorded price by legal increments. On 21 of the 88
    /// lots that carry bids the dataset's own numbers cannot be squared with
    /// an increment ladder -- lot D-0013 records 17 bids across a $500 spread,
    /// which no $500 increment can produce -- so those fall back to an even
    /// climb. Both dataset facts are worth more than a tidy ladder in history
    /// that nobody can bid against anyway; the increment rule governs new bids,
    /// where it is enforced.
    /// </remarks>
    private static List<decimal> PreBidLadder(decimal startingBid, decimal currentBid, int count)
    {
        if (count == 1)
        {
            return [currentBid];
        }

        var descending = new List<decimal> { currentBid };
        var price = currentBid;

        while (descending.Count < count)
        {
            var previous = price - BidPolicy.IncrementAt(price);
            if (previous < startingBid)
            {
                break;
            }

            descending.Add(previous);
            price = previous;
        }

        if (descending.Count == count)
        {
            descending.Reverse();
            return descending;
        }

        return EvenLadder(startingBid, currentBid, count);
    }

    private static List<decimal> EvenLadder(decimal low, decimal high, int count)
    {
        var step = (high - low) / count;
        var amounts = Enumerable
            .Range(1, count)
            .Select(index => Math.Round(low + (step * index)))
            .ToList();

        amounts[^1] = high;
        return amounts;
    }

    private static List<decimal> RunLadder(decimal floor, decimal target, decimal? openingBid)
    {
        var amounts = new List<decimal>();

        // A lot nobody pre-bid on opens with someone taking the starting bid.
        var price = floor;
        if (openingBid is { } opening)
        {
            amounts.Add(opening);
            price = opening;
        }

        while (true)
        {
            var next = price + BidPolicy.IncrementAt(price);
            if (next > target)
            {
                break;
            }

            amounts.Add(next);
            price = next;
        }

        return amounts;
    }

    /// <summary>
    /// Ascending instants across a window. A bias above 1 crowds them toward
    /// the end, the way bidding crowds a close.
    /// </summary>
    private static List<DateTimeOffset> SpreadTimes(
        DateTimeOffset from, DateTimeOffset to, int count, Random random, double bias)
    {
        var span = to - from;

        return Enumerable
            .Range(0, count)
            .Select(_ => Math.Pow(random.NextDouble(), 1.0 / bias))
            .Order()
            .Select(fraction => from + (span * fraction))
            .ToList();
    }

    private static Bid Seeded(string lotId, decimal amount, DateTimeOffset placedAtSale, Random random) => new()
    {
        LotId = lotId,
        Amount = amount,
        BidderId = $"lane-{random.Next(1000, 9999)}",
        PlacedAtSale = placedAtSale,
        CreatedAtRealUtc = placedAtSale,
        IsSeeded = true,
    };

    private static decimal Between(decimal low, decimal high, Random random) =>
        low + ((high - low) * (decimal)random.NextDouble());

    private static decimal RoundTo(decimal value, decimal unit) => Math.Round(value / unit) * unit;

    /// <summary>
    /// A hash that does not move between runs. String.GetHashCode is seeded
    /// per process, which would re-tell every lot's story on each restart.
    /// </summary>
    private static int StableSeed(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
            {
                hash = (hash * 31) + character;
            }

            return hash;
        }
    }
}
