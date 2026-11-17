using System.Globalization;
using Microsoft.Extensions.Options;
using Neo4j.Driver;

namespace GeoViz.Data;

/// <summary>
/// Neo4j implementation of <see cref="IGeoStore"/>. Graph shape:
/// (:Continent), (:Country), (:City), (:Landmark) with
/// IN_CONTINENT, IN_COUNTRY, BORDERS and NEAR.
/// </summary>
public sealed class Neo4jGeoStore : IGeoStore, IAsyncDisposable
{
    private readonly IDriver _driver;
    private readonly Neo4jOptions _options;

    public Neo4jGeoStore(IOptions<Neo4jOptions> options)
    {
        _options = options.Value;

        // One driver per application: it is thread-safe and pools connections.
        _driver = GraphDatabase.Driver(
            _options.Uri,
            AuthTokens.Basic(_options.Username, _options.Password));
    }

    public string DisplayName => $"Neo4j · {_options.Uri}";

    private IAsyncSession Session() => _driver.AsyncSession(o => o.WithDatabase(_options.Database));

    // ------------------------------------------------------------------ read

    public async Task<GeoSnapshot> LoadAsync()
    {
        await using var session = Session();

        var continents = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (k:Continent)
                RETURN k.code AS code, k.name AS name, k.lat AS lat, k.lon AS lon
                ORDER BY code
                """);

            return await cursor.ToListAsync(record => new Continent(
                Text(record["code"]),
                Text(record["name"]),
                ToDouble(record["lat"]),
                ToDouble(record["lon"])));
        });

        var countries = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (c:Country)
                OPTIONAL MATCH (c)-[:IN_CONTINENT]->(k:Continent)
                WITH c, coalesce(k.code, c.continent) AS continent
                WHERE continent IS NOT NULL
                RETURN c.iso2 AS iso2,
                       c.name AS name,
                       c.capital AS capital,
                       continent,
                       c.population AS population,
                       c.areaKm2 AS areaKm2,
                       c.lat AS lat,
                       c.lon AS lon
                ORDER BY iso2
                """);

            return await cursor.ToListAsync(record => new Country(
                Text(record["iso2"]),
                Text(record["name"]),
                Text(record["capital"]),
                Text(record["continent"]),
                ToLong(record["population"]),
                ToDouble(record["areaKm2"]),
                ToDouble(record["lat"]),
                ToDouble(record["lon"])));
        });

        var cities = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (t:City)
                OPTIONAL MATCH (t)-[:IN_COUNTRY]->(c:Country)
                WITH t, coalesce(c.iso2, t.country) AS country
                WHERE country IS NOT NULL
                RETURN t.id AS id,
                       t.name AS name,
                       country,
                       t.lat AS lat,
                       t.lon AS lon,
                       t.population AS population,
                       coalesce(t.capital, false) AS capital
                ORDER BY name
                """);

            return await cursor.ToListAsync(record => new City(
                Text(record["id"]),
                Text(record["name"]),
                Text(record["country"]),
                ToDouble(record["lat"]),
                ToDouble(record["lon"]),
                ToLong(record["population"]),
                ToBool(record["capital"])));
        });

        var landmarks = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (l:Landmark)
                OPTIONAL MATCH (l)-[:NEAR]->(t:City)
                WITH l, coalesce(t.id, l.city) AS city
                WHERE city IS NOT NULL
                RETURN l.id AS id,
                       l.name AS name,
                       city,
                       l.lat AS lat,
                       l.lon AS lon,
                       coalesce(l.kind, 'Landmark') AS kind,
                       coalesce(l.description, '') AS description
                ORDER BY name
                """);

            return await cursor.ToListAsync(record => new Landmark(
                Text(record["id"]),
                Text(record["name"]),
                Text(record["city"]),
                ToDouble(record["lat"]),
                ToDouble(record["lon"]),
                Text(record["kind"]),
                Text(record["description"])));
        });

        var borders = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (a:Country)-[:BORDERS]->(b:Country)
                WHERE a.iso2 < b.iso2
                RETURN a.iso2 AS a, b.iso2 AS b
                ORDER BY a, b
                """);

            return await cursor.ToListAsync(record =>
                (Text(record["a"]), Text(record["b"])));
        });

        return new GeoSnapshot(continents, countries, cities, landmarks, borders);
    }

    // ----------------------------------------------------------------- write

    public async Task SeedAsync()
    {
        var errors = GeoSeed.Validate();
        if (errors.Count > 0)
        {
            throw new GeoValidationException(
                "The embedded atlas is inconsistent: " + string.Join(" ", errors));
        }

        await using var session = Session();

        // Schema commands run outside the data transaction.
        await session.ExecuteWriteAsync(async tx =>
        {
            await Consume(tx, "CREATE CONSTRAINT geo_continent_code IF NOT EXISTS FOR (n:Continent) REQUIRE n.code IS UNIQUE", new { });
            await Consume(tx, "CREATE CONSTRAINT geo_country_iso2 IF NOT EXISTS FOR (n:Country) REQUIRE n.iso2 IS UNIQUE", new { });
            await Consume(tx, "CREATE CONSTRAINT geo_city_id IF NOT EXISTS FOR (n:City) REQUIRE n.id IS UNIQUE", new { });
            await Consume(tx, "CREATE CONSTRAINT geo_landmark_id IF NOT EXISTS FOR (n:Landmark) REQUIRE n.id IS UNIQUE", new { });
            await Consume(tx, "CREATE INDEX geo_country_name IF NOT EXISTS FOR (n:Country) ON (n.name)", new { });
            await Consume(tx, "CREATE INDEX geo_city_name IF NOT EXISTS FOR (n:City) ON (n.name)", new { });
            await Consume(tx, "CREATE INDEX geo_landmark_name IF NOT EXISTS FOR (n:Landmark) ON (n.name)", new { });
            return true;
        });

        await session.ExecuteWriteAsync(async tx =>
        {
            // Reset only the four atlas relationship types so the seed stays deterministic.
            await Consume(tx, "MATCH (:Country)-[r:IN_CONTINENT]->(:Continent) DELETE r", new { });
            await Consume(tx, "MATCH (:City)-[r:IN_COUNTRY]->(:Country) DELETE r", new { });
            await Consume(tx, "MATCH (:Country)-[r:BORDERS]->(:Country) DELETE r", new { });
            await Consume(tx, "MATCH (:Landmark)-[r:NEAR]->(:City) DELETE r", new { });

            await Consume(tx,
                """
                UNWIND $rows AS row
                MERGE (n:Continent {code: row.code})
                SET n.name = row.name, n.lat = row.lat, n.lon = row.lon
                """,
                new
                {
                    rows = GeoSeed.Continents.Select(c => new
                    {
                        code = c.Code,
                        name = c.Name,
                        lat = c.Lat,
                        lon = c.Lon
                    }).ToArray()
                });

            await Consume(tx,
                """
                UNWIND $rows AS row
                MERGE (n:Country {iso2: row.iso2})
                SET n.name = row.name,
                    n.capital = row.capital,
                    n.continent = row.continent,
                    n.population = row.population,
                    n.areaKm2 = row.areaKm2,
                    n.lat = row.lat,
                    n.lon = row.lon
                """,
                new
                {
                    rows = GeoSeed.Countries.Select(c => new
                    {
                        iso2 = c.Iso2,
                        name = c.Name,
                        capital = c.Capital,
                        continent = c.ContinentCode,
                        population = c.Population,
                        areaKm2 = c.AreaKm2,
                        lat = c.Lat,
                        lon = c.Lon
                    }).ToArray()
                });

            await Consume(tx,
                """
                UNWIND $rows AS row
                MERGE (n:City {id: row.id})
                SET n.name = row.name,
                    n.country = row.country,
                    n.lat = row.lat,
                    n.lon = row.lon,
                    n.population = row.population,
                    n.capital = row.capital
                """,
                new
                {
                    rows = GeoSeed.Cities.Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        country = c.CountryIso2,
                        lat = c.Lat,
                        lon = c.Lon,
                        population = c.Population,
                        capital = c.IsCapital
                    }).ToArray()
                });

            await Consume(tx,
                """
                UNWIND $rows AS row
                MERGE (n:Landmark {id: row.id})
                SET n.name = row.name,
                    n.city = row.city,
                    n.lat = row.lat,
                    n.lon = row.lon,
                    n.kind = row.kind,
                    n.description = row.description
                """,
                new
                {
                    rows = GeoSeed.Landmarks.Select(l => new
                    {
                        id = l.Id,
                        name = l.Name,
                        city = l.CityId,
                        lat = l.Lat,
                        lon = l.Lon,
                        kind = l.Kind,
                        description = l.Description
                    }).ToArray()
                });

            await Consume(tx,
                """
                UNWIND $rows AS row
                MATCH (c:Country {iso2: row.country}), (k:Continent {code: row.continent})
                MERGE (c)-[:IN_CONTINENT]->(k)
                """,
                new
                {
                    rows = GeoSeed.Countries.Select(c => new
                    {
                        country = c.Iso2,
                        continent = c.ContinentCode
                    }).ToArray()
                });

            await Consume(tx,
                """
                UNWIND $rows AS row
                MATCH (c:City {id: row.city}), (k:Country {iso2: row.country})
                MERGE (c)-[:IN_COUNTRY]->(k)
                """,
                new
                {
                    rows = GeoSeed.Cities.Select(c => new
                    {
                        city = c.Id,
                        country = c.CountryIso2
                    }).ToArray()
                });

            await Consume(tx,
                """
                UNWIND $rows AS row
                MATCH (a:Country {iso2: row.a}), (b:Country {iso2: row.b})
                MERGE (a)-[:BORDERS]->(b)
                """,
                new
                {
                    rows = GeoSeed.Borders.Select(b => new { a = b.A, b = b.B }).ToArray()
                });

            var cityById = GeoSeed.Cities.ToDictionary(c => c.Id, StringComparer.Ordinal);
            await Consume(tx,
                """
                UNWIND $rows AS row
                MATCH (l:Landmark {id: row.landmark}), (c:City {id: row.city})
                MERGE (l)-[r:NEAR]->(c)
                SET r.km = row.km
                """,
                new
                {
                    rows = GeoSeed.Landmarks.Select(l =>
                    {
                        var km = cityById.TryGetValue(l.CityId, out var city)
                            ? GeoMath.DistanceKm(l.Lat, l.Lon, city.Lat, city.Lon)
                            : 0;
                        return new
                        {
                            landmark = l.Id,
                            city = l.CityId,
                            km = Math.Round(km, 1)
                        };
                    }).ToArray()
                });

            return true;
        });
    }

    public async Task ClearAsync()
    {
        await using var session = Session();
        await session.ExecuteWriteAsync(async tx =>
        {
            await Consume(tx,
                "MATCH (n) WHERE n:Continent OR n:Country OR n:City OR n:Landmark DETACH DELETE n",
                new { });
            return true;
        });
    }

    // ---------------------------------------------------------------- helpers

    private static async Task Consume(IAsyncQueryRunner runner, string cypher, object parameters)
    {
        var cursor = await runner.RunAsync(cypher, parameters);
        await cursor.ConsumeAsync();
    }

    private static string Text(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static long ToLong(object? value) => value switch
    {
        null => 0,
        long l => l,
        int i => i,
        double d => (long)d,
        IFormattable formattable => long.TryParse(
            formattable.ToString(null, CultureInfo.InvariantCulture),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed) ? parsed : 0,
        _ => 0
    };

    private static double ToDouble(object? value) => value switch
    {
        null => 0,
        double d => d,
        float f => f,
        long l => l,
        int i => i,
        IFormattable formattable => double.TryParse(
            formattable.ToString(null, CultureInfo.InvariantCulture),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed) ? parsed : 0,
        _ => 0
    };

    private static bool ToBool(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => bool.TryParse(s, out var parsed) && parsed,
        _ => false
    };

    public async ValueTask DisposeAsync() => await _driver.DisposeAsync();
}
