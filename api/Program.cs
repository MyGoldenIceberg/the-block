using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TheBlock.Api.Api;
using TheBlock.Api.Data;
using TheBlock.Api.Domain;

var builder = WebApplication.CreateBuilder(args);

// Enums cross the wire as names, not ordinals: "preview" reads in a network
// tab and survives a member being reordered.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "theblock.db");

builder.Services.AddDbContext<AuctionDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Auction") ?? $"Data Source={databasePath}"));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<SaleClock>();
builder.Services.AddScoped<SaleSeeder>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();

    // EnsureCreated rather than migrations: the schema is fixed and there is
    // no deployed data to evolve, so this keeps `dotnet run` as the entire
    // setup story -- no dotnet-ef tool to install, no migration step to
    // forget. Migrations would earn their keep the moment this had users.
    await database.Database.EnsureCreatedAsync();

    // Bidding writes while the lifecycle notifier reads. WAL lets those
    // overlap instead of surfacing as "database is locked".
    await database.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

    await scope.ServiceProvider.GetRequiredService<SaleSeeder>().SeedAsync();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapLotEndpoints();

app.Run();
