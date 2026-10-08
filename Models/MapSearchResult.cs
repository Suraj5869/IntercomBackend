namespace RiderIntercom.Models
{
    public class MapSearchResult
    {
        public string Label { get; set; } = string.Empty;
        public string FeatureType { get; set; } = string.Empty;
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
