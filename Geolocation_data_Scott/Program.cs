using GeoViz.Components;
using GeoViz.Data;

var builder = WebApplication.CreateBuilder(args);

// Export the embedded seed as a plain Cypher script, then exit. This keeps
// seed.cypher and the app's seed data in sync from a single source of truth.
if (args.Contains("--export-seed"))
{
    var target = Path.Combine(builder.Environment.ContentRootPath, "seed.cypher");
    await File.WriteAllTextAsync(target, GeoSeed.ToCypher());
    Console.WriteLine($"Wrote {target}");
    return;
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// One driver for the whole app; it is thread-safe and pools connections.
builder.Services.Configure<Neo4jOptions>(
    builder.Configuration.GetSection(Neo4jOptions.SectionName));
builder.Services.AddSingleton<IGeoStore, Neo4jGeoStore>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
