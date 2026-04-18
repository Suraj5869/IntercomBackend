namespace RiderIntercom.Models
{
    public class RoomParticipant
    {
        public Guid Id { get; set; }
        public Guid RoomId { get; set; }
        public Guid UserId { get; set; }
    }
}
