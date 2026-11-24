// GeoViz seed · Europe only, the 3 biggest cities per country
// Links are calculated in Neo4j from lat/lon, nothing is hand-listed. Safe to run repeatedly.

// 0. start clean (removes any older world data)
MATCH (n) WHERE n:Continent OR n:Country OR n:City OR n:Landmark DETACH DELETE n;

CREATE CONSTRAINT geo_continent_code IF NOT EXISTS FOR (n:Continent) REQUIRE n.code IS UNIQUE;
CREATE CONSTRAINT geo_country_iso2 IF NOT EXISTS FOR (n:Country) REQUIRE n.iso2 IS UNIQUE;
CREATE CONSTRAINT geo_city_id IF NOT EXISTS FOR (n:City) REQUIRE n.id IS UNIQUE;
CREATE CONSTRAINT geo_landmark_id IF NOT EXISTS FOR (n:Landmark) REQUIRE n.id IS UNIQUE;
CREATE INDEX geo_country_name IF NOT EXISTS FOR (n:Country) ON (n.name);
CREATE INDEX geo_city_name IF NOT EXISTS FOR (n:City) ON (n.name);
CREATE INDEX geo_landmark_name IF NOT EXISTS FOR (n:Landmark) ON (n.name);

// 1. places
MERGE (n:Continent {code: 'EUR'}) SET n.name = 'Europe', n.lat = 52, n.lon = 15;
MERGE (n:Country {iso2: 'DE'}) SET n.name = 'Germany', n.capital = 'Berlin', n.continent = 'EUR', n.population = 84482000, n.areaKm2 = 357588, n.lat = 51.16, n.lon = 10.45;
MERGE (n:Country {iso2: 'FR'}) SET n.name = 'France', n.capital = 'Paris', n.continent = 'EUR', n.population = 68170000, n.areaKm2 = 551695, n.lat = 46.6, n.lon = 2.21;
MERGE (n:Country {iso2: 'ES'}) SET n.name = 'Spain', n.capital = 'Madrid', n.continent = 'EUR', n.population = 48350000, n.areaKm2 = 505990, n.lat = 40.46, n.lon = -3.75;
MERGE (n:Country {iso2: 'IT'}) SET n.name = 'Italy', n.capital = 'Rome', n.continent = 'EUR', n.population = 58870000, n.areaKm2 = 301340, n.lat = 41.87, n.lon = 12.57;
MERGE (n:Country {iso2: 'GB'}) SET n.name = 'United Kingdom', n.capital = 'London', n.continent = 'EUR', n.population = 67600000, n.areaKm2 = 243610, n.lat = 54, n.lon = -2;
MERGE (n:Country {iso2: 'NL'}) SET n.name = 'Netherlands', n.capital = 'Amsterdam', n.continent = 'EUR', n.population = 17880000, n.areaKm2 = 41850, n.lat = 52.13, n.lon = 5.29;
MERGE (n:Country {iso2: 'CH'}) SET n.name = 'Switzerland', n.capital = 'Bern', n.continent = 'EUR', n.population = 8850000, n.areaKm2 = 41285, n.lat = 46.82, n.lon = 8.23;
MERGE (n:Country {iso2: 'NO'}) SET n.name = 'Norway', n.capital = 'Oslo', n.continent = 'EUR', n.population = 5520000, n.areaKm2 = 385207, n.lat = 60.47, n.lon = 8.47;
MERGE (n:Country {iso2: 'SE'}) SET n.name = 'Sweden', n.capital = 'Stockholm', n.continent = 'EUR', n.population = 10550000, n.areaKm2 = 450295, n.lat = 60.13, n.lon = 18.64;
MERGE (n:Country {iso2: 'PL'}) SET n.name = 'Poland', n.capital = 'Warsaw', n.continent = 'EUR', n.population = 37700000, n.areaKm2 = 312696, n.lat = 51.92, n.lon = 19.15;
MERGE (n:Country {iso2: 'GR'}) SET n.name = 'Greece', n.capital = 'Athens', n.continent = 'EUR', n.population = 10410000, n.areaKm2 = 131957, n.lat = 39.07, n.lon = 21.82;
MERGE (n:Country {iso2: 'PT'}) SET n.name = 'Portugal', n.capital = 'Lisbon', n.continent = 'EUR', n.population = 10470000, n.areaKm2 = 92212, n.lat = 39.4, n.lon = -8.22;
MERGE (n:Country {iso2: 'AT'}) SET n.name = 'Austria', n.capital = 'Vienna', n.continent = 'EUR', n.population = 9100000, n.areaKm2 = 83879, n.lat = 47.52, n.lon = 14.55;
MERGE (n:Country {iso2: 'CZ'}) SET n.name = 'Czechia', n.capital = 'Prague', n.continent = 'EUR', n.population = 10870000, n.areaKm2 = 78871, n.lat = 49.82, n.lon = 15.47;
MERGE (n:Country {iso2: 'RU'}) SET n.name = 'Russia', n.capital = 'Moscow', n.continent = 'EUR', n.population = 144200000, n.areaKm2 = 17098246, n.lat = 60, n.lon = 90;
MERGE (n:City {id: 'berlin'}) SET n.name = 'Berlin', n.country = 'DE', n.lat = 52.52, n.lon = 13.405, n.population = 3769000, n.capital = true;
MERGE (n:City {id: 'hamburg'}) SET n.name = 'Hamburg', n.country = 'DE', n.lat = 53.551, n.lon = 9.994, n.population = 1853000, n.capital = false;
MERGE (n:City {id: 'munich'}) SET n.name = 'Munich', n.country = 'DE', n.lat = 48.135, n.lon = 11.582, n.population = 1512000, n.capital = false;
MERGE (n:City {id: 'paris'}) SET n.name = 'Paris', n.country = 'FR', n.lat = 48.857, n.lon = 2.352, n.population = 2102000, n.capital = true;
MERGE (n:City {id: 'marseille'}) SET n.name = 'Marseille', n.country = 'FR', n.lat = 43.297, n.lon = 5.37, n.population = 873000, n.capital = false;
MERGE (n:City {id: 'lyon'}) SET n.name = 'Lyon', n.country = 'FR', n.lat = 45.764, n.lon = 4.836, n.population = 522000, n.capital = false;
MERGE (n:City {id: 'madrid'}) SET n.name = 'Madrid', n.country = 'ES', n.lat = 40.417, n.lon = -3.704, n.population = 3305000, n.capital = true;
MERGE (n:City {id: 'barcelona'}) SET n.name = 'Barcelona', n.country = 'ES', n.lat = 41.387, n.lon = 2.169, n.population = 1620000, n.capital = false;
MERGE (n:City {id: 'seville'}) SET n.name = 'Seville', n.country = 'ES', n.lat = 37.389, n.lon = -5.985, n.population = 688000, n.capital = false;
MERGE (n:City {id: 'rome'}) SET n.name = 'Rome', n.country = 'IT', n.lat = 41.903, n.lon = 12.496, n.population = 2749000, n.capital = true;
MERGE (n:City {id: 'milan'}) SET n.name = 'Milan', n.country = 'IT', n.lat = 45.464, n.lon = 9.19, n.population = 1371000, n.capital = false;
MERGE (n:City {id: 'naples'}) SET n.name = 'Naples', n.country = 'IT', n.lat = 40.852, n.lon = 14.268, n.population = 914000, n.capital = false;
MERGE (n:City {id: 'london'}) SET n.name = 'London', n.country = 'GB', n.lat = 51.507, n.lon = -0.128, n.population = 8982000, n.capital = true;
MERGE (n:City {id: 'manchester'}) SET n.name = 'Manchester', n.country = 'GB', n.lat = 53.481, n.lon = -2.243, n.population = 553000, n.capital = false;
MERGE (n:City {id: 'edinburgh'}) SET n.name = 'Edinburgh', n.country = 'GB', n.lat = 55.953, n.lon = -3.188, n.population = 530000, n.capital = false;
MERGE (n:City {id: 'amsterdam'}) SET n.name = 'Amsterdam', n.country = 'NL', n.lat = 52.368, n.lon = 4.904, n.population = 921000, n.capital = true;
MERGE (n:City {id: 'rotterdam'}) SET n.name = 'Rotterdam', n.country = 'NL', n.lat = 51.924, n.lon = 4.478, n.population = 651000, n.capital = false;
MERGE (n:City {id: 'bern'}) SET n.name = 'Bern', n.country = 'CH', n.lat = 46.948, n.lon = 7.447, n.population = 134000, n.capital = true;
MERGE (n:City {id: 'zurich'}) SET n.name = 'Zurich', n.country = 'CH', n.lat = 47.377, n.lon = 8.542, n.population = 421000, n.capital = false;
MERGE (n:City {id: 'geneva'}) SET n.name = 'Geneva', n.country = 'CH', n.lat = 46.204, n.lon = 6.143, n.population = 203000, n.capital = false;
MERGE (n:City {id: 'oslo'}) SET n.name = 'Oslo', n.country = 'NO', n.lat = 59.914, n.lon = 10.752, n.population = 709000, n.capital = true;
MERGE (n:City {id: 'bergen'}) SET n.name = 'Bergen', n.country = 'NO', n.lat = 60.391, n.lon = 5.322, n.population = 285000, n.capital = false;
MERGE (n:City {id: 'stockholm'}) SET n.name = 'Stockholm', n.country = 'SE', n.lat = 59.329, n.lon = 18.069, n.population = 984000, n.capital = true;
MERGE (n:City {id: 'gothenburg'}) SET n.name = 'Gothenburg', n.country = 'SE', n.lat = 57.709, n.lon = 11.975, n.population = 583000, n.capital = false;
MERGE (n:City {id: 'warsaw'}) SET n.name = 'Warsaw', n.country = 'PL', n.lat = 52.23, n.lon = 21.012, n.population = 1794000, n.capital = true;
MERGE (n:City {id: 'krakow'}) SET n.name = 'Kraków', n.country = 'PL', n.lat = 50.065, n.lon = 19.945, n.population = 779000, n.capital = false;
MERGE (n:City {id: 'athens'}) SET n.name = 'Athens', n.country = 'GR', n.lat = 37.984, n.lon = 23.728, n.population = 664000, n.capital = true;
MERGE (n:City {id: 'thessaloniki'}) SET n.name = 'Thessaloniki', n.country = 'GR', n.lat = 40.64, n.lon = 22.944, n.population = 319000, n.capital = false;
MERGE (n:City {id: 'lisbon'}) SET n.name = 'Lisbon', n.country = 'PT', n.lat = 38.722, n.lon = -9.139, n.population = 545000, n.capital = true;
MERGE (n:City {id: 'porto'}) SET n.name = 'Porto', n.country = 'PT', n.lat = 41.158, n.lon = -8.629, n.population = 231000, n.capital = false;
MERGE (n:City {id: 'vienna'}) SET n.name = 'Vienna', n.country = 'AT', n.lat = 48.208, n.lon = 16.374, n.population = 1982000, n.capital = true;
MERGE (n:City {id: 'salzburg'}) SET n.name = 'Salzburg', n.country = 'AT', n.lat = 47.81, n.lon = 13.055, n.population = 156000, n.capital = false;
MERGE (n:City {id: 'prague'}) SET n.name = 'Prague', n.country = 'CZ', n.lat = 50.076, n.lon = 14.438, n.population = 1309000, n.capital = true;
MERGE (n:City {id: 'moscow'}) SET n.name = 'Moscow', n.country = 'RU', n.lat = 55.756, n.lon = 37.617, n.population = 12506000, n.capital = true;
MERGE (n:City {id: 'saint-petersburg'}) SET n.name = 'Saint Petersburg', n.country = 'RU', n.lat = 59.934, n.lon = 30.335, n.population = 5601000, n.capital = false;
MERGE (n:City {id: 'novosibirsk'}) SET n.name = 'Novosibirsk', n.country = 'RU', n.lat = 55.008, n.lon = 82.936, n.population = 1625000, n.capital = false;
MERGE (n:Landmark {id: 'eiffel-tower'}) SET n.name = 'Eiffel Tower', n.city = 'paris', n.lat = 48.858, n.lon = 2.295, n.kind = 'Monument', n.description = '330 m wrought-iron tower built for the 1889 World\'s Fair.';
MERGE (n:Landmark {id: 'colosseum'}) SET n.name = 'Colosseum', n.city = 'rome', n.lat = 41.89, n.lon = 12.492, n.kind = 'Monument', n.description = 'Flavian amphitheatre completed in 80 AD, seating over 50,000.';
MERGE (n:Landmark {id: 'leaning-tower-of-pisa'}) SET n.name = 'Leaning Tower of Pisa', n.city = 'florence', n.lat = 43.723, n.lon = 10.397, n.kind = 'Monument', n.description = 'Freestanding bell tower famous for its four-degree tilt.';
MERGE (n:Landmark {id: 'sagrada-familia'}) SET n.name = 'Sagrada Família', n.city = 'barcelona', n.lat = 41.404, n.lon = 2.174, n.kind = 'Religious site', n.description = 'Gaudí\'s unfinished basilica, under construction since 1882.';
MERGE (n:Landmark {id: 'brandenburg-gate'}) SET n.name = 'Brandenburg Gate', n.city = 'berlin', n.lat = 52.516, n.lon = 13.378, n.kind = 'Monument', n.description = 'Neoclassical gate that once marked the edge of Berlin.';
MERGE (n:Landmark {id: 'neuschwanstein-castle'}) SET n.name = 'Neuschwanstein Castle', n.city = 'munich', n.lat = 47.558, n.lon = 10.75, n.kind = 'Castle', n.description = 'Nineteenth-century palace in the Bavarian Alps.';
MERGE (n:Landmark {id: 'stonehenge'}) SET n.name = 'Stonehenge', n.city = 'london', n.lat = 51.179, n.lon = -1.826, n.kind = 'Archaeological site', n.description = 'Neolithic stone circle on Salisbury Plain.';
MERGE (n:Landmark {id: 'big-ben'}) SET n.name = 'Big Ben', n.city = 'london', n.lat = 51.501, n.lon = -0.125, n.kind = 'Monument', n.description = 'The Elizabeth Tower clock at the Palace of Westminster.';
MERGE (n:Landmark {id: 'acropolis-athens'}) SET n.name = 'Acropolis of Athens', n.city = 'athens', n.lat = 37.972, n.lon = 23.726, n.kind = 'Archaeological site', n.description = 'Citadel above Athens crowned by the Parthenon.';
MERGE (n:Landmark {id: 'matterhorn'}) SET n.name = 'Matterhorn', n.city = 'geneva', n.lat = 45.976, n.lon = 7.659, n.kind = 'Mountain', n.description = '4,478 m pyramidal peak on the Swiss-Italian border.';
MERGE (n:Landmark {id: 'red-square'}) SET n.name = 'Red Square', n.city = 'moscow', n.lat = 55.754, n.lon = 37.621, n.kind = 'Square', n.description = 'Historic square between the Kremlin and Saint Basil\'s Cathedral.';

// 2. each place -> closest place one rank higher
MATCH (k:Country), (c:Continent)
WITH k, c, point.distance(point({latitude: k.lat, longitude: k.lon}), point({latitude: c.lat, longitude: c.lon})) AS d
ORDER BY d
WITH k, head(collect(c)) AS closest
MERGE (k)-[:IN_CONTINENT]->(closest);

MATCH (c:City), (k:Country)
WITH c, k, point.distance(point({latitude: c.lat, longitude: c.lon}), point({latitude: k.lat, longitude: k.lon})) AS d
ORDER BY d
WITH c, head(collect(k)) AS closest
MERGE (c)-[:IN_COUNTRY]->(closest);

MATCH (l:Landmark), (c:City)
WITH l, c, point.distance(point({latitude: l.lat, longitude: l.lon}), point({latitude: c.lat, longitude: c.lon})) AS d
ORDER BY d
WITH l, head(collect({city: c, d: d})) AS best
WITH l, best.city AS city, best.d AS d
MERGE (l)-[:NEAR {km: round(d / 100) / 10.0}]->(city);

// 3. each place -> its 3 nearest places of the same kind (k-nearest-neighbours)
MATCH (a:Country), (b:Country) WHERE a <> b
WITH a, b, point.distance(point({latitude: a.lat, longitude: a.lon}), point({latitude: b.lat, longitude: b.lon})) AS d
ORDER BY d
WITH a, collect(b)[..3] AS closest
UNWIND closest AS b
MERGE (a)-[:NEAREST]->(b);

MATCH (a:City), (b:City) WHERE a <> b
WITH a, b, point.distance(point({latitude: a.lat, longitude: a.lon}), point({latitude: b.lat, longitude: b.lon})) AS d
ORDER BY d
WITH a, collect(b)[..3] AS closest
UNWIND closest AS b
MERGE (a)-[:NEAREST]->(b);

MATCH (a:Landmark), (b:Landmark) WHERE a <> b
WITH a, b, point.distance(point({latitude: a.lat, longitude: a.lon}), point({latitude: b.lat, longitude: b.lon})) AS d
ORDER BY d
WITH a, collect(b)[..3] AS closest
UNWIND closest AS b
MERGE (a)-[:NEAREST]->(b);

// 4. keep the parent properties in sync with the links
MATCH (k:Country)-[:IN_CONTINENT]->(c:Continent) SET k.continent = c.code;
MATCH (c:City)-[:IN_COUNTRY]->(k:Country) SET c.country = k.iso2;
MATCH (l:Landmark)-[:NEAR]->(c:City) SET l.city = c.id;

// 5. show it
MATCH p = ()-[]->() RETURN p;
