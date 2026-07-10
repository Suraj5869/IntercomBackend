namespace RiderIntercom.Models
{
    public class RoomMusicState
    {
        public Guid SongId { get; set; }
        public string SongName { get; set; }
        public string SongUrl { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsPaused { get; set; }
        public double PausedAtSeconds { get; set; }
    }
}
