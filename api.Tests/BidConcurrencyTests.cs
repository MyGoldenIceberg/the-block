using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TheBlock.Api.Data;

namespace TheBlock.Api.Tests;

/// <summary>
/// Two buyers, one lot, one instant.
/// </summary>
/// <remarks>
/// The invariant an auction cannot bend: a bid that was valid when it was read
/// is not valid once someone else has taken that price. Bidding is serialised
/// through the lot -- the aggregate root for its own bids -- so a second
/// writer working from a stale read is refused rather than quietly accepted.
/// </remarks>
public class BidConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AuctionDbContext> _options;

    public BidConcurrencyTests()
    {
        // A real SQLite engine, kept in memory. It lives as long as the
        // connection, so separate contexts can share one database.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AuctionDbContext>().UseSqlite(_connection).Options;

        using var database = new AuctionDbContext(_options);
        database.Database.EnsureCreated();
        database.Lots.Add(SampleLot.Build());
        database.SaveChanges();
    }

    private AuctionDbContext Connect() => new(_options);

    private static Bid BidOf(decimal amount, string bidder) => new()
    {
        LotId = "test-lot",
        Amount = amount,
        BidderId = bidder,
        PlacedAtSale = SampleLot.Opens + TimeSpan.FromHours(1),
        CreatedAtRealUtc = SampleLot.Opens + TimeSpan.FromHours(1),
        IsSeeded = false,
    };

    [Fact]
    public async Task Two_buyers_reading_the_same_price_cannot_both_win()
    {
        await using var alice = Connect();
        await using var bob = Connect();

        // Both read the lot before either of them bids.
        var aliceLot = await alice.Lots.FirstAsync();
        var bobLot = await bob.Lots.FirstAsync();

        alice.Bids.Add(BidOf(15_500m, "alice"));
        aliceLot.Version++;
        await alice.SaveChangesAsync();

        bob.Bids.Add(BidOf(15_500m, "bob"));
        bobLot.Version++;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => bob.SaveChangesAsync());
    }

    // The refused bid must not survive the refusal. SaveChanges is one
    // transaction, so the insert rolls back with the update that failed --
    // otherwise the lot would quietly carry a bid nobody accepted.
    [Fact]
    public async Task A_refused_bid_leaves_nothing_behind()
    {
        await using var alice = Connect();
        await using var bob = Connect();

        var aliceLot = await alice.Lots.FirstAsync();
        var bobLot = await bob.Lots.FirstAsync();

        alice.Bids.Add(BidOf(15_500m, "alice"));
        aliceLot.Version++;
        await alice.SaveChangesAsync();

        bob.Bids.Add(BidOf(16_000m, "bob"));
        bobLot.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => bob.SaveChangesAsync());

        await using var audit = Connect();
        var bids = await audit.Bids.ToListAsync();

        Assert.Single(bids);
        Assert.Equal("alice", bids[0].BidderId);
        Assert.Equal(15_500m, bids[0].Amount);
    }

    [Fact]
    public async Task Bidding_one_after_another_is_not_a_conflict()
    {
        await using (var alice = Connect())
        {
            var lot = await alice.Lots.FirstAsync();
            alice.Bids.Add(BidOf(15_500m, "alice"));
            lot.Version++;
            await alice.SaveChangesAsync();
        }

        await using (var bob = Connect())
        {
            var lot = await bob.Lots.FirstAsync();
            bob.Bids.Add(BidOf(15_750m, "bob"));
            lot.Version++;
            await bob.SaveChangesAsync();
        }

        await using var audit = Connect();
        Assert.Equal(2, await audit.Bids.CountAsync());
        Assert.Equal(2, (await audit.Lots.FirstAsync()).Version);
    }

    // The price is the highest bid standing at a moment, so bids scheduled
    // ahead of that moment are simply not visible yet.
    [Fact]
    public async Task A_lot_is_priced_only_by_the_bids_that_have_happened()
    {
        await using (var seed = Connect())
        {
            seed.Bids.Add(BidOf(15_500m, "alice"));
            seed.Bids.Add(new Bid
            {
                LotId = "test-lot",
                Amount = 90_000m,
                BidderId = "future",
                PlacedAtSale = SampleLot.Opens + TimeSpan.FromHours(10),
                CreatedAtRealUtc = SampleLot.Opens + TimeSpan.FromHours(10),
                IsSeeded = true,
            });
            await seed.SaveChangesAsync();
        }

        await using var database = Connect();
        var saleNow = SampleLot.Opens + TimeSpan.FromHours(2);

        var standing = await database.Bids
            .Where(bid => bid.PlacedAtSale <= saleNow)
            .MaxAsync(bid => bid.Amount);

        Assert.Equal(15_500m, standing);
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
