using Microsoft.EntityFrameworkCore;
using TheBlock.Api.Data;
using TheBlock.Api.Domain;

var builder = WebApplication.CreateBuilder(args);

var databasePath = Path.Combine(builder.Environment.ContentRootPath, "theblock.db");

builder.Services.AddDbContext<AuctionDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Auction") ?? $"Data Source={databasePath}"));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<SaleClock>();

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
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();
