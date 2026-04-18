using Dapper;
using RiderIntercom.Interfaces;
using RiderIntercom.Models;

namespace RiderIntercom.Services
{
    public class RoomRepository
    {
        private readonly IDbConnectionFactory _db;

        public RoomRepository(IDbConnectionFactory db)
        {
            _db = db;
        }

        public async Task<string> CreateRoom(Guid userId)
        {
            var roomCode = Guid.NewGuid().ToString().Substring(0, 6);

            var room = new Room
            {
                Id = Guid.NewGuid(),
                RoomCode = roomCode,
                CreatedBy = userId
            };

            using var conn = _db.CreateConnection();

            await conn.ExecuteAsync(
                "INSERT INTO public.Rooms VALUES (@Id, @RoomCode, @CreatedBy, NOW())",
                room);

            return roomCode;
        }

        public async Task JoinRoom(Guid userId, string roomCode)
        {
            using var conn = _db.CreateConnection();

            var room = await conn.QueryFirstOrDefaultAsync<Room>(
                "SELECT * FROM public.Rooms WHERE RoomCode = @RoomCode",
                new { RoomCode = roomCode });

            await conn.ExecuteAsync(
                "INSERT INTO public.RoomParticipants VALUES (@Id, @RoomId, @UserId)",
                new
                {
                    Id = Guid.NewGuid(),
                    RoomId = room.Id,
                    UserId = userId
                });
        }
    }
}
