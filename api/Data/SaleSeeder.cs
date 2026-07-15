using Microsoft.EntityFrameworkCore;
using TheBlock.Api.Domain;

namespace TheBlock.Api.Data;

/// <summary>
/// Loads the sale into the database and positions it against real time.
/// </summary>
public sealed class SaleSeeder(AuctionDbContext database, SaleClock clock, ILogger<SaleSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var vehicles = VehicleDataset.Load();
        var opens = vehicles.Select(vehicle => VehicleDataset.ParseSaleTime(vehicle.AuctionStart)).ToList();

        await ApplyAnchorAsync(opens, cancellationToken);

        if (await database.Lots.AnyAsync(cancellationToken))
        {
            return;
        }

        var lots = vehicles.Select(ToLot).ToList();
        database.Lots.AddRange(lots);

        // Written after the anchor, and only ever with it: the offset decides
        // which lots have already run, and so which bidding is already history.
        var anchoredNow = clock.SaleNow;
        var bids = vehicles
            .SelectMany(vehicle => BidScript.Generate(vehicle, VehicleDataset.ParseSaleTime(vehicle.AuctionStart), anchoredNow))
            .ToList();

        database.Bids.AddRange(bids);
        await database.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Lots} lots and {Bids} scripted bids.", lots.Count, bids.Count);
    }

    /// <summary>
    /// Establishes the offset between real time and the sale timeline.
    /// </summary>
    /// <remarks>
    /// Runs before anything else is seeded: the offset decides which lots have
    /// already run, and so what their bidding history has to look like.
    /// </remarks>
    private async Task ApplyAnchorAsync(List<DateTimeOffset> opens, CancellationToken cancellationToken)
    {
        var anchor = await database.SaleAnchor.FirstOrDefaultAsync(cancellationToken);

        if (anchor is null)
        {
            anchor = new SaleAnchorState();
            database.SaleAnchor.Add(anchor);
            Reanchor(anchor, opens, "first run");
        }
        else
        {
            clock.SetAnchor(anchor.Offset);

            // The sale only spans about 179 hours, so a database left alone
            // for a few days has run past its own end. Without this a reviewer
            // coming back next week opens the app to 200 dead lots.
            if (SaleAnchor.IsStale(opens, clock.SaleNow))
            {
                Reanchor(anchor, opens, "sale had run down");
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private void Reanchor(SaleAnchorState anchor, List<DateTimeOffset> opens, string reason)
    {
        anchor.Offset = SaleAnchor.ComputeOffset(opens, clock.RealNow);
        anchor.AnchoredAtRealUtc = clock.RealNow;
        clock.SetAnchor(anchor.Offset);

        logger.LogInformation("Anchored the sale ({Reason}); sale time is now {SaleNow:u}.", reason, clock.SaleNow);
    }

    private static Lot ToLot(VehicleRecord vehicle) => new()
    {
        Id = vehicle.Id,
        Vin = vehicle.Vin,
        LotNumber = vehicle.Lot,
        Lane = VehicleDataset.LaneOf(vehicle.Lot),
        Year = vehicle.Year,
        Make = vehicle.Make,
        Model = vehicle.Model,
        Trim = vehicle.Trim,
        BodyStyle = vehicle.BodyStyle,
        ExteriorColor = vehicle.ExteriorColor,
        InteriorColor = vehicle.InteriorColor,
        Engine = vehicle.Engine,
        Transmission = vehicle.Transmission,
        Drivetrain = vehicle.Drivetrain,
        FuelType = vehicle.FuelType,
        OdometerKm = vehicle.OdometerKm,
        ConditionGrade = vehicle.ConditionGrade,
        ConditionReport = vehicle.ConditionReport,
        DamageNotes = vehicle.DamageNotes,
        TitleStatus = vehicle.TitleStatus,
        Province = vehicle.Province,
        City = vehicle.City,
        SellingDealership = vehicle.SellingDealership,
        Images = vehicle.Images,

        // Stored verbatim. The dataset's schedule is the fact; the offset that
        // drags it into the present is applied at the edges, never baked in.
        OpensAt = VehicleDataset.ParseSaleTime(vehicle.AuctionStart),
        Extension = TimeSpan.Zero,

        StartingBid = vehicle.StartingBid,
        ReservePrice = vehicle.ReservePrice,
        BuyNowPrice = vehicle.BuyNowPrice,
    };
}
