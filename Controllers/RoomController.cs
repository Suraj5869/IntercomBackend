using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiderIntercom.Dtos;
using RiderIntercom.Interfaces;
using RiderIntercom.Services;

namespace RiderIntercom.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoomController : ControllerBase
    {
        private readonly RoomRepository _repo;
        private readonly IDbConnectionFactory _db;

        public RoomController(RoomRepository repo, IDbConnectionFactory db)
        {
            _repo = repo;
            _db = db;
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> Create(CreateRoomRequest createRoomRequest)
        {
            var code = await _repo.CreateRoom(createRoomRequest.UserId);
            return Ok(new { roomId = code.RoomId, roomCode = code.RoomCode });
        }

        [Authorize]
        [HttpPost("join")]
        public async Task<IActionResult> Join(JoinRoomDto joinRoom)
        {
            Guid id = await _repo.JoinRoom(joinRoom.UserId, joinRoom.Code);
            return Ok(new { roomId=id, message = "Joined" });
        }
    }
}
