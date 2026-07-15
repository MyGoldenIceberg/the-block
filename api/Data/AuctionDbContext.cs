using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TheBlock.Api.Data;

public sealed class AuctionDbContext(DbContextOptions<AuctionDbContext> options) : DbContext(options)
{
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<SaleAnchorState> SaleAnchor => Set<SaleAnchorState>();

    /// <summary>
    /// Money is stored as whole cents.
    /// </summary>
    /// <remarks>
    /// SQLite has no decimal type, and EF Core's default mapping falls back to
    /// TEXT. Text compares lexicographically, which would quietly break the
    /// two things bidding depends on most: "9500" sorts above "10000", so the
    /// highest bid on a lot would be wrong, and so would every price filter.
    /// An integer sorts and aggregates the way money should.
    /// </remarks>
    private static readonly ValueConverter<decimal, long> MoneyToCents = new(
        money => (long)Math.Round(money * 100m, MidpointRounding.AwayFromZero),
        cents => cents / 100m);

    /// <summary>
    /// Durations are stored as ticks, for the same reason money is stored as
    /// cents: SQLite has no interval type.
    /// </summary>
    private static readonly ValueConverter<TimeSpan, long> DurationToTicks = new(
        duration => duration.Ticks,
        ticks => TimeSpan.FromTicks(ticks));

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Lot>(lot =>
        {
            lot.HasKey(entity => entity.Id);

            // Bidding is serialised through the lot, so the aggregate root
            // carries the concurrency token rather than each bid.
            lot.Property(entity => entity.Version).IsConcurrencyToken();

            // Not money, so it does not need exact cents -- but it is a filter
            // buyers lean on, and left as the default TEXT mapping "grade at
            // least 3.5" would be a string comparison.
            lot.Property(entity => entity.ConditionGrade).HasConversion<double>();

            lot.Property(entity => entity.StartingBid).HasConversion(MoneyToCents);
            lot.Property(entity => entity.ReservePrice).HasConversion(MoneyToCents);
            lot.Property(entity => entity.BuyNowPrice).HasConversion(MoneyToCents);
            lot.Property(entity => entity.Extension).HasConversion(DurationToTicks);

            lot.HasIndex(entity => entity.OpensAt);
            lot.HasIndex(entity => entity.LotNumber).IsUnique();

            lot.HasMany(entity => entity.Bids)
                .WithOne(bid => bid.Lot)
                .HasForeignKey(bid => bid.LotId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Bid>(bid =>
        {
            bid.HasKey(entity => entity.Id);
            bid.Property(entity => entity.Amount).HasConversion(MoneyToCents);

            // Every read of a lot's price is "the highest bid placed at or
            // before the current sale time", so that is the index.
            bid.HasIndex(entity => new { entity.LotId, entity.PlacedAtSale });
        });

        builder.Entity<SaleAnchorState>(anchor => anchor.HasKey(entity => entity.Id));
    }
}
