using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RiderIntercom.Dtos;
using RiderIntercom.Services;

namespace RiderIntercom.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RoomController : ControllerBase
    {
        private readonly RoomRepository _repo;

        public RoomController(RoomRepository repo)
        {
            _repo = repo;
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create(Guid userId)
        {
            var code = await _repo.CreateRoom(userId);
            return Ok(new { roomCode = code });
        }

        [HttpPost("join")]
        public async Task<IActionResult> Join(JoinRoomDto joinRoom)
        {
            await _repo.JoinRoom(joinRoom.UserId, joinRoom.Code);
            return Ok(new { message = "Joined" });
        }
    }
}
