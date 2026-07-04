using Dapper;
using RiderIntercom.Dtos;
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

        public async Task<CreateRoomResponse> CreateRoom(Guid userId)
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

            return new CreateRoomResponse
            {
                RoomId = room.Id,
                RoomCode = room.RoomCode
            };
        }

        public async Task<Guid> JoinRoom(Guid userId, string roomCode)
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

            return room.Id;
        }

        public async Task<Room?> GetRoomByCode(string roomCode)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Room>(
                "SELECT * FROM public.Rooms WHERE RoomCode = @RoomCode",
                new { RoomCode = roomCode });
        }

        public async Task<Room?> GetRoomById(Guid roomId)
        {
            using var conn = _db.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Room>(
                "SELECT * FROM public.Rooms WHERE Id = @Id",
                new { Id = roomId });
        }
    }
}
