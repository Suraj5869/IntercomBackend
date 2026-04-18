using Dapper;
using RiderIntercom.Interfaces;
using RiderIntercom.Models;

namespace RiderIntercom.Services
{
    public class AuthRepository
    {
        private readonly IDbConnectionFactory _db;

        public AuthRepository(IDbConnectionFactory db)
        {
            _db = db;
        }

        public async Task CreateUser(User user)
        {
            var sql = @"INSERT INTO public.Users (Id, Name, Email, PasswordHash)
                    VALUES (@Id, @Name, @Email, @PasswordHash)";

            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync(sql, user);
        }

        public async Task<User?> GetByEmail(string email)
        {
            var sql = "SELECT * FROM public.Users WHERE Email = @Email";

            using var conn = _db.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
        }
    }
}
