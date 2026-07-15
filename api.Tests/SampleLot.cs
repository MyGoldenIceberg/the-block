using TheBlock.Api.Data;

namespace TheBlock.Api.Tests;

internal static class SampleLot
{
    internal static readonly DateTimeOffset Opens = new(2026, 4, 3, 9, 0, 0, TimeSpan.Zero);

    internal static Lot Build(string id = "test-lot", decimal? reservePrice = null) => new()
    {
        Id = id,
        Vin = "TRD7L1KS0HNB5X3K3",
        LotNumber = "A-0043",
        Lane = "A",
        Year = 2023,
        Make = "Ford",
        Model = "Bronco",
        Trim = "Big Bend",
        BodyStyle = "SUV",
        ExteriorColor = "Burgundy",
        InteriorColor = "Beige",
        Engine = "2.7L EcoBoost V6",
        Transmission = "automatic",
        Drivetrain = "4WD",
        FuelType = "gasoline",
        OdometerKm = 47_731,
        ConditionGrade = 3.8m,
        ConditionReport = "Average condition.",
        DamageNotes = ["Scratch on liftgate"],
        TitleStatus = "clean",
        Province = "Ontario",
        City = "Toronto",
        SellingDealership = "King City Auto",
        Images = ["https://example.invalid/photo-1", "https://example.invalid/photo-2"],
        OpensAt = Opens,
        Extension = TimeSpan.Zero,
        StartingBid = 14_500m,
        ReservePrice = reservePrice,
        BuyNowPrice = null,
    };
}
