using System.Globalization;
using System.Text;

namespace GeoViz.Data;

// ---------------------------------------------------------------- entities

/// <summary>One of the seven continents. Lat/Lon is the anchor used by the map.</summary>
public sealed record Continent(string Code, string Name, double Lat, double Lon);

/// <summary>A sovereign state. Lat/Lon is an approximate geographic centre.</summary>
public sealed record Country(
    string Iso2,
    string Name,
    string Capital,
    string ContinentCode,
    long Population,
    double AreaKm2,
    double Lat,
    double Lon);

/// <summary>A populated place that belongs to exactly one country.</summary>
public sealed record City(
    string Id,
    string Name,
    string CountryIso2,
    double Lat,
    double Lon,
    long Population,
    bool IsCapital);

/// <summary>A point of interest near one of the seeded cities.</summary>
public sealed record Landmark(
    string Id,
    string Name,
    string CityId,
    double Lat,
    double Lon,
    string Kind,
    string Description);

// ------------------------------------------------------------- identifiers

public enum PlaceKind
{
    Continent,
    Country,
    City,
    Landmark
}

/// <summary>
/// Stable string keys used by the SVG views and by the details panel.
/// Keeping the kind in the prefix means one selection id can address any entity.
/// </summary>
public static class GeoIds
{
    public const string ContinentPrefix = "cont:";
    public const string CountryPrefix = "country:";
    public const string CityPrefix = "city:";
    public const string LandmarkPrefix = "landmark:";

    public static string Continent(string code) => ContinentPrefix + code;

    public static string Country(string iso2) => CountryPrefix + iso2;

    public static string City(string id) => CityPrefix + id;

    public static string Landmark(string id) => LandmarkPrefix + id;

    public static PlaceKind KindOf(string id) => id switch
    {
        _ when id.StartsWith(ContinentPrefix, StringComparison.Ordinal) => PlaceKind.Continent,
        _ when id.StartsWith(CountryPrefix, StringComparison.Ordinal) => PlaceKind.Country,
        _ when id.StartsWith(CityPrefix, StringComparison.Ordinal) => PlaceKind.City,
        _ when id.StartsWith(LandmarkPrefix, StringComparison.Ordinal) => PlaceKind.Landmark,
        _ => throw new ArgumentException($"Unknown place id '{id}'.", nameof(id))
    };

    public static string RawId(string id) => id[(id.IndexOf(':') + 1)..];
}

// ----------------------------------------------------------------- palette

/// <summary>Colour system shared by the map, the network and the table.</summary>
public static class GeoPalette
{
    public const string Fallback = "#94a3b8";
    public const string Landmark = "#fbbf24";

    /// <summary>Display order of the continents in menus and charts.</summary>
    public static readonly IReadOnlyList<string> Order = ["EUR", "ASI", "AFR", "NAM", "SAM", "OCE", "ANT"];

    private static readonly Dictionary<string, string> Colors = new(StringComparer.Ordinal)
    {
        ["EUR"] = "#6ea8fe",
        ["ASI"] = "#ffd166",
        ["AFR"] = "#f97362",
        ["NAM"] = "#a78bfa",
        ["SAM"] = "#2dd4bf",
        ["OCE"] = "#f472b6",
        ["ANT"] = "#94a3b8"
    };

    public static string ForContinent(string? code) =>
        code is not null && Colors.TryGetValue(code, out var color) ? color : Fallback;

    public static string ForKind(PlaceKind kind) =>
        kind == PlaceKind.Landmark ? Landmark : Fallback;

    public static string KindLabel(PlaceKind kind) => kind switch
    {
        PlaceKind.Continent => "Continent",
        PlaceKind.Country => "Country",
        PlaceKind.City => "City",
        _ => "Landmark"
    };
}

// -------------------------------------------------------------- formatting

public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Compact population: 1.41B, 84.5M, 671K.</summary>
    public static string Population(long value)
    {
        if (value >= 1_000_000_000)
        {
            return Trim(value / 1_000_000_000d, 2) + "B";
        }

        if (value >= 1_000_000)
        {
            return Trim(value / 1_000_000d, 1) + "M";
        }

        if (value >= 1_000)
        {
            return (value / 1_000d).ToString("0", Inv) + "K";
        }

        return value.ToString("0", Inv);
    }

    public static string People(long value) => value.ToString("N0", Inv);

    public static string Area(double km2) => km2.ToString("N0", Inv) + " km²";

    public static string Km(double km) =>
        km >= 10 ? km.ToString("N0", Inv) + " km" : km.ToString("0.#", Inv) + " km";

    /// <summary>48.86° N, 2.29° E</summary>
    public static string Coordinates(double lat, double lon)
    {
        var ns = lat >= 0 ? "N" : "S";
        var ew = lon >= 0 ? "E" : "W";
        return $"{Math.Abs(lat).ToString("0.##", Inv)}° {ns}, {Math.Abs(lon).ToString("0.##", Inv)}° {ew}";
    }

    /// <summary>Culture-invariant number formatting for SVG geometry.</summary>
    public static string N(double value) => value.ToString("0.###", Inv);

    private static string Trim(double value, int decimals)
    {
        var text = value.ToString("0." + new string('#', decimals), Inv);
        return text;
    }
}

// ------------------------------------------------------------------ maths

public static class GeoMath
{
    private const double EarthRadiusKm = 6371.0088;

    /// <summary>Great-circle distance in kilometres.</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}

// ------------------------------------------------------------- repository

/// <summary>
/// Immutable snapshot of the database contents with the lookups and aggregates
/// the UI needs. A new instance is created after every load.
/// </summary>
public sealed class GeoRepository
{
    private readonly Dictionary<string, Continent> _continents;
    private readonly Dictionary<string, Country> _countries;
    private readonly Dictionary<string, City> _cities;
    private readonly Dictionary<string, Landmark> _landmarks;
    private readonly Dictionary<string, IReadOnlyList<Country>> _countriesByContinent;
    private readonly Dictionary<string, IReadOnlyList<City>> _citiesByCountry;
    private readonly Dictionary<string, IReadOnlyList<City>> _citiesByContinent;
    private readonly Dictionary<string, IReadOnlyList<Landmark>> _landmarksByCity;
    private readonly Dictionary<string, IReadOnlyList<Landmark>> _landmarksByCountry;
    private readonly Dictionary<string, IReadOnlyList<Country>> _nearestCountries;

    public GeoRepository(GeoSnapshot snapshot)
    {
        Continents = snapshot.Continents;
        Countries = snapshot.Countries;
        Cities = snapshot.Cities;
        Landmarks = snapshot.Landmarks;
        Nearest = snapshot.Nearest;
        NearestDrawn = Dedupe(Nearest);

        _continents = Continents.ToDictionary(c => c.Code, StringComparer.Ordinal);
        _countries = Countries.ToDictionary(c => c.Iso2, StringComparer.Ordinal);
        _cities = Cities.ToDictionary(c => c.Id, StringComparer.Ordinal);
        _landmarks = Landmarks.ToDictionary(l => l.Id, StringComparer.Ordinal);

        _countriesByContinent = Countries
            .GroupBy(c => c.ContinentCode, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Country>)g.OrderBy(c => c.Name, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        _citiesByCountry = Cities
            .GroupBy(c => c.CountryIso2, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<City>)g.OrderByDescending(c => c.Population).ThenBy(c => c.Name, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        _citiesByContinent = _countriesByContinent.Keys
            .ToDictionary(
                code => code,
                code => (IReadOnlyList<City>)CountriesIn(code)
                    .SelectMany(country => CitiesIn(country.Iso2))
                    .OrderByDescending(c => c.Population)
                    .ToList(),
                StringComparer.Ordinal);

        _landmarksByCity = Landmarks
            .GroupBy(l => l.CityId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Landmark>)g.OrderBy(l => l.Name, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        _landmarksByCountry = Landmarks
            .Where(l => _cities.TryGetValue(l.CityId, out var city) && _countries.ContainsKey(city.CountryIso2))
            .GroupBy(l => _cities[l.CityId].CountryIso2, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<Landmark>)g.OrderBy(l => l.Name, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        var nearestMap = new Dictionary<string, List<Country>>(StringComparer.Ordinal);
        foreach (var (a, b) in Nearest)
        {
            if (!a.StartsWith(GeoIds.CountryPrefix, StringComparison.Ordinal)
                || !b.StartsWith(GeoIds.CountryPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (_countries.TryGetValue(GeoIds.RawId(b), out var target))
            {
                AddBorder(nearestMap, GeoIds.RawId(a), target);
            }
        }

        _nearestCountries = nearestMap.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<Country>)pair.Value.OrderBy(c => c.Name, StringComparer.Ordinal).ToList(),
            StringComparer.Ordinal);
    }

    public IReadOnlyList<Continent> Continents { get; }

    public IReadOnlyList<Country> Countries { get; }

    public IReadOnlyList<City> Cities { get; }

    public IReadOnlyList<Landmark> Landmarks { get; }

    /// <summary>
    /// "Points at the nearest" links between places of the same kind, as GeoIds.
    /// (A, B) means B is one of A's closest places; every place has up to three.
    /// </summary>
    public IReadOnlyList<(string A, string B)> Nearest { get; }

    /// <summary>The same links with mutual pairs listed once, so views draw one line each.</summary>
    public IReadOnlyList<(string A, string B)> NearestDrawn { get; }

    public int RelationshipCount =>
        Countries.Count + Cities.Count + Landmarks.Count + Nearest.Count;

    public Continent? FindContinent(string code) => _continents.GetValueOrDefault(code);

    public Country? FindCountry(string iso2) => _countries.GetValueOrDefault(iso2);

    public City? FindCity(string id) => _cities.GetValueOrDefault(id);

    public Landmark? FindLandmark(string id) => _landmarks.GetValueOrDefault(id);

    public Country? CountryOf(City city) => FindCountry(city.CountryIso2);

    public Country? CountryOf(Landmark landmark) =>
        FindCity(landmark.CityId) is { } city ? CountryOf(city) : null;

    public Continent? ContinentOf(Country country) => FindContinent(country.ContinentCode);

    public Continent? ContinentOf(City city) => CountryOf(city) is { } country ? ContinentOf(country) : null;

    public Continent? ContinentOf(Landmark landmark) => CountryOf(landmark) is { } country ? ContinentOf(country) : null;

    public IReadOnlyList<Country> CountriesIn(string continentCode) =>
        _countriesByContinent.GetValueOrDefault(continentCode) ?? [];

    public IReadOnlyList<City> CitiesIn(string countryIso2) =>
        _citiesByCountry.GetValueOrDefault(countryIso2) ?? [];

    public IReadOnlyList<City> CitiesInContinent(string continentCode) =>
        _citiesByContinent.GetValueOrDefault(continentCode) ?? [];

    public IReadOnlyList<Landmark> LandmarksNear(string cityId) =>
        _landmarksByCity.GetValueOrDefault(cityId) ?? [];

    public IReadOnlyList<Landmark> LandmarksIn(string countryIso2) =>
        _landmarksByCountry.GetValueOrDefault(countryIso2) ?? [];

    public IReadOnlyList<Country> NearestCountriesOf(string countryIso2) =>
        _nearestCountries.GetValueOrDefault(countryIso2) ?? [];

    /// <summary>Latitude/longitude of any place addressed by a GeoId, or null if unknown.</summary>
    public (double Lat, double Lon)? Locate(string id)
    {
        try
        {
            var raw = GeoIds.RawId(id);

            switch (GeoIds.KindOf(id))
            {
                case PlaceKind.Continent when FindContinent(raw) is { } continent:
                    return (continent.Lat, continent.Lon);
                case PlaceKind.Country when FindCountry(raw) is { } country:
                    return (country.Lat, country.Lon);
                case PlaceKind.City when FindCity(raw) is { } city:
                    return (city.Lat, city.Lon);
                case PlaceKind.Landmark when FindLandmark(raw) is { } landmark:
                    return (landmark.Lat, landmark.Lon);
            }
        }
        catch (ArgumentException)
        {
            // Unknown id.
        }

        return null;
    }

    public City? CapitalOf(Country country) =>
        CitiesIn(country.Iso2).FirstOrDefault(c => c.IsCapital)
        ?? CitiesIn(country.Iso2).FirstOrDefault(c =>
            string.Equals(c.Name, country.Capital, StringComparison.OrdinalIgnoreCase));

    public City? LargestCityIn(Continent continent) =>
        CitiesInContinent(continent.Code).FirstOrDefault();

    public long PopulationOf(Continent continent) =>
        CountriesIn(continent.Code).Sum(c => c.Population);

    public double AreaOf(Continent continent) =>
        CountriesIn(continent.Code).Sum(c => c.AreaKm2);

    public int CityCountIn(Continent continent) => CitiesInContinent(continent.Code).Count;

    public int LandmarkCountIn(Continent continent) =>
        CountriesIn(continent.Code).Sum(c => LandmarksIn(c.Iso2).Count);

    public double DistanceToCity(Landmark landmark)
    {
        if (FindCity(landmark.CityId) is not { } city)
        {
            return 0;
        }

        return GeoMath.DistanceKm(landmark.Lat, landmark.Lon, city.Lat, city.Lon);
    }

    public string DisplayName(string id)
    {
        try
        {
            return GeoIds.KindOf(id) switch
            {
                PlaceKind.Continent => FindContinent(GeoIds.RawId(id))?.Name ?? id,
                PlaceKind.Country => FindCountry(GeoIds.RawId(id))?.Name ?? id,
                PlaceKind.City => FindCity(GeoIds.RawId(id))?.Name ?? id,
                _ => FindLandmark(GeoIds.RawId(id))?.Name ?? id
            };
        }
        catch (ArgumentException)
        {
            return id;
        }
    }

    /// <summary>Free-text haystack used by the search box.</summary>
    public string SearchText(string id)
    {
        try
        {
            switch (GeoIds.KindOf(id))
            {
                case PlaceKind.Continent when FindContinent(GeoIds.RawId(id)) is { } continent:
                    return $"{continent.Name} {continent.Code}";

                case PlaceKind.Country when FindCountry(GeoIds.RawId(id)) is { } country:
                {
                    var continent = ContinentOf(country);
                    return $"{country.Name} {country.Iso2} {country.Capital} {continent?.Name}";
                }

                case PlaceKind.City when FindCity(GeoIds.RawId(id)) is { } city:
                {
                    var country = CountryOf(city);
                    var continent = country is null ? null : ContinentOf(country);
                    return $"{city.Name} {city.Id} {country?.Name} {country?.Iso2} {continent?.Name}";
                }

                case PlaceKind.Landmark when FindLandmark(GeoIds.RawId(id)) is { } landmark:
                {
                    var city = FindCity(landmark.CityId);
                    var country = city is null ? null : CountryOf(city);
                    return $"{landmark.Name} {landmark.Kind} {landmark.Description} {city?.Name} {country?.Name}";
                }
            }
        }
        catch (ArgumentException)
        {
            // fall through
        }

        return id;
    }

    private static IReadOnlyList<(string A, string B)> Dedupe(IReadOnlyList<(string A, string B)> links)
    {
        var seen = new HashSet<(string, string)>();
        var result = new List<(string A, string B)>();

        foreach (var (a, b) in links)
        {
            var key = string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
            if (seen.Add(key))
            {
                result.Add((a, b));
            }
        }

        return result;
    }

    private static void AddBorder(Dictionary<string, List<Country>> map, string key, Country neighbour)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        if (!list.Contains(neighbour))
        {
            list.Add(neighbour);
        }
    }
}

// -------------------------------------------------------------------- seed

/// <summary>
/// Curated real-world geolocation data: 7 continents, 64 countries, 152 cities,
/// 43 landmarks and real land borders. Coordinates are WGS84 decimal degrees.
/// </summary>
public static class GeoSeed
{
    public static readonly IReadOnlyList<Continent> Continents =
    [
        new("EUR", "Europe", 52.0, 15.0),
        new("ASI", "Asia", 34.0, 95.0),
        new("AFR", "Africa", 2.0, 20.0),
        new("NAM", "North America", 47.0, -102.0),
        new("SAM", "South America", -15.0, -60.0),
        new("OCE", "Oceania", -25.0, 140.0),
        new("ANT", "Antarctica", -80.0, 20.0)
    ];

    private static readonly IReadOnlyList<Country> RawCountries =
    [
        // Europe
        new("DE", "Germany", "Berlin", "EUR", 84_482_000, 357_588, 51.16, 10.45),
        new("FR", "France", "Paris", "EUR", 68_170_000, 551_695, 46.60, 2.21),
        new("ES", "Spain", "Madrid", "EUR", 48_350_000, 505_990, 40.46, -3.75),
        new("IT", "Italy", "Rome", "EUR", 58_870_000, 301_340, 41.87, 12.57),
        new("GB", "United Kingdom", "London", "EUR", 67_600_000, 243_610, 54.00, -2.00),
        new("NL", "Netherlands", "Amsterdam", "EUR", 17_880_000, 41_850, 52.13, 5.29),
        new("CH", "Switzerland", "Bern", "EUR", 8_850_000, 41_285, 46.82, 8.23),
        new("NO", "Norway", "Oslo", "EUR", 5_520_000, 385_207, 60.47, 8.47),
        new("SE", "Sweden", "Stockholm", "EUR", 10_550_000, 450_295, 60.13, 18.64),
        new("PL", "Poland", "Warsaw", "EUR", 37_700_000, 312_696, 51.92, 19.15),
        new("GR", "Greece", "Athens", "EUR", 10_410_000, 131_957, 39.07, 21.82),
        new("PT", "Portugal", "Lisbon", "EUR", 10_470_000, 92_212, 39.40, -8.22),
        new("AT", "Austria", "Vienna", "EUR", 9_100_000, 83_879, 47.52, 14.55),
        new("CZ", "Czechia", "Prague", "EUR", 10_870_000, 78_871, 49.82, 15.47),
        new("RU", "Russia", "Moscow", "EUR", 144_200_000, 17_098_246, 60.00, 90.00),

        // Asia
        new("TR", "Türkiye", "Ankara", "ASI", 85_300_000, 783_562, 39.00, 35.00),
        new("CN", "China", "Beijing", "ASI", 1_410_700_000, 9_596_961, 35.86, 104.19),
        new("JP", "Japan", "Tokyo", "ASI", 124_500_000, 377_975, 36.20, 138.25),
        new("IN", "India", "New Delhi", "ASI", 1_407_600_000, 3_287_263, 22.00, 79.00),
        new("KR", "South Korea", "Seoul", "ASI", 51_700_000, 100_210, 36.50, 127.80),
        new("ID", "Indonesia", "Jakarta", "ASI", 275_500_000, 1_904_569, -2.50, 118.00),
        new("TH", "Thailand", "Bangkok", "ASI", 71_700_000, 513_120, 15.00, 101.00),
        new("VN", "Vietnam", "Hanoi", "ASI", 97_500_000, 331_210, 16.00, 106.30),
        new("SA", "Saudi Arabia", "Riyadh", "ASI", 35_900_000, 2_149_690, 23.89, 45.08),
        new("AE", "United Arab Emirates", "Abu Dhabi", "ASI", 9_900_000, 83_600, 23.42, 53.85),
        new("IR", "Iran", "Tehran", "ASI", 87_900_000, 1_648_195, 32.43, 53.69),
        new("IL", "Israel", "Jerusalem", "ASI", 9_700_000, 22_072, 31.05, 34.85),
        new("SG", "Singapore", "Singapore", "ASI", 5_900_000, 728, 1.35, 103.82),
        new("MY", "Malaysia", "Kuala Lumpur", "ASI", 33_600_000, 330_803, 4.21, 101.98),
        new("PK", "Pakistan", "Islamabad", "ASI", 235_800_000, 881_913, 30.37, 69.35),
        new("NP", "Nepal", "Kathmandu", "ASI", 30_500_000, 147_181, 28.39, 84.12),
        new("KZ", "Kazakhstan", "Astana", "ASI", 19_600_000, 2_724_900, 48.02, 66.92),
        new("MN", "Mongolia", "Ulaanbaatar", "ASI", 3_400_000, 1_564_116, 46.86, 103.85),

        // Africa
        new("EG", "Egypt", "Cairo", "AFR", 111_000_000, 1_010_408, 26.82, 30.80),
        new("NG", "Nigeria", "Abuja", "AFR", 218_500_000, 923_768, 9.08, 8.68),
        new("ZA", "South Africa", "Pretoria", "AFR", 60_400_000, 1_221_037, -30.56, 22.94),
        new("MA", "Morocco", "Rabat", "AFR", 37_500_000, 446_550, 31.79, -7.09),
        new("KE", "Kenya", "Nairobi", "AFR", 55_100_000, 580_367, -0.02, 37.91),
        new("GH", "Ghana", "Accra", "AFR", 33_500_000, 238_533, 7.95, -1.02),
        new("ET", "Ethiopia", "Addis Ababa", "AFR", 123_400_000, 1_104_300, 9.15, 40.49),
        new("TZ", "Tanzania", "Dodoma", "AFR", 65_500_000, 947_303, -6.37, 34.89),
        new("DZ", "Algeria", "Algiers", "AFR", 44_900_000, 2_381_741, 28.03, 1.66),
        new("MG", "Madagascar", "Antananarivo", "AFR", 28_900_000, 587_041, -18.77, 46.87),

        // North America
        new("US", "United States", "Washington, D.C.", "NAM", 335_000_000, 9_833_517, 39.83, -98.58),
        new("CA", "Canada", "Ottawa", "NAM", 39_600_000, 9_984_670, 56.13, -106.35),
        new("MX", "Mexico", "Mexico City", "NAM", 128_500_000, 1_964_375, 23.63, -102.55),
        new("CU", "Cuba", "Havana", "NAM", 11_200_000, 109_884, 21.52, -77.78),
        new("GT", "Guatemala", "Guatemala City", "NAM", 17_600_000, 108_889, 15.78, -90.23),
        new("CR", "Costa Rica", "San José", "NAM", 5_200_000, 51_100, 9.75, -83.75),
        new("PA", "Panama", "Panama City", "NAM", 4_400_000, 75_417, 8.54, -80.78),

        // South America
        new("BR", "Brazil", "Brasília", "SAM", 216_400_000, 8_515_767, -14.24, -51.93),
        new("AR", "Argentina", "Buenos Aires", "SAM", 46_200_000, 2_780_400, -38.42, -63.62),
        new("CL", "Chile", "Santiago", "SAM", 19_600_000, 756_102, -35.68, -71.54),
        new("PE", "Peru", "Lima", "SAM", 34_400_000, 1_285_216, -9.19, -75.02),
        new("CO", "Colombia", "Bogotá", "SAM", 52_100_000, 1_141_748, 4.57, -74.30),
        new("EC", "Ecuador", "Quito", "SAM", 18_200_000, 283_561, -1.83, -78.18),
        new("BO", "Bolivia", "Sucre", "SAM", 12_400_000, 1_098_581, -16.29, -63.59),
        new("VE", "Venezuela", "Caracas", "SAM", 28_800_000, 916_445, 6.42, -66.59),
        new("UY", "Uruguay", "Montevideo", "SAM", 3_400_000, 181_034, -32.52, -55.77),

        // Oceania
        new("AU", "Australia", "Canberra", "OCE", 26_600_000, 7_692_024, -25.27, 133.78),
        new("NZ", "New Zealand", "Wellington", "OCE", 5_200_000, 268_021, -40.90, 174.89),
        new("PG", "Papua New Guinea", "Port Moresby", "OCE", 10_300_000, 462_840, -6.31, 143.96),
        new("FJ", "Fiji", "Suva", "OCE", 930_000, 18_274, -17.71, 178.07),

        // Antarctica
        new("AQ", "Antarctica", "McMurdo Station", "ANT", 1_500, 14_200_000, -82.00, 20.00)
    ];

    private static readonly IReadOnlyList<City> RawCities =
    [
        // Germany
        new("berlin", "Berlin", "DE", 52.5200, 13.4050, 3_769_000, true),
        new("hamburg", "Hamburg", "DE", 53.5511, 9.9937, 1_853_000, false),
        new("munich", "Munich", "DE", 48.1351, 11.5820, 1_512_000, false),
        new("cologne", "Cologne", "DE", 50.9375, 6.9603, 1_083_000, false),

        // France
        new("paris", "Paris", "FR", 48.8566, 2.3522, 2_102_000, true),
        new("marseille", "Marseille", "FR", 43.2965, 5.3698, 873_000, false),
        new("lyon", "Lyon", "FR", 45.7640, 4.8357, 522_000, false),
        new("nice", "Nice", "FR", 43.7102, 7.2620, 342_000, false),

        // Spain
        new("madrid", "Madrid", "ES", 40.4168, -3.7038, 3_305_000, true),
        new("barcelona", "Barcelona", "ES", 41.3874, 2.1686, 1_620_000, false),
        new("seville", "Seville", "ES", 37.3891, -5.9845, 688_000, false),

        // Italy
        new("rome", "Rome", "IT", 41.9028, 12.4964, 2_749_000, true),
        new("milan", "Milan", "IT", 45.4642, 9.1900, 1_371_000, false),
        new("naples", "Naples", "IT", 40.8518, 14.2681, 914_000, false),
        new("venice", "Venice", "IT", 45.4408, 12.3155, 258_000, false),
        new("florence", "Florence", "IT", 43.7696, 11.2558, 383_000, false),

        // United Kingdom
        new("london", "London", "GB", 51.5074, -0.1278, 8_982_000, true),
        new("manchester", "Manchester", "GB", 53.4808, -2.2426, 553_000, false),
        new("edinburgh", "Edinburgh", "GB", 55.9533, -3.1883, 530_000, false),

        // Netherlands
        new("amsterdam", "Amsterdam", "NL", 52.3676, 4.9041, 921_000, true),
        new("rotterdam", "Rotterdam", "NL", 51.9244, 4.4777, 651_000, false),

        // Switzerland
        new("bern", "Bern", "CH", 46.9480, 7.4474, 134_000, true),
        new("zurich", "Zurich", "CH", 47.3769, 8.5417, 421_000, false),
        new("geneva", "Geneva", "CH", 46.2044, 6.1432, 203_000, false),

        // Norway
        new("oslo", "Oslo", "NO", 59.9139, 10.7522, 709_000, true),
        new("bergen", "Bergen", "NO", 60.3913, 5.3221, 285_000, false),

        // Sweden
        new("stockholm", "Stockholm", "SE", 59.3293, 18.0686, 984_000, true),
        new("gothenburg", "Gothenburg", "SE", 57.7089, 11.9746, 583_000, false),

        // Poland
        new("warsaw", "Warsaw", "PL", 52.2297, 21.0122, 1_794_000, true),
        new("krakow", "Kraków", "PL", 50.0647, 19.9450, 779_000, false),

        // Greece
        new("athens", "Athens", "GR", 37.9838, 23.7275, 664_000, true),
        new("thessaloniki", "Thessaloniki", "GR", 40.6401, 22.9444, 319_000, false),

        // Portugal
        new("lisbon", "Lisbon", "PT", 38.7223, -9.1393, 545_000, true),
        new("porto", "Porto", "PT", 41.1579, -8.6291, 231_000, false),

        // Austria
        new("vienna", "Vienna", "AT", 48.2082, 16.3738, 1_982_000, true),
        new("salzburg", "Salzburg", "AT", 47.8095, 13.0550, 156_000, false),

        // Czechia
        new("prague", "Prague", "CZ", 50.0755, 14.4378, 1_309_000, true),

        // Russia
        new("moscow", "Moscow", "RU", 55.7558, 37.6173, 12_506_000, true),
        new("saint-petersburg", "Saint Petersburg", "RU", 59.9343, 30.3351, 5_601_000, false),
        new("novosibirsk", "Novosibirsk", "RU", 55.0084, 82.9357, 1_625_000, false),

        // Türkiye
        new("istanbul", "Istanbul", "TR", 41.0082, 28.9784, 15_460_000, false),
        new("ankara", "Ankara", "TR", 39.9334, 32.8597, 5_700_000, true),
        new("izmir", "Izmir", "TR", 38.4237, 27.1428, 2_994_000, false),

        // China
        new("beijing", "Beijing", "CN", 39.9042, 116.4074, 21_540_000, true),
        new("shanghai", "Shanghai", "CN", 31.2304, 121.4737, 24_870_000, false),
        new("guangzhou", "Guangzhou", "CN", 23.1291, 113.2644, 18_676_000, false),
        new("xian", "Xi'an", "CN", 34.3416, 108.9398, 12_900_000, false),

        // Japan
        new("tokyo", "Tokyo", "JP", 35.6762, 139.6503, 13_960_000, true),
        new("osaka", "Osaka", "JP", 34.6937, 135.5023, 2_750_000, false),
        new("kyoto", "Kyoto", "JP", 35.0116, 135.7681, 1_460_000, false),

        // India
        new("new-delhi", "New Delhi", "IN", 28.6139, 77.2090, 32_900_000, true),
        new("mumbai", "Mumbai", "IN", 19.0760, 72.8777, 20_411_000, false),
        new("bengaluru", "Bengaluru", "IN", 12.9716, 77.5946, 12_765_000, false),
        new("kolkata", "Kolkata", "IN", 22.5726, 88.3639, 14_974_000, false),

        // South Korea
        new("seoul", "Seoul", "KR", 37.5665, 126.9780, 9_400_000, true),
        new("busan", "Busan", "KR", 35.1796, 129.0756, 3_400_000, false),

        // Indonesia
        new("jakarta", "Jakarta", "ID", -6.2088, 106.8456, 10_562_000, true),
        new("surabaya", "Surabaya", "ID", -7.2575, 112.7521, 2_874_000, false),
        new("denpasar", "Denpasar", "ID", -8.6705, 115.2126, 748_000, false),

        // Thailand
        new("bangkok", "Bangkok", "TH", 13.7563, 100.5018, 10_539_000, true),
        new("chiang-mai", "Chiang Mai", "TH", 18.7883, 98.9853, 1_198_000, false),

        // Vietnam
        new("hanoi", "Hanoi", "VN", 21.0285, 105.8542, 8_053_000, true),
        new("ho-chi-minh-city", "Ho Chi Minh City", "VN", 10.8231, 106.6297, 9_003_000, false),

        // Saudi Arabia
        new("riyadh", "Riyadh", "SA", 24.7136, 46.6753, 7_676_000, true),
        new("jeddah", "Jeddah", "SA", 21.4858, 39.1925, 4_697_000, false),
        new("mecca", "Mecca", "SA", 21.3891, 39.8579, 2_000_000, false),

        // United Arab Emirates
        new("dubai", "Dubai", "AE", 25.2048, 55.2708, 3_478_000, false),
        new("abu-dhabi", "Abu Dhabi", "AE", 24.4539, 54.3773, 1_483_000, true),

        // Iran
        new("tehran", "Tehran", "IR", 35.6892, 51.3890, 9_500_000, true),
        new("isfahan", "Isfahan", "IR", 32.6546, 51.6680, 2_200_000, false),

        // Israel
        new("jerusalem", "Jerusalem", "IL", 31.7683, 35.2137, 971_000, true),
        new("tel-aviv", "Tel Aviv", "IL", 32.0853, 34.7818, 460_000, false),

        // Singapore
        new("singapore", "Singapore", "SG", 1.3521, 103.8198, 5_700_000, true),

        // Malaysia
        new("kuala-lumpur", "Kuala Lumpur", "MY", 3.1390, 101.6869, 1_808_000, true),
        new("george-town", "George Town", "MY", 5.4141, 100.3288, 794_000, false),

        // Pakistan
        new("islamabad", "Islamabad", "PK", 33.6844, 73.0479, 1_198_000, true),
        new("karachi", "Karachi", "PK", 24.8607, 67.0011, 16_840_000, false),
        new("lahore", "Lahore", "PK", 31.5204, 74.3587, 13_095_000, false),

        // Nepal
        new("kathmandu", "Kathmandu", "NP", 27.7172, 85.3240, 1_500_000, true),

        // Kazakhstan
        new("astana", "Astana", "KZ", 51.1605, 71.4704, 1_200_000, true),
        new("almaty", "Almaty", "KZ", 43.2220, 76.8512, 2_000_000, false),

        // Mongolia
        new("ulaanbaatar", "Ulaanbaatar", "MN", 47.8864, 106.9057, 1_600_000, true),

        // Egypt
        new("cairo", "Cairo", "EG", 30.0444, 31.2357, 10_100_000, true),
        new("alexandria", "Alexandria", "EG", 31.2001, 29.9187, 5_400_000, false),

        // Nigeria
        new("lagos", "Lagos", "NG", 6.5244, 3.3792, 15_388_000, false),
        new("abuja", "Abuja", "NG", 9.0765, 7.3986, 3_600_000, true),

        // South Africa
        new("johannesburg", "Johannesburg", "ZA", -26.2041, 28.0473, 5_800_000, false),
        new("cape-town", "Cape Town", "ZA", -33.9249, 18.4241, 4_618_000, false),
        new("pretoria", "Pretoria", "ZA", -25.7479, 28.2293, 2_500_000, true),

        // Morocco
        new("casablanca", "Casablanca", "MA", 33.5731, -7.5898, 3_600_000, false),
        new("rabat", "Rabat", "MA", 34.0209, -6.8416, 1_900_000, true),
        new("marrakesh", "Marrakesh", "MA", 31.6295, -7.9811, 1_000_000, false),

        // Kenya
        new("nairobi", "Nairobi", "KE", -1.2921, 36.8219, 4_400_000, true),
        new("mombasa", "Mombasa", "KE", -4.0435, 39.6682, 1_200_000, false),

        // Ghana
        new("accra", "Accra", "GH", 5.6037, -0.1870, 2_600_000, true),

        // Ethiopia
        new("addis-ababa", "Addis Ababa", "ET", 9.0320, 38.7469, 5_200_000, true),

        // Tanzania
        new("dar-es-salaam", "Dar es Salaam", "TZ", -6.7924, 39.2083, 7_400_000, false),
        new("dodoma", "Dodoma", "TZ", -6.1630, 35.7516, 765_000, true),
        new("arusha", "Arusha", "TZ", -3.3869, 36.6830, 600_000, false),

        // Algeria
        new("algiers", "Algiers", "DZ", 36.7538, 3.0588, 2_900_000, true),

        // Madagascar
        new("antananarivo", "Antananarivo", "MG", -18.8792, 47.5079, 1_400_000, true),

        // United States
        new("washington-dc", "Washington, D.C.", "US", 38.9072, -77.0369, 671_000, true),
        new("new-york", "New York", "US", 40.7128, -74.0060, 8_336_000, false),
        new("los-angeles", "Los Angeles", "US", 34.0522, -118.2437, 3_898_000, false),
        new("chicago", "Chicago", "US", 41.8781, -87.6298, 2_697_000, false),
        new("san-francisco", "San Francisco", "US", 37.7749, -122.4194, 874_000, false),
        new("las-vegas", "Las Vegas", "US", 36.1699, -115.1398, 641_000, false),

        // Canada
        new("toronto", "Toronto", "CA", 43.6532, -79.3832, 2_794_000, false),
        new("vancouver", "Vancouver", "CA", 49.2827, -123.1207, 662_000, false),
        new("ottawa", "Ottawa", "CA", 45.4215, -75.6972, 1_017_000, true),

        // Mexico
        new("mexico-city", "Mexico City", "MX", 19.4326, -99.1332, 9_209_000, true),
        new("guadalajara", "Guadalajara", "MX", 20.6597, -103.3496, 1_495_000, false),
        new("cancun", "Cancún", "MX", 21.1619, -86.8515, 888_000, false),

        // Cuba
        new("havana", "Havana", "CU", 23.1136, -82.3666, 2_137_000, true),

        // Guatemala
        new("guatemala-city", "Guatemala City", "GT", 14.6349, -90.5069, 994_000, true),

        // Costa Rica
        new("san-jose", "San José", "CR", 9.9281, -84.0907, 342_000, true),

        // Panama
        new("panama-city", "Panama City", "PA", 8.9824, -79.5199, 880_000, true),

        // Brazil
        new("sao-paulo", "São Paulo", "BR", -23.5505, -46.6333, 12_300_000, false),
        new("rio-de-janeiro", "Rio de Janeiro", "BR", -22.9068, -43.1729, 6_748_000, false),
        new("brasilia", "Brasília", "BR", -15.7939, -47.8828, 3_094_000, true),
        new("manaus", "Manaus", "BR", -3.1190, -60.0217, 2_219_000, false),
        new("foz-do-iguacu", "Foz do Iguaçu", "BR", -25.5478, -54.5882, 258_000, false),

        // Argentina
        new("buenos-aires", "Buenos Aires", "AR", -34.6037, -58.3816, 3_120_000, true),
        new("cordoba", "Córdoba", "AR", -31.4201, -64.1888, 1_600_000, false),
        new("ushuaia", "Ushuaia", "AR", -54.8019, -68.3030, 82_000, false),
        new("el-calafate", "El Calafate", "AR", -50.3379, -72.2648, 21_000, false),

        // Chile
        new("santiago", "Santiago", "CL", -33.4489, -70.6693, 6_310_000, true),
        new("valparaiso", "Valparaíso", "CL", -33.0472, -71.6127, 296_000, false),
        new("punta-arenas", "Punta Arenas", "CL", -53.1638, -70.9171, 123_000, false),
        new("hanga-roa", "Hanga Roa", "CL", -27.1497, -109.4324, 7_750, false),

        // Peru
        new("lima", "Lima", "PE", -12.0464, -77.0428, 10_700_000, true),
        new("cusco", "Cusco", "PE", -13.5319, -71.9675, 447_000, false),

        // Colombia
        new("bogota", "Bogotá", "CO", 4.7110, -74.0721, 11_500_000, true),
        new("medellin", "Medellín", "CO", 6.1543, -75.5740, 2_500_000, false),
        new("cartagena", "Cartagena", "CO", 10.3910, -75.4794, 1_000_000, false),

        // Ecuador
        new("quito", "Quito", "EC", -0.1807, -78.4678, 1_900_000, true),
        new("guayaquil", "Guayaquil", "EC", -2.1700, -79.9224, 2_700_000, false),
        new("puerto-ayora", "Puerto Ayora", "EC", -0.7433, -90.3151, 12_000, false),

        // Bolivia
        new("la-paz", "La Paz", "BO", -16.4897, -68.1193, 816_000, false),
        new("sucre", "Sucre", "BO", -19.0196, -65.2619, 300_000, true),

        // Venezuela
        new("caracas", "Caracas", "VE", 10.4806, -66.9036, 2_900_000, true),

        // Uruguay
        new("montevideo", "Montevideo", "UY", -34.9011, -56.1645, 1_300_000, true),

        // Australia
        new("sydney", "Sydney", "AU", -33.8688, 151.2093, 5_312_000, false),
        new("melbourne", "Melbourne", "AU", -37.8136, 144.9631, 5_207_000, false),
        new("canberra", "Canberra", "AU", -35.2809, 149.1300, 464_000, true),
        new("perth", "Perth", "AU", -31.9505, 115.8605, 2_225_000, false),
        new("alice-springs", "Alice Springs", "AU", -23.6980, 133.8807, 26_500, false),
        new("cairns", "Cairns", "AU", -16.9186, 145.7781, 153_000, false),

        // New Zealand
        new("auckland", "Auckland", "NZ", -36.8485, 174.7633, 1_674_000, false),
        new("wellington", "Wellington", "NZ", -41.2866, 174.7756, 215_000, true),
        new("queenstown", "Queenstown", "NZ", -45.0312, 168.6626, 15_000, false),

        // Papua New Guinea
        new("port-moresby", "Port Moresby", "PG", -9.4438, 147.1803, 402_000, true),

        // Fiji
        new("suva", "Suva", "FJ", -18.1416, 178.4419, 93_000, true),

        // Antarctica
        new("mcmurdo-station", "McMurdo Station", "AQ", -77.8419, 166.6863, 1_000, true),
        new("south-pole-station", "South Pole Station", "AQ", -89.9822, 0.0000, 50, false)
    ];

    private static readonly IReadOnlyList<Landmark> RawLandmarks =
    [
        // Europe
        new("eiffel-tower", "Eiffel Tower", "paris", 48.8584, 2.2945, "Monument",
            "330 m wrought-iron tower built for the 1889 World's Fair."),
        new("colosseum", "Colosseum", "rome", 41.8902, 12.4922, "Monument",
            "Flavian amphitheatre completed in 80 AD, seating over 50,000."),
        new("leaning-tower-of-pisa", "Leaning Tower of Pisa", "florence", 43.7230, 10.3966, "Monument",
            "Freestanding bell tower famous for its four-degree tilt."),
        new("sagrada-familia", "Sagrada Família", "barcelona", 41.4036, 2.1744, "Religious site",
            "Gaudí's unfinished basilica, under construction since 1882."),
        new("brandenburg-gate", "Brandenburg Gate", "berlin", 52.5163, 13.3777, "Monument",
            "Neoclassical gate that once marked the edge of Berlin."),
        new("neuschwanstein-castle", "Neuschwanstein Castle", "munich", 47.5576, 10.7498, "Castle",
            "Nineteenth-century palace in the Bavarian Alps."),
        new("stonehenge", "Stonehenge", "london", 51.1789, -1.8262, "Archaeological site",
            "Neolithic stone circle on Salisbury Plain."),
        new("big-ben", "Big Ben", "london", 51.5007, -0.1246, "Monument",
            "The Elizabeth Tower clock at the Palace of Westminster."),
        new("acropolis-athens", "Acropolis of Athens", "athens", 37.9715, 23.7257, "Archaeological site",
            "Citadel above Athens crowned by the Parthenon."),
        new("matterhorn", "Matterhorn", "geneva", 45.9763, 7.6586, "Mountain",
            "4,478 m pyramidal peak on the Swiss-Italian border."),
        new("red-square", "Red Square", "moscow", 55.7539, 37.6208, "Square",
            "Historic square between the Kremlin and Saint Basil's Cathedral."),
        new("hagia-sophia", "Hagia Sophia", "istanbul", 41.0086, 28.9802, "Religious site",
            "Byzantine cathedral turned mosque, completed in 537 AD."),

        // Asia
        new("great-wall", "Great Wall (Badaling)", "beijing", 40.3598, 116.0200, "Wonder",
            "Best-preserved section of China's 21,000 km fortification."),
        new("forbidden-city", "Forbidden City", "beijing", 39.9163, 116.3972, "Palace",
            "Imperial palace of the Ming and Qing dynasties."),
        new("mount-fuji", "Mount Fuji", "tokyo", 35.3606, 138.7274, "Mountain",
            "Japan's highest peak at 3,776 m."),
        new("taj-mahal", "Taj Mahal", "new-delhi", 27.1751, 78.0421, "Wonder",
            "White-marble mausoleum in Agra, completed in 1653."),
        new("burj-khalifa", "Burj Khalifa", "dubai", 25.1972, 55.2744, "Monument",
            "828 m skyscraper, the tallest building in the world."),
        new("marina-bay-sands", "Marina Bay Sands", "singapore", 1.2834, 103.8607, "Monument",
            "Three-tower hotel with a 340 m rooftop sky park."),
        new("mount-everest", "Mount Everest", "kathmandu", 27.9881, 86.9250, "Mountain",
            "Earth's highest mountain above sea level at 8,849 m."),
        new("borobudur", "Borobudur", "surabaya", -7.6079, 110.2038, "Religious site",
            "Ninth-century Mahayana Buddhist temple in central Java."),
        new("dead-sea", "Dead Sea", "jerusalem", 31.5590, 35.4732, "Natural wonder",
            "Hypersaline lake at the lowest land elevation on Earth."),

        // Africa
        new("pyramids-of-giza", "Great Pyramid of Giza", "cairo", 29.9792, 31.1342, "Wonder",
            "Oldest of the Seven Wonders, built around 2560 BC."),
        new("table-mountain", "Table Mountain", "cape-town", -33.9628, 18.4098, "Mountain",
            "Flat-topped mountain overlooking Cape Town."),
        new("cape-of-good-hope", "Cape of Good Hope", "cape-town", -34.3568, 18.4740, "Natural wonder",
            "Rocky headland at the south-western tip of Africa."),
        new("mount-kilimanjaro", "Mount Kilimanjaro", "arusha", -3.0674, 37.3556, "Mountain",
            "Africa's highest peak at 5,895 m."),

        // North America
        new("statue-of-liberty", "Statue of Liberty", "new-york", 40.6892, -74.0445, "Monument",
            "Copper statue gifted by France, dedicated in 1886."),
        new("grand-canyon", "Grand Canyon", "las-vegas", 36.1069, -112.1129, "Natural wonder",
            "446 km-long canyon carved by the Colorado River."),
        new("golden-gate-bridge", "Golden Gate Bridge", "san-francisco", 37.8199, -122.4783, "Bridge",
            "1.7 km suspension bridge spanning the Golden Gate strait."),
        new("niagara-falls", "Niagara Falls", "toronto", 43.0962, -79.0377, "Natural wonder",
            "Three waterfalls on the border of Canada and the United States."),
        new("chichen-itza", "Chichén Itzá", "cancun", 20.6843, -88.5678, "Archaeological site",
            "Maya city whose El Castillo pyramid dates to the 9th century."),

        // South America
        new("christ-the-redeemer", "Christ the Redeemer", "rio-de-janeiro", -22.9519, -43.2105, "Monument",
            "30 m Art Deco statue on Corcovado mountain."),
        new("machu-picchu", "Machu Picchu", "cusco", -13.1631, -72.5450, "Archaeological site",
            "Inca citadel built around 1450 on a 2,430 m ridge."),
        new("iguazu-falls", "Iguazú Falls", "foz-do-iguacu", -25.6953, -54.4367, "Natural wonder",
            "275 waterfalls on the border of Brazil and Argentina."),
        new("perito-moreno-glacier", "Perito Moreno Glacier", "el-calafate", -50.4967, -73.1377, "Glacier",
            "250 km² glacier in Los Glaciares National Park."),
        new("easter-island", "Easter Island (Rapa Nui)", "hanga-roa", -27.1127, -109.3497, "Archaeological site",
            "Remote island famous for its 900 moai statues."),
        new("galapagos-islands", "Galápagos Islands", "puerto-ayora", -0.9538, -90.9656, "Natural wonder",
            "Volcanic archipelago that inspired Darwin's theory of evolution."),
        new("salar-de-uyuni", "Salar de Uyuni", "sucre", -20.1338, -67.4891, "Natural wonder",
            "10,582 km² salt flat, the largest in the world."),

        // Oceania
        new("uluru", "Uluru", "alice-springs", -25.3444, 131.0369, "Natural wonder",
            "348 m sandstone monolith sacred to the Aṉangu people."),
        new("sydney-opera-house", "Sydney Opera House", "sydney", -33.8568, 151.2153, "Monument",
            "Shell-shaped performing arts centre opened in 1973."),
        new("milford-sound", "Milford Sound", "queenstown", -44.6414, 167.8974, "Natural wonder",
            "Fjord in Fiordland National Park with 1,000 m cliffs."),
        new("great-barrier-reef", "Great Barrier Reef", "cairns", -18.2871, 147.6992, "Natural wonder",
            "2,300 km coral reef system, the largest living structure on Earth."),

        // Antarctica
        new("south-pole", "South Pole", "south-pole-station", -90.0000, 0.0000, "Polar point",
            "Southernmost point on Earth, site of the Amundsen-Scott station."),
        new("dry-valleys", "McMurdo Dry Valleys", "mcmurdo-station", -77.4500, 161.0000, "Natural wonder",
            "Snow-free polar desert in Victoria Land.")
    ];

    // ---------------------------------------------------------------------
    // Links are calculated from the coordinates; nothing below is hand-listed.
    //   * every place points at the closest place one rank higher
    //     (landmark -> city, city -> country, country -> continent)
    //   * every place points at its NearestCount closest places of the same kind
    // The Raw* lists above only carry names, coordinates and facts; the parent
    // code they contain is overwritten here.
    // ---------------------------------------------------------------------

    public const int NearestCount = 3;

    public static readonly IReadOnlyList<Country> Countries = RawCountries
        .Select(country => country with
        {
            ContinentCode = Closest(Continents, country.Lat, country.Lon, c => c.Lat, c => c.Lon).Code
        })
        .ToList();

    public static readonly IReadOnlyList<City> Cities = RawCities
        .Select(city => city with
        {
            CountryIso2 = Closest(RawCountries, city.Lat, city.Lon, c => c.Lat, c => c.Lon).Iso2
        })
        .ToList();

    public static readonly IReadOnlyList<Landmark> Landmarks = RawLandmarks
        .Select(landmark => landmark with
        {
            CityId = Closest(RawCities, landmark.Lat, landmark.Lon, c => c.Lat, c => c.Lon).Id
        })
        .ToList();

    /// <summary>(A, B): B is one of A's closest places of the same kind. Ids are GeoIds.</summary>
    public static readonly IReadOnlyList<(string A, string B)> Nearest =
    [
        .. NearestLinks(Continents, c => GeoIds.Continent(c.Code), c => c.Lat, c => c.Lon),
        .. NearestLinks(Countries, c => GeoIds.Country(c.Iso2), c => c.Lat, c => c.Lon),
        .. NearestLinks(Cities, c => GeoIds.City(c.Id), c => c.Lat, c => c.Lon),
        .. NearestLinks(Landmarks, l => GeoIds.Landmark(l.Id), l => l.Lat, l => l.Lon)
    ];

    private static T Closest<T>(
        IEnumerable<T> candidates, double lat, double lon, Func<T, double> candLat, Func<T, double> candLon) =>
        candidates.MinBy(c => GeoMath.DistanceKm(lat, lon, candLat(c), candLon(c)))!;

    private static IEnumerable<(string A, string B)> NearestLinks<T>(
        IReadOnlyList<T> places, Func<T, string> id, Func<T, double> lat, Func<T, double> lon)
    {
        foreach (var place in places)
        {
            var closest = places
                .Where(other => !object.ReferenceEquals(other, place))
                .OrderBy(other => GeoMath.DistanceKm(lat(place), lon(place), lat(other), lon(other)))
                .Take(NearestCount);

            foreach (var other in closest)
            {
                yield return (id(place), id(other));
            }
        }
    }

    public static GeoSnapshot ToSnapshot() =>
        new(Continents, Countries, Cities, Landmarks, Nearest);

    /// <summary>Sanity checks that run before the seed touches the database.</summary>
    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var csv = new HashSet<string>(StringComparer.Ordinal);
        var countryCodes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var continent in Continents)
        {
            if (!csv.Add(continent.Code))
            {
                errors.Add($"Duplicate continent code {continent.Code}.");
            }
        }

        foreach (var country in Countries)
        {
            if (!countryCodes.Add(country.Iso2))
            {
                errors.Add($"Duplicate country code {country.Iso2}.");
            }

            if (!csv.Contains(country.ContinentCode))
            {
                errors.Add($"Country {country.Iso2} references unknown continent {country.ContinentCode}.");
            }
        }

        var cityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var city in Cities)
        {
            if (!cityIds.Add(city.Id))
            {
                errors.Add($"Duplicate city id {city.Id}.");
            }

            if (!countryCodes.Contains(city.CountryIso2))
            {
                errors.Add($"City {city.Id} references unknown country {city.CountryIso2}.");
            }
        }

        var landmarkIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var landmark in Landmarks)
        {
            if (!landmarkIds.Add(landmark.Id))
            {
                errors.Add($"Duplicate landmark id {landmark.Id}.");
            }

            if (!cityIds.Contains(landmark.CityId))
            {
                errors.Add($"Landmark {landmark.Id} references unknown city {landmark.CityId}.");
            }
        }

        foreach (var continent in Continents)
        {
            if (!Countries.Any(c => c.ContinentCode == continent.Code))
            {
                errors.Add($"Continent {continent.Code} has no countries.");
            }
        }

        return errors;
    }

    /// <summary>Plain Cypher script (also written to seed.cypher by the app).</summary>
    public static string ToCypher()
    {
        var cypher = new StringBuilder();
        cypher.AppendLine("// GeoViz seed · real places across the world");
        cypher.AppendLine("// Generated by `dotnet run -- --export-seed`. Safe to run repeatedly.");
        cypher.AppendLine();

        cypher.AppendLine("CREATE CONSTRAINT geo_continent_code IF NOT EXISTS FOR (n:Continent) REQUIRE n.code IS UNIQUE;");
        cypher.AppendLine("CREATE CONSTRAINT geo_country_iso2 IF NOT EXISTS FOR (n:Country) REQUIRE n.iso2 IS UNIQUE;");
        cypher.AppendLine("CREATE CONSTRAINT geo_city_id IF NOT EXISTS FOR (n:City) REQUIRE n.id IS UNIQUE;");
        cypher.AppendLine("CREATE CONSTRAINT geo_landmark_id IF NOT EXISTS FOR (n:Landmark) REQUIRE n.id IS UNIQUE;");
        cypher.AppendLine("CREATE INDEX geo_country_name IF NOT EXISTS FOR (n:Country) ON (n.name);");
        cypher.AppendLine("CREATE INDEX geo_city_name IF NOT EXISTS FOR (n:City) ON (n.name);");
        cypher.AppendLine("CREATE INDEX geo_landmark_name IF NOT EXISTS FOR (n:Landmark) ON (n.name);");
        cypher.AppendLine();

        foreach (var continent in Continents)
        {
            cypher.AppendLine(
                $"MERGE (n:Continent {{code: '{Esc(continent.Code)}'}}) SET n.name = '{Esc(continent.Name)}', " +
                $"n.lat = {Num(continent.Lat)}, n.lon = {Num(continent.Lon)};");
        }

        cypher.AppendLine();

        foreach (var country in Countries)
        {
            cypher.AppendLine(
                $"MERGE (n:Country {{iso2: '{Esc(country.Iso2)}'}}) SET n.name = '{Esc(country.Name)}', " +
                $"n.capital = '{Esc(country.Capital)}', n.continent = '{Esc(country.ContinentCode)}', " +
                $"n.population = {country.Population}, n.areaKm2 = {Num(country.AreaKm2)}, " +
                $"n.lat = {Num(country.Lat)}, n.lon = {Num(country.Lon)};");
        }

        cypher.AppendLine();

        foreach (var city in Cities)
        {
            cypher.AppendLine(
                $"MERGE (n:City {{id: '{Esc(city.Id)}'}}) SET n.name = '{Esc(city.Name)}', " +
                $"n.country = '{Esc(city.CountryIso2)}', n.lat = {Num(city.Lat)}, n.lon = {Num(city.Lon)}, " +
                $"n.population = {city.Population}, n.capital = {Bool(city.IsCapital)};");
        }

        cypher.AppendLine();

        foreach (var landmark in Landmarks)
        {
            cypher.AppendLine(
                $"MERGE (n:Landmark {{id: '{Esc(landmark.Id)}'}}) SET n.name = '{Esc(landmark.Name)}', " +
                $"n.city = '{Esc(landmark.CityId)}', n.lat = {Num(landmark.Lat)}, n.lon = {Num(landmark.Lon)}, " +
                $"n.kind = '{Esc(landmark.Kind)}', n.description = '{Esc(landmark.Description)}';");
        }

        cypher.AppendLine();
        cypher.AppendLine("// Country -> continent");

        foreach (var country in Countries)
        {
            cypher.AppendLine(
                $"MATCH (c:Country {{iso2: '{Esc(country.Iso2)}'}}), (k:Continent {{code: '{Esc(country.ContinentCode)}'}}) " +
                "MERGE (c)-[:IN_CONTINENT]->(k);");
        }

        cypher.AppendLine();
        cypher.AppendLine("// City -> country");

        foreach (var city in Cities)
        {
            cypher.AppendLine(
                $"MATCH (c:City {{id: '{Esc(city.Id)}'}}), (k:Country {{iso2: '{Esc(city.CountryIso2)}'}}) " +
                "MERGE (c)-[:IN_COUNTRY]->(k);");
        }

        cypher.AppendLine();
        cypher.AppendLine($"// Every place -> its {NearestCount} nearest places of the same kind");

        foreach (var (a, b) in Nearest)
        {
            cypher.AppendLine($"MATCH {CypherNode("a", a)}, {CypherNode("b", b)} MERGE (a)-[:NEAREST]->(b);");
        }

        cypher.AppendLine();
        cypher.AppendLine("// Landmark -> nearest seeded city");

        var cityById = Cities.ToDictionary(c => c.Id, StringComparer.Ordinal);
        foreach (var landmark in Landmarks)
        {
            var distance = cityById.TryGetValue(landmark.CityId, out var city)
                ? GeoMath.DistanceKm(landmark.Lat, landmark.Lon, city.Lat, city.Lon)
                : 0;

            cypher.AppendLine(
                $"MATCH (l:Landmark {{id: '{Esc(landmark.Id)}'}}), (c:City {{id: '{Esc(landmark.CityId)}'}}) " +
                $"MERGE (l)-[:NEAR {{km: {Num(Math.Round(distance, 1))}}}]->(c);");
        }

        return cypher.ToString();
    }

    private static string CypherNode(string alias, string id)
    {
        var raw = GeoIds.RawId(id);
        var (label, key) = GeoIds.KindOf(id) switch
        {
            PlaceKind.Continent => ("Continent", "code"),
            PlaceKind.Country => ("Country", "iso2"),
            PlaceKind.City => ("City", "id"),
            _ => ("Landmark", "id")
        };

        return $"({alias}:{label} {{{key}: '{Esc(raw)}'}})";
    }

    private static string Esc(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Bool(bool value) => value ? "true" : "false";
}
