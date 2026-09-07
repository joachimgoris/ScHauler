using Microsoft.EntityFrameworkCore;
using ScHauler.Components;
using ScHauler.Data;
using ScHauler.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("Hauler") ?? "Data Source=data/hauler.db";

builder.Services.AddDbContextFactory<HaulerDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<ManifestService>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

// Self-bootstrapping: create schema + seed on first run.
Directory.CreateDirectory("data");
await using (var db = await app.Services.GetRequiredService<IDbContextFactory<HaulerDbContext>>().CreateDbContextAsync())
{
    await db.Database.EnsureCreatedAsync();
    await LocationSeed.EnsureSeededAsync(db);
}

app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();