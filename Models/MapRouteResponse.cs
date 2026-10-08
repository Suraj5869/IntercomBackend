namespace RiderIntercom.Models
{
    public class MapRouteResponse
    {
        public List<MapCoordinate> Coordinates { get; set; } = new();
        public List<MapTrafficSegment> TrafficSegments { get; set; } = new();
        public List<MapRouteStep> Steps { get; set; } = new();
        public double DurationSeconds { get; set; }
        public double TypicalDurationSeconds { get; set; }
        public double TrafficDelaySeconds { get; set; }
        public double DistanceMeters { get; set; }
        public string TrafficLevel { get; set; } = "unknown";
        public bool TrafficDataAvailable { get; set; }
    }

    public class MapCoordinate
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }

    public class MapTrafficSegment
    {
        public List<MapCoordinate> Coordinates { get; set; } = new();
        public string Level { get; set; } = "unknown";
        public double? Numeric { get; set; }
    }

    public class MapRouteStep
    {
        public double Distance { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ManeuverType { get; set; } = string.Empty;
        public string? Modifier { get; set; }
        public MapCoordinate ManeuverLocation { get; set; } = new();
    }
}
