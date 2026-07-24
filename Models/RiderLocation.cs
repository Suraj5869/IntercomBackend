namespace RiderIntercom.Models
{
    public class RiderLocation
    {
        public string ConnectionId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public double? EtaMinutes { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
