using Microsoft.AspNetCore.SignalR;
using RiderIntercom.Models;
using RiderIntercom.Services;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace RiderIntercom.Hubs
{
    public class RiderHub : Hub
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

            // connectionId is included so clients can address signaling
            // messages (offer/answer/ICE) at a specific peer instead of
            // broadcasting them to everyone in the room.
            await Clients.Group(roomCode).SendAsync("UsersInRoom", room.Select(u => new {
                connectionId = u.Key,
                id = u.Value.userId,
                name = u.Value.userName
            }));

            // late-joiners / reconnects get whatever is currently playing so
            // they land in sync instead of starting silent. If the room's
            // music is paused, sync the paused state instead of playing —
            // otherwise a refresh would start audio for just that client
            // while everyone else's is correctly silent.
            if (MusicStates.TryGetValue(roomCode, out var state))
            {
                if (state.IsPaused)
                {
                    await Clients.Caller.SendAsync("MusicSyncPaused", new
                    {
                        songId = state.SongId,
                        songUrl = state.SongUrl,
                        songName = state.SongName,
                        position = state.PausedAtSeconds
                    });
                }
                else
                {
                    await Clients.Caller.SendAsync("MusicPlay", new
                    {
                        songId = state.SongId,
                        songUrl = state.SongUrl,
                        songName = state.SongName,
                        startTime = state.StartTime.ToString("o")
                    });
                }
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

                await Clients.Group(roomCode).SendAsync("UsersInRoom", roomUsers.Select(u => new {
                    connectionId = u.Key,
                    id = u.Value.userId,
                    name = u.Value.userName
                }));

                // Tell everyone else this connection is gone so they can
                // tear down the RTCPeerConnection they had for it, instead
                // of leaving a dead/half-negotiated connection lying around.
                await Clients.Group(roomCode).SendAsync("PeerLeft", Context.ConnectionId);
            }
        }

        // Browser tab closes / network drops without an explicit LeaveRoom call.
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            foreach (var kvp in Rooms)
            {
                if (kvp.Value.Remove(Context.ConnectionId))
                {
                    await Clients.Group(kvp.Key).SendAsync("UsersInRoom", kvp.Value.Select(u => new {
                        connectionId = u.Key,
                        id = u.Value.userId,
                        name = u.Value.userName
                    }));
                    await Clients.Group(kvp.Key).SendAsync("PeerLeft", Context.ConnectionId);
                }
            }
            await base.OnDisconnectedAsync(exception);
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

        // All three signaling calls are now targeted at a single peer
        // (targetConnectionId) instead of broadcast to the whole room via
        // OthersInGroup. With more than 2 people in a room, broadcasting
        // meant every client's single RTCPeerConnection received offers/
        // answers meant for other pairs, which is what produced the
        // "Called in wrong state: stable" error. The sender's own
        // connectionId is forwarded so the recipient knows which of its
        // per-peer RTCPeerConnections to route the message to.
        public async Task SendOffer(string roomCode, string targetConnectionId, string offer)
        {
            await Clients.Client(targetConnectionId)
                .SendAsync("ReceiveOffer", Context.ConnectionId, offer);
        }

        public async Task SendAnswer(string roomCode, string targetConnectionId, string answer)
        {
            await Clients.Client(targetConnectionId)
                .SendAsync("ReceiveAnswer", Context.ConnectionId, answer);
        }

        public async Task SendIceCandidate(string roomCode, string targetConnectionId, string candidate)
        {
            await Clients.Client(targetConnectionId)
                .SendAsync("ReceiveIceCandidate", Context.ConnectionId, candidate);
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
                SongId = songId,
                SongName = songName,
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
            // Keep the paused position/song around instead of clearing it,
            // so ResumeMusic (and late joiners) know what to restart.
            if (MusicStates.TryGetValue(roomCode, out var state))
            {
                state.PausedAtSeconds = positionSeconds;
                state.IsPaused = true;
            }
            await Clients.Group(roomCode).SendAsync("MusicPause", positionSeconds);
        }

        // Resumes the song that was paused, re-broadcasting a fresh
        // startTime offset by the paused position so every client's drift
        // correction lands them back in sync.
        public async Task ResumeMusic(string roomCode)
        {
            if (!MusicStates.TryGetValue(roomCode, out var state) || !state.IsPaused)
            {
                await Clients.Caller.SendAsync("MusicError", "Nothing to resume");
                return;
            }

            var startTime = DateTime.UtcNow.AddSeconds(1) - TimeSpan.FromSeconds(state.PausedAtSeconds);
            state.StartTime = startTime;
            state.IsPaused = false;

            await Clients.Group(roomCode).SendAsync("MusicPlay", new
            {
                songId = state.SongId,
                songUrl = state.SongUrl,
                songName = state.SongName,
                startTime = startTime.ToString("o")
            });
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
                SongId = next.Id,
                SongName = next.SongName,
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