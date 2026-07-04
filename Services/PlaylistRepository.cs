using Dapper;
using RiderIntercom.Interfaces;
using RiderIntercom.Models;

namespace RiderIntercom.Services
{
    public class PlaylistRepository
    {
        private readonly IDbConnectionFactory _db;
        public PlaylistRepository(IDbConnectionFactory db) => _db = db;

        public async Task<PlaylistSong> AddSong(Guid roomId, string songUrl, string songName, Guid addedBy)
        {
            var song = new PlaylistSong
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                SongUrl = songUrl,
                SongName = songName,
                AddedBy = addedBy,
                AddedAt = DateTime.UtcNow
            };

            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync(
                "INSERT INTO public.PlaylistSongs VALUES (@Id, @RoomId, @SongUrl, @SongName, @AddedBy, @AddedAt)",
                song);

            return song;
        }

        public async Task<IEnumerable<PlaylistSong>> GetByRoom(Guid roomId)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryAsync<PlaylistSong>(
                "SELECT * FROM public.PlaylistSongs WHERE RoomId = @RoomId ORDER BY AddedAt ASC",
                new { RoomId = roomId });
        }

        public async Task<PlaylistSong?> GetById(Guid songId)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<PlaylistSong>(
                "SELECT * FROM public.PlaylistSongs WHERE Id = @Id",
                new { Id = songId });
        }

        public async Task RemoveSong(Guid songId)
        {
            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync(
                "DELETE FROM public.PlaylistSongs WHERE Id = @Id",
                new { Id = songId });
        }

        /// <summary>
        /// Returns the next song after currentSongId (by AddedAt order),
        /// wrapping around to the first song in the playlist if currentSongId
        /// was the last one. Returns null if the playlist is empty.
        /// </summary>
        public async Task<PlaylistSong?> GetNextSong(Guid roomId, Guid currentSongId)
        {
            using var conn = _db.CreateConnection();

            var current = await conn.QueryFirstOrDefaultAsync<PlaylistSong>(
                "SELECT * FROM public.PlaylistSongs WHERE Id = @Id",
                new { Id = currentSongId });

            if (current == null)
            {
                // current song was removed / not found — just start from the top
                return await conn.QueryFirstOrDefaultAsync<PlaylistSong>(
                    "SELECT * FROM public.PlaylistSongs WHERE RoomId = @RoomId ORDER BY AddedAt ASC LIMIT 1",
                    new { RoomId = roomId });
            }

            var next = await conn.QueryFirstOrDefaultAsync<PlaylistSong>(
                @"SELECT * FROM public.PlaylistSongs 
                  WHERE RoomId = @RoomId AND AddedAt > @AddedAt 
                  ORDER BY AddedAt ASC LIMIT 1",
                new { RoomId = roomId, current.AddedAt });

            if (next != null) return next;

            // wrap around to the first song in the playlist
            return await conn.QueryFirstOrDefaultAsync<PlaylistSong>(
                "SELECT * FROM public.PlaylistSongs WHERE RoomId = @RoomId ORDER BY AddedAt ASC LIMIT 1",
                new { RoomId = roomId });
        }
    }
}
