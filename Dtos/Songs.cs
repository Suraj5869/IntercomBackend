namespace RiderIntercom.Dtos
{
    public class Song
    {
        public Guid Id { get; set; }

        public string SongName { get; set; } = string.Empty;

        public string OriginalFileName { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public string UploadedBy { get; set; } = string.Empty;

        public string RoomCode { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; }
    }
}
