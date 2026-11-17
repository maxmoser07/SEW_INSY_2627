namespace GeoViz.Data;

public enum GeoEdgeKind
{
    InContinent,
    InCountry,
    Nearest,
    Near
}

/// <summary>A laid-out network node. X/Y live in the 1500 x 950 canvas space.</summary>
public sealed class GeoGraphNode
{
    public required string Id { get; init; }
    public required PlaceKind Kind { get; init; }
    public required string Label { get; init; }
    public required string Fill { get; init; }
    public required double X { get; set; }
    public required double Y { get; set; }
    public double Vx { get; set; }
    public double Vy { get; set; }
    public double Mass { get; init; } = 1;
    public double Radius { get; init; } = 8;
    public double BoxWidth { get; init; } = 56;
    public double BoxHeight { get; init; } = 30;
    public string Accent { get; init; } = GeoPalette.Fallback;
    public string ContinentCode { get; init; } = string.Empty;
    public long Population { get; init; }
    public double Lat { get; init; }
    public double Lon { get; init; }
    public bool IsCapital { get; init; }

    public bool IsContinent => Kind == PlaceKind.Continent;
    public bool IsCountry => Kind == PlaceKind.Country;
    public bool IsCity => Kind == PlaceKind.City;
    public bool IsLandmark => Kind == PlaceKind.Landmark;
}

public sealed record GeoGraphEdge(GeoEdgeKind Kind, GeoGraphNode Source, GeoGraphNode Target);

public sealed class GeoGraph
{
    public GeoGraph(IReadOnlyList<GeoGraphNode> nodes, IReadOnlyList<GeoGraphEdge> edges)
    {
        Nodes = nodes;
        Edges = edges;
        ById = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<GeoGraphNode> Nodes { get; }

    public IReadOnlyList<GeoGraphEdge> Edges { get; }

    public IReadOnlyDictionary<string, GeoGraphNode> ById { get; }

    public GeoGraphNode? Find(string? id) =>
        id is not null && ById.TryGetValue(id, out var node) ? node : null;

    public IEnumerable<GeoGraphNode> Neighbours(GeoGraphNode node) =>
        Edges.Where(e => e.Source.Id == node.Id || e.Target.Id == node.Id)
             .Select(e => e.Source.Id == node.Id ? e.Target : e.Source);

    public bool AreNeighbours(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return Edges.Any(e =>
            (e.Source.Id == left && e.Target.Id == right)
            || (e.Source.Id == right && e.Target.Id == left));
    }
}

/// <summary>
/// Force-directed layout tuned for the continent -> country -> city -> landmark
/// hierarchy. Runs once (deterministically, seeded) so the network does not jump
/// between renders; the previous positions can be reused after a reload.
/// </summary>
public static class GraphBuilder
{
    public const double Width = 1500d;
    public const double Height = 950d;
    public const int DefaultSeed = 20261116;

    public static GeoGraph Build(
        GeoRepository repo,
        int seed = DefaultSeed,
        IReadOnlyDictionary<string, (double X, double Y)>? previousPositions = null)
    {
        var rng = new Random(seed);
        var nodes = new List<GeoGraphNode>(repo.Continents.Count + repo.Countries.Count + repo.Cities.Count + repo.Landmarks.Count);
        var byId = new Dictionary<string, GeoGraphNode>(StringComparer.Ordinal);

        // Continents sit on a ring; countries orbit their continent, cities orbit
        // their country and landmarks sit close to their city.
        var continents = repo.Continents
            .OrderBy(c => PositionOf(c.Code))
            .ToList();

        for (var i = 0; i < continents.Count; i++)
        {
            var continent = continents[i];
            var angle = -Math.PI / 2 + i * (Math.PI * 2 / continents.Count);
            var color = GeoPalette.ForContinent(continent.Code);
            var node = new GeoGraphNode
            {
                Id = GeoIds.Continent(continent.Code),
                Kind = PlaceKind.Continent,
                Label = continent.Name,
                Fill = color,
                Accent = color,
                ContinentCode = continent.Code,
                Lat = continent.Lat,
                Lon = continent.Lon,
                Mass = 3.4,
                Radius = 30,
                BoxWidth = 84 + (continent.Name.Length * 2.2),
                BoxHeight = 38,
                X = Width / 2 + Math.Cos(angle) * 310,
                Y = Height / 2 + Math.Sin(angle) * 260
            };

            nodes.Add(node);
            byId[node.Id] = node;
        }

        foreach (var country in repo.Countries)
        {
            if (!byId.TryGetValue(GeoIds.Continent(country.ContinentCode), out var continentNode))
            {
                continue;
            }

            var index = repo.CountriesIn(country.ContinentCode).ToList().IndexOf(country);
            var count = Math.Max(1, repo.CountriesIn(country.ContinentCode).Count);
            var spread = Math.PI * 1.6;
            var angle = -spread / 2 + index * (spread / Math.Max(1, count - 1))
                        + (rng.NextDouble() - 0.5) * 0.25;
            var distance = 150 + rng.NextDouble() * 125;
            var color = GeoPalette.ForContinent(country.ContinentCode);

            var node = new GeoGraphNode
            {
                Id = GeoIds.Country(country.Iso2),
                Kind = PlaceKind.Country,
                Label = country.Iso2,
                Fill = color,
                Accent = color,
                ContinentCode = country.ContinentCode,
                Population = country.Population,
                Lat = country.Lat,
                Lon = country.Lon,
                Mass = 1.9,
                Radius = 15,
                BoxWidth = 46,
                BoxHeight = 26,
                X = continentNode.X + Math.Cos(angle) * distance,
                Y = continentNode.Y + Math.Sin(angle) * distance
            };

            nodes.Add(node);
            byId[node.Id] = node;
        }

        foreach (var city in repo.Cities)
        {
            if (!byId.TryGetValue(GeoIds.Country(city.CountryIso2), out var countryNode))
            {
                continue;
            }

            var angle = rng.NextDouble() * Math.PI * 2;
            var distance = 34 + rng.NextDouble() * 62;
            var radius = 5 + Math.Min(7.5, Math.Sqrt(city.Population) / 1400d);

            var node = new GeoGraphNode
            {
                Id = GeoIds.City(city.Id),
                Kind = PlaceKind.City,
                Label = city.Name,
                Fill = GeoPalette.ForContinent(countryNode.ContinentCode),
                Accent = GeoPalette.ForContinent(countryNode.ContinentCode),
                ContinentCode = countryNode.ContinentCode,
                Population = city.Population,
                Lat = city.Lat,
                Lon = city.Lon,
                IsCapital = city.IsCapital,
                Mass = 1,
                Radius = radius,
                X = countryNode.X + Math.Cos(angle) * distance,
                Y = countryNode.Y + Math.Sin(angle) * distance
            };

            nodes.Add(node);
            byId[node.Id] = node;
        }

        foreach (var landmark in repo.Landmarks)
        {
            if (!byId.TryGetValue(GeoIds.City(landmark.CityId), out var cityNode))
            {
                continue;
            }

            var angle = rng.NextDouble() * Math.PI * 2;
            var distance = 26 + rng.NextDouble() * 30;
            var node = new GeoGraphNode
            {
                Id = GeoIds.Landmark(landmark.Id),
                Kind = PlaceKind.Landmark,
                Label = landmark.Name,
                Fill = GeoPalette.Landmark,
                Accent = GeoPalette.Landmark,
                ContinentCode = cityNode.ContinentCode,
                Lat = landmark.Lat,
                Lon = landmark.Lon,
                Mass = 0.75,
                Radius = 7,
                X = cityNode.X + Math.Cos(angle) * distance,
                Y = cityNode.Y + Math.Sin(angle) * distance
            };

            nodes.Add(node);
            byId[node.Id] = node;
        }

        var edges = new List<GeoGraphEdge>();

        foreach (var country in repo.Countries)
        {
            if (byId.TryGetValue(GeoIds.Country(country.Iso2), out var countryNode)
                && byId.TryGetValue(GeoIds.Continent(country.ContinentCode), out var continentNode))
            {
                edges.Add(new GeoGraphEdge(GeoEdgeKind.InContinent, countryNode, continentNode));
            }
        }

        foreach (var city in repo.Cities)
        {
            if (byId.TryGetValue(GeoIds.City(city.Id), out var cityNode)
                && byId.TryGetValue(GeoIds.Country(city.CountryIso2), out var countryNode))
            {
                edges.Add(new GeoGraphEdge(GeoEdgeKind.InCountry, cityNode, countryNode));
            }
        }

        foreach (var (a, b) in repo.NearestDrawn)
        {
            if (byId.TryGetValue(a, out var left) && byId.TryGetValue(b, out var right))
            {
                edges.Add(new GeoGraphEdge(GeoEdgeKind.Nearest, left, right));
            }
        }

        foreach (var landmark in repo.Landmarks)
        {
            if (byId.TryGetValue(GeoIds.Landmark(landmark.Id), out var landmarkNode)
                && byId.TryGetValue(GeoIds.City(landmark.CityId), out var cityNode))
            {
                edges.Add(new GeoGraphEdge(GeoEdgeKind.Near, landmarkNode, cityNode));
            }
        }

        // Reuse known positions so the graph does not jump after an edit.
        if (previousPositions is not null)
        {
            foreach (var node in nodes)
            {
                if (previousPositions.TryGetValue(node.Id, out var position))
                {
                    node.X = position.X;
                    node.Y = position.Y;
                }
            }
        }

        Simulate(nodes, edges);

        return new GeoGraph(nodes, edges);
    }

    private static void Simulate(List<GeoGraphNode> nodes, List<GeoGraphEdge> edges)
    {
        const double centerX = Width / 2;
        const double centerY = Height / 2;
        var alpha = 1.0;

        for (var step = 0; step < 420; step++)
        {
            // Pair-wise charge + collision.
            for (var i = 0; i < nodes.Count; i++)
            {
                var a = nodes[i];
                for (var j = i + 1; j < nodes.Count; j++)
                {
                    var b = nodes[j];
                    var dx = b.X - a.X;
                    var dy = b.Y - a.Y;
                    var d2 = dx * dx + dy * dy;

                    if (d2 < 1)
                    {
                        dx = 0.5;
                        dy = 0.5;
                        d2 = 0.5;
                    }

                    var d = Math.Sqrt(d2);
                    var ux = dx / d;
                    var uy = dy / d;
                    var force = 15000 / d2;

                    var minDistance = a.Radius + b.Radius
                        + (a.IsLandmark || b.IsLandmark ? 14 : 30);

                    if (d < minDistance)
                    {
                        force += (minDistance - d) * 0.18;
                    }

                    a.Vx -= ux * force / a.Mass;
                    a.Vy -= uy * force / a.Mass;
                    b.Vx += ux * force / b.Mass;
                    b.Vy += uy * force / b.Mass;
                }
            }

            // Springs for every relationship.
            foreach (var edge in edges)
            {
                var s = edge.Source;
                var t = edge.Target;
                var dx = t.X - s.X;
                var dy = t.Y - s.Y;
                var d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 0.01)
                {
                    d = 0.01;
                }

                var (restLength, strength) = edge.Kind switch
                {
                    GeoEdgeKind.InContinent => (245d, 0.008d),
                    GeoEdgeKind.InCountry => (82d, 0.019d),
                    GeoEdgeKind.Nearest => edge.Source.Kind switch
                    {
                        PlaceKind.Continent => (300d, 0.006d),
                        PlaceKind.Country => (235d, 0.011d),
                        _ => (120d, 0.006d)
                    },
                    _ => (44d, 0.021d)
                };

                var force = (d - restLength) * strength;
                var ux = dx / d;
                var uy = dy / d;

                s.Vx += ux * force / s.Mass;
                s.Vy += uy * force / s.Mass;
                t.Vx -= ux * force / t.Mass;
                t.Vy -= uy * force / t.Mass;
            }

            // Centring gravity + integration.
            foreach (var node in nodes)
            {
                node.Vx += (centerX - node.X) * 0.0014;
                node.Vy += (centerY - node.Y) * 0.0014;

                node.X += node.Vx * alpha;
                node.Y += node.Vy * alpha;

                node.Vx *= 0.81;
                node.Vy *= 0.81;

                var marginX = node.BoxWidth / 2 + 30;
                var marginY = node.BoxHeight / 2 + 30;
                node.X = Math.Clamp(node.X, marginX, Width - marginX);
                node.Y = Math.Clamp(node.Y, marginY, Height - marginY);
            }

            alpha *= 0.986;
        }
    }

    private static int PositionOf(string continentCode)
    {
        for (var i = 0; i < GeoPalette.Order.Count; i++)
        {
            if (GeoPalette.Order[i] == continentCode)
            {
                return i;
            }
        }

        return GeoPalette.Order.Count;
    }
}
