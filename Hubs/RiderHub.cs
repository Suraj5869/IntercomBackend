using Microsoft.AspNetCore.SignalR;
using RiderIntercom.Services;
using System.Text.RegularExpressions;

namespace RiderIntercom.Hubs
{
    public class RiderHub:Hub
    {
        private static Dictionary<string, Dictionary<string, (string userId, string userName)>> Rooms = new();
        private readonly UserRepository _userRepository;
        public RiderHub(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }
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

        public async Task SendMessage(string roomCode, string message)
        {
            await Clients.Group(roomCode)
                .SendAsync("ReceiveMessage", message);
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
    }
}
