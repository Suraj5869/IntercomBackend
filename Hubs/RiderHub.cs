using Microsoft.AspNetCore.SignalR;
using RiderIntercom.Models;
using RiderIntercom.Services;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace RiderIntercom.Hubs
{
    public class RiderHub:Hub
    {
        private static Dictionary<string, Dictionary<string, (string userId, string userName)>> Rooms = new();
        private static Dictionary<string, RoomMusicState> MusicStates = new();
        // guards against every listener's <audio> firing "ended" at once and
        // advancing the playlist multiple times for a single song finishing
        private static Dictionary<string, Guid> HandledEndings = new();
        private static readonly object _advanceLock = new();

        private readonly UserRepository _userRepository;
        private readonly PlaylistRepository _playlistRepo;
        private readonly RoomRepository _roomRepo;

        public RiderHub(UserRepository userRepository, PlaylistRepository playlistRepo, RoomRepository roomRepo)
        {
            _userRepository = userRepository;
            _playlistRepo = playlistRepo;
            _roomRepo = roomRepo;
        }

        // The caller's real userId, taken from their JWT claim rather than a
        // client-supplied argument — used to authorize creator-only actions.
        private Guid? CallerUserId =>
            Guid.TryParse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (Guid?)null;

        public async Task JoinRoom(string roomCode, string userId)
        {
            var user = await _userRepository.GetById(Guid.Parse(userId));

            if (!Rooms.ContainsKey(roomCode))
                Rooms[roomCode] = new Dictionary<string, (string, string)>();

            var room = Rooms[roomCode];

            // 🔥 REMOVE OLD CONNECTIONS OF SAME USER
            var existing = room
                .FirstOrDefault(x => x.Value.userId == userId);

            if (!existing.Equals(default(KeyValuePair<string, (string, string)>)))
            {
                room.Remove(existing.Key);
            }

            // ADD NEW CONNECTION
            room[Context.ConnectionId] = (userId, user.Name);

            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

            await Clients.Group(roomCode).SendAsync("UsersInRoom", room.Values.Select(u => new {
                id = u.userId,
                name = u.userName
            }));

            // late-joiners / reconnects get whatever is currently playing so
            // they land in sync instead of starting silent
            if (MusicStates.ContainsKey(roomCode))
            {
                var state = MusicStates[roomCode];
                await Clients.Caller.SendAsync("MusicPlay", new
                {
                    songUrl = state.SongUrl,
                    startTime = state.StartTime.ToString("o")
                });
            }
        }

        public async Task LeaveRoom(string roomCode)
        {
            if (!Rooms.ContainsKey(roomCode)) return;

            var roomUsers = Rooms[roomCode];

            if (roomUsers.ContainsKey(Context.ConnectionId))
            {
                roomUsers.Remove(Context.ConnectionId);

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);

                await Clients.Group(roomCode).SendAsync("UsersInRoom", roomUsers.Values.Select(u => new {
                    id = u.userId,
                    name = u.userName
                }));
            }
        }

        public async Task SendMessage(string roomCode, string message, string userId, string userName)
        {
            var chatMessage = new
            {
                MessageId = Guid.NewGuid().ToString(),
                RoomCode = roomCode,
                SenderId = userId,
                SenderName = userName,
                Message = message,
                SentAt = DateTime.UtcNow
            };

            await Clients.Group(roomCode)
                .SendAsync("ReceiveMessage", chatMessage);
        }

        public async Task SendOffer(string roomCode, string offer)
        {
            await Clients.OthersInGroup(roomCode)
                .SendAsync("ReceiveOffer", offer);
        }

        public async Task SendAnswer(string roomCode, string answer)
        {
            await Clients.OthersInGroup(roomCode)
                .SendAsync("ReceiveAnswer", answer);
        }

        public async Task SendIceCandidate(string roomCode, string candidate)
        {
            await Clients.OthersInGroup(roomCode)
                .SendAsync("ReceiveIceCandidate", candidate);
        }

        public async Task UpdateSpeaking(string roomCode, string userId, bool isSpeaking)
        {
            await Clients.OthersInGroup(roomCode)
                .SendAsync("UserSpeaking", userId, isSpeaking);
        }

        // Anyone in the room can pick a song from the playlist to play.
        public async Task PlayMusic(string roomCode, Guid songId, string songUrl, string songName)
        {
            var startTime = DateTime.UtcNow.AddSeconds(1); // buffer so every client's Play() call lands together

            MusicStates[roomCode] = new RoomMusicState
            {
                SongUrl = songUrl,
                StartTime = startTime
            };

            await Clients.Group(roomCode).SendAsync("MusicPlay", new
            {
                songId,
                songUrl,
                songName,
                startTime = startTime.ToString("o")
            });
        }

        public async Task PauseMusic(string roomCode, double positionSeconds)
        {
            MusicStates.Remove(roomCode);
            await Clients.Group(roomCode).SendAsync("MusicPause", positionSeconds);
        }

        public async Task StopMusic(string roomCode)
        {
            MusicStates.Remove(roomCode);
            await Clients.Group(roomCode).SendAsync("MusicStop");
        }

        // Fired by every client's <audio> "ended" event. Only the first one
        // to arrive for a given songId actually advances the playlist.
        public async Task NotifySongEnded(string roomCode, Guid roomId, Guid songId)
        {
            lock (_advanceLock)
            {
                if (HandledEndings.TryGetValue(roomCode, out var lastHandled) && lastHandled == songId)
                    return; // another client's "ended" event already triggered this advance

                HandledEndings[roomCode] = songId;
            }

            await AdvanceToNext(roomCode, roomId, songId);
        }

        // Manual skip — room-creator only. Caller identity comes from the JWT
        // claim, so a client can't just claim to be the creator.
        public async Task SkipMusic(string roomCode, Guid roomId, Guid currentSongId)
        {
            var callerId = CallerUserId;
            if (callerId == null)
            {
                await Clients.Caller.SendAsync("MusicError", "Not authenticated");
                return;
            }

            var room = await _roomRepo.GetRoomById(roomId);
            if (room == null || room.CreatedBy != callerId)
            {
                await Clients.Caller.SendAsync("MusicError", "Only the room creator can skip songs");
                return;
            }

            await AdvanceToNext(roomCode, roomId, currentSongId);
        }

        private async Task AdvanceToNext(string roomCode, Guid roomId, Guid currentSongId)
        {
            var next = await _playlistRepo.GetNextSong(roomId, currentSongId);
            if (next == null)
            {
                MusicStates.Remove(roomCode);
                await Clients.Group(roomCode).SendAsync("MusicStop");
                return;
            }

            var startTime = DateTime.UtcNow.AddSeconds(1);

            MusicStates[roomCode] = new RoomMusicState
            {
                SongUrl = next.SongUrl,
                StartTime = startTime
            };

            await Clients.Group(roomCode).SendAsync("MusicPlay", new
            {
                songId = next.Id,
                songUrl = next.SongUrl,
                songName = next.SongName,
                startTime = startTime.ToString("o")
            });
        }
    }
}
