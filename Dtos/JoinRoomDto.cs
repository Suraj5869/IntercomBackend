using System.ComponentModel.DataAnnotations;

namespace RiderIntercom.Dtos
{
    public class JoinRoomDto
    {
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public string Code { get; set; }
    }

    public class CreateRoomRequest
    {
        public Guid UserId { get; set; }
    }
    public class CreateRoomResponse
    {
        public Guid RoomId { get; set; }   
        public string RoomCode { get; set; }
        public Guid CreatedBy { get; set; }
    }
}
