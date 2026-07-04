using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RiderIntercom.Hubs;
using RiderIntercom.Services;
using System.Security.Claims;

namespace RiderIntercom.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MusicController : ControllerBase
    {
        private readonly ClaudinaryService _cloudinary;
        private readonly PlaylistRepository _playlist;
        private readonly RoomRepository _rooms;
        private readonly IHubContext<RiderHub> _hub;

        public MusicController(
            ClaudinaryService cloudinary,
            PlaylistRepository playlist,
            RoomRepository rooms,
            IHubContext<RiderHub> hub)
        {
            _cloudinary = cloudinary;
            _playlist = playlist;
            _rooms = rooms;
            _hub = hub;
        }

        [Authorize]
        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] Guid roomId, [FromForm] Guid userId, [FromForm] string roomCode)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided");

            if (Path.GetExtension(file.FileName).ToLower() != ".mp3")
                return BadRequest("Only .mp3 files allowed");

            var url = await _cloudinary.UploadAudio(file);
            var song = await _playlist.AddSong(roomId, url, file.FileName, userId);

            await _hub.Clients.Group(roomCode).SendAsync("PlaylistUpdated", song);

            return Ok(song);
        }

        [Authorize]
        [HttpGet("playlist/{roomId}")]
        public async Task<IActionResult> GetPlaylist(Guid roomId)
        {
            var songs = await _playlist.GetByRoom(roomId);
            return Ok(songs);
        }

        // Only the room creator can remove a song from the playlist.
        // Caller identity comes from the JWT claim, not a client-supplied field,
        // so a room member can't just pass someone else's userId to bypass this.
        [Authorize]
        [HttpDelete("playlist/{songId}/room/{roomCode}")]
        public async Task<IActionResult> RemoveSong(Guid songId, string roomCode)
        {
            var callerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(callerIdClaim, out var callerId))
                return Unauthorized();

            var room = await _rooms.GetRoomByCode(roomCode);
            if (room == null)
                return NotFound("Room not found");

            if (room.CreatedBy != callerId)
                return Forbid();

            await _playlist.RemoveSong(songId);
            await _hub.Clients.Group(roomCode).SendAsync("PlaylistSongRemoved", songId);
            return Ok();
        }
    }
}
