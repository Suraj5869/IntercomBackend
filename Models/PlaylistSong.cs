namespace RiderIntercom.Models
{
    public class PlaylistSong
    {
        public Guid Id { get; set; }
        public Guid RoomId { get; set; }
        public string SongUrl { get; set; }
        public string SongName { get; set; }
        public Guid AddedBy { get; set; }
        public DateTime AddedAt { get; set; }
    }
}
