using System.Text.Json;
using RiderIntercom.Models;

namespace RiderIntercom.Services
{
    public class MapService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public MapService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<List<MapSearchResult>> SearchAsync(
            string query,
            double? proximityLat = null,
            double? proximityLng = null)
        {
            var client = CreateClient();
            var parameters = new List<string>
            {
                $"q={Uri.EscapeDataString(query)}",
                "autocomplete=true",
                "limit=8",
                "country=in",
                "language=en-IN",
                "worldview=in",
                "types=region,district,place,locality,neighborhood,street,address",
                $"access_token={Uri.EscapeDataString(GetAccessToken())}"
            };

            if (IsValidCoordinate(proximityLat, proximityLng))
            {
                parameters.Add($"proximity={proximityLng!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)},{proximityLat!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }

            using var response = await client.GetAsync(
                $"search/geocode/v6/forward?{string.Join("&", parameters)}");

            await EnsureSuccess(response, "Mapbox search");

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);

            var results = new List<MapSearchResult>();

            if (!document.RootElement.TryGetProperty("features", out var features))
            {
                return results;
            }

            foreach (var feature in features.EnumerateArray())
            {
                if (!feature.TryGetProperty("geometry", out var geometry) ||
                    !geometry.TryGetProperty("coordinates", out var coordinates) ||
                    coordinates.GetArrayLength() < 2)
                {
                    continue;
                }

                var properties = feature.TryGetProperty("properties", out var props)
                    ? props
                    : default;

                var label =
                    GetString(properties, "full_address") ??
                    BuildLabel(properties);

                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                var featureType = GetString(properties, "feature_type") ?? "place";

                results.Add(
                    new MapSearchResult
                    {
                        Label = label,
                        FeatureType = featureType,
                        Lng = coordinates[0].GetDouble(),
                        Lat = coordinates[1].GetDouble()
                    });
            }

            return results;
        }

        public async Task<MapRouteResponse> GetRouteAsync(
            double fromLat,
            double fromLng,
            double toLat,
            double toLng)
        {
            var client = CreateClient();
            var coordinates =
                $"{Invariant(fromLng)},{Invariant(fromLat)};{Invariant(toLng)},{Invariant(toLat)}";

            var url =
                $"directions/v5/mapbox/driving-traffic/{coordinates}" +
                "?overview=full" +
                "&geometries=geojson" +
                "&steps=true" +
                "&annotations=congestion,congestion_numeric,distance,duration" +
                "&language=en-IN" +
                "&depart_at=now" +
                $"&access_token={Uri.EscapeDataString(GetAccessToken())}";

            using var response = await client.GetAsync(url);
            await EnsureSuccess(response, "Mapbox route");

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);
            var root = document.RootElement;

            var code = GetString(root, "code");
            if (!string.Equals(code, "Ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Mapbox route failed: {code ?? "Unknown"}");
            }

            if (!root.TryGetProperty("routes", out var routes) ||
                routes.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("No route found.");
            }

            var route = routes[0];
            var result = new MapRouteResponse
            {
                DurationSeconds = GetDouble(route, "duration"),
                TypicalDurationSeconds = GetDouble(route, "duration_typical"),
                DistanceMeters = GetDouble(route, "distance")
            };

            if (!route.TryGetProperty("geometry", out var geometry) ||
                !geometry.TryGetProperty("coordinates", out var routeCoordinates))
            {
                throw new InvalidOperationException("Mapbox returned no route geometry.");
            }

            foreach (var coordinate in routeCoordinates.EnumerateArray())
            {
                if (coordinate.GetArrayLength() < 2)
                {
                    continue;
                }

                result.Coordinates.Add(
                    new MapCoordinate
                    {
                        Lng = coordinate[0].GetDouble(),
                        Lat = coordinate[1].GetDouble()
                    });
            }

            var legs = route.TryGetProperty("legs", out var routeLegs)
                ? routeLegs
                : default;

            if (legs.ValueKind == JsonValueKind.Array)
            {
                foreach (var leg in legs.EnumerateArray())
                {
                    AddSteps(leg, result);
                    AddTrafficSegments(leg, result);
                }
            }

            result.TrafficDelaySeconds = result.TypicalDurationSeconds > 0
                ? Math.Max(
                    0,
                    result.DurationSeconds - result.TypicalDurationSeconds)
                : 0;

            result.TrafficLevel = GetOverallTrafficLevel(result.TrafficSegments);
            result.TrafficDataAvailable = result.TrafficSegments.Any(
                segment => !string.Equals(segment.Level, "unknown", StringComparison.OrdinalIgnoreCase));

            return result;
        }

        private void AddSteps(JsonElement leg, MapRouteResponse result)
        {
            if (!leg.TryGetProperty("steps", out var steps) ||
                steps.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var step in steps.EnumerateArray())
            {
                if (!step.TryGetProperty("maneuver", out var maneuver) ||
                    !maneuver.TryGetProperty("location", out var location) ||
                    location.GetArrayLength() < 2)
                {
                    continue;
                }

                result.Steps.Add(
                    new MapRouteStep
                    {
                        Distance = GetDouble(step, "distance"),
                        Name = GetString(step, "name") ?? string.Empty,
                        ManeuverType = GetString(maneuver, "type") ?? string.Empty,
                        Modifier = GetString(maneuver, "modifier"),
                        ManeuverLocation = new MapCoordinate
                        {
                            Lng = location[0].GetDouble(),
                            Lat = location[1].GetDouble()
                        }
                    });
            }
        }

        private void AddTrafficSegments(JsonElement leg, MapRouteResponse result)
        {
            if (!leg.TryGetProperty("annotation", out var annotation) ||
                !annotation.TryGetProperty("congestion", out var congestion) ||
                !leg.TryGetProperty("annotation", out var annotationForNumeric))
            {
                return;
            }

            var geometry = leg.TryGetProperty("steps", out _)
                ? result.Coordinates
                : result.Coordinates;

            if (geometry.Count < 2)
            {
                return;
            }

            var numericValues = annotationForNumeric.TryGetProperty(
                "congestion_numeric",
                out var numeric)
                ? numeric
                : default;

            var levels = congestion.EnumerateArray().Select(
                item => item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? "unknown"
                    : "unknown").ToList();

            var numericList = numericValues.ValueKind == JsonValueKind.Array
                ? numericValues.EnumerateArray()
                    .Select(item =>
                        item.ValueKind == JsonValueKind.Number
                            ? (double?)item.GetDouble()
                            : null)
                    .ToList()
                : new List<double?>();

            var segmentCount = Math.Min(levels.Count, geometry.Count - 1);
            if (segmentCount == 0)
            {
                return;
            }

            var startIndex = 0;
            var currentLevel = NormalizeTrafficLevel(levels[0]);
            double? currentNumeric = numericList.Count > 0 ? numericList[0] : null;

            for (var i = 1; i < segmentCount; i++)
            {
                var nextLevel = NormalizeTrafficLevel(levels[i]);
                var nextNumeric = numericList.Count > i ? numericList[i] : null;

                if (nextLevel == currentLevel)
                {
                    currentNumeric = MaxNumeric(currentNumeric, nextNumeric);
                    continue;
                }

                result.TrafficSegments.Add(
                    BuildTrafficSegment(
                        geometry,
                        startIndex,
                        i,
                        currentLevel,
                        currentNumeric));

                startIndex = i;
                currentLevel = nextLevel;
                currentNumeric = nextNumeric;
            }

            result.TrafficSegments.Add(
                BuildTrafficSegment(
                    geometry,
                    startIndex,
                    segmentCount,
                    currentLevel,
                    currentNumeric));
        }

        private static MapTrafficSegment BuildTrafficSegment(
            List<MapCoordinate> geometry,
            int startIndex,
            int endExclusive,
            string level,
            double? numeric)
        {
            var coordinates = new List<MapCoordinate>();

            for (var index = startIndex; index <= endExclusive && index < geometry.Count; index++)
            {
                coordinates.Add(geometry[index]);
            }

            return new MapTrafficSegment
            {
                Coordinates = coordinates,
                Level = level,
                Numeric = numeric
            };
        }

        private static string GetOverallTrafficLevel(
            IEnumerable<MapTrafficSegment> segments)
        {
            var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["unknown"] = 0,
                ["low"] = 1,
                ["moderate"] = 2,
                ["heavy"] = 3,
                ["severe"] = 4
            };

            return segments
                .Select(segment => segment.Level)
                .OrderByDescending(level => rank.TryGetValue(level, out var value) ? value : 0)
                .FirstOrDefault() ?? "unknown";
        }

        private static string NormalizeTrafficLevel(string? level)
        {
            return level?.Trim().ToLowerInvariant() switch
            {
                "low" => "low",
                "moderate" => "moderate",
                "heavy" => "heavy",
                "severe" => "severe",
                _ => "unknown"
            };
        }

        private static double? MaxNumeric(double? first, double? second)
        {
            if (!first.HasValue) return second;
            if (!second.HasValue) return first;
            return Math.Max(first.Value, second.Value);
        }

        private static string? BuildLabel(JsonElement properties)
        {
            var name = GetString(properties, "name_preferred")
                ?? GetString(properties, "name");
            var context = GetString(properties, "place_formatted");

            if (!string.IsNullOrWhiteSpace(name) &&
                !string.IsNullOrWhiteSpace(context))
            {
                return $"{name}, {context}";
            }

            return name ?? context;
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(propertyName, out var value) ||
                value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return value.GetString();
        }

        private static double GetDouble(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) &&
                   value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : 0;
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient("Mapbox");
            client.Timeout = TimeSpan.FromSeconds(15);
            return client;
        }

        private string GetAccessToken()
        {
            var token = _configuration["Mapbox:AccessToken"];

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    "Mapbox:AccessToken is not configured.");
            }

            return token;
        }

        private static bool IsValidCoordinate(double? lat, double? lng)
        {
            return lat.HasValue &&
                   lng.HasValue &&
                   lat.Value >= -90 &&
                   lat.Value <= 90 &&
                   lng.Value >= -180 &&
                   lng.Value <= 180;
        }

        private static string Invariant(double value)
        {
            return value.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }

        private static async Task EnsureSuccess(
            HttpResponseMessage response,
            string operation)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(
                $"{operation} failed with HTTP {(int)response.StatusCode}: {body}");
        }
    }
}
