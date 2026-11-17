namespace GeoViz.Data;

/// <summary>Connection settings for the Neo4j instance (section "Neo4j" in appsettings.json).</summary>
public sealed class Neo4jOptions
{
    public const string SectionName = "Neo4j";

    public string Uri { get; set; } = "bolt://localhost:7687";
    public string Username { get; set; } = "neo4j";
    public string Password { get; set; } = "MaXI2007!";
    public string Database { get; set; } = "neo4j";
}

/// <summary>Everything needed to rebuild the graph after a database round trip.</summary>
public sealed record GeoSnapshot(
    IReadOnlyList<Continent> Continents,
    IReadOnlyList<Country> Countries,
    IReadOnlyList<City> Cities,
    IReadOnlyList<Landmark> Landmarks,
    IReadOnlyList<(string A, string B)> Borders);

public sealed class GeoValidationException(string message) : Exception(message);

/// <summary>Persistence boundary: the Blazor UI never talks Cypher directly.</summary>
public interface IGeoStore
{
    /// <summary>Short label shown in the UI, e.g. "Neo4j · neo4j://localhost:7687".</summary>
    string DisplayName { get; }

    Task<GeoSnapshot> LoadAsync();

    /// <summary>Idempotent MERGE of the curated world atlas.</summary>
    Task SeedAsync();

    /// <summary>Removes only the atlas labels, leaving other graphs in the database alone.</summary>
    Task ClearAsync();
}
