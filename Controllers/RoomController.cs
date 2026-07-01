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

        [HttpPost("upload")]
        public async Task<IActionResult> UploadSong()
        {
            var file = Request.Form.Files[0];

            var roomId =
                Guid.Parse(Request.Form["roomId"]);

            var uploadedBy =
                Guid.Parse(Request.Form["userId"]);

            if (file == null)
                return BadRequest();

            var extension =
                Path.GetExtension(file.FileName);

            if (extension.ToLower() != ".mp3")
                return BadRequest("Only MP3 allowed");

            if (file.Length > 20 * 1024 * 1024)
                return BadRequest("Max 20MB");

            var roomFolder =
                Path.Combine(
                    "uploads",
                    $"Room_{roomId}");

            if (!Directory.Exists(roomFolder))
                Directory.CreateDirectory(roomFolder);

            var uniqueName =
                $"{Guid.NewGuid()}.mp3";

            var fullPath =
                Path.Combine(roomFolder, uniqueName);

            using var stream =
                new FileStream(
                    fullPath,
                    FileMode.Create);

            await file.CopyToAsync(stream);

            using var con = _db.CreateConnection();

            var sequence =
                await con.ExecuteScalarAsync<int>(
                @"SELECT COALESCE(MAX(sequencenumber),0)
          FROM public.roomsongs
          WHERE roomid=@RoomId",
                new { RoomId = roomId });

            sequence++;

            var songId = Guid.NewGuid();

            await con.ExecuteAsync(
            @"INSERT INTO public.roomsongs
      (
        id,
        roomid,
        songname,
        filepath,
        uploadedby,
        sequencenumber,
        created_at
      )
      VALUES
      (
        @Id,
        @RoomId,
        @SongName,
        @FilePath,
        @UploadedBy,
        @SequenceNo,
        NOW()
      )",
              new
              {
                  Id = songId,
                  RoomId = roomId,
                  SongName = file.FileName,
                  FilePath =
                     $"/uploads/Room_{roomId}/{uniqueName}",
                  UploadedBy = uploadedBy,
                  SequenceNo = sequence
              });

            return Ok();
        }

        [HttpGet("{roomId}")]
        public async Task<IActionResult> GetPlaylist(
    Guid roomId)
        {
            using var con = _db.CreateConnection();

            var songs =
                await con.QueryAsync(
                @"SELECT *
          FROM public.RoomSongs
          WHERE RoomId=@RoomId
          ORDER BY SequenceNumber",
                  new { RoomId = roomId });

            return Ok(songs);
        }
    }
}
