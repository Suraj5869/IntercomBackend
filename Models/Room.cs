namespace RiderIntercom.Models
{
    public class Room
    {
        public Guid Id { get; set; }
        public string RoomCode { get; set; }
        public Guid CreatedBy { get; set; }

    }
}
