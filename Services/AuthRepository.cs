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
            var sql = @"INSERT INTO public.Users (Id, Name, Email, PasswordHash, created_at)
                    VALUES (@Id, @Name, @Email, @PasswordHash, NOW())";

            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync(sql, user);
        }

        public async Task<User?> GetByEmail(string email)
        {
            var sql = "SELECT * FROM public.Users WHERE Email = @Email";

            using var conn = _db.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<User>(sql, new { Email = email });
        }

        public async Task CreatePasswordResetToken(Guid userId, string tokenHash, DateTime expiresAt)
        {
            const string sql = @"
                UPDATE public.PasswordResetTokens
                SET Used = TRUE
                WHERE UserId = @UserId AND Used = FALSE;

                INSERT INTO public.PasswordResetTokens
                    (Id, UserId, TokenHash, ExpiresAt, Used, CreatedAt)
                VALUES
                    (@Id, @UserId, @TokenHash, @ExpiresAt, FALSE, NOW());";

            using var conn = _db.CreateConnection();

            await conn.ExecuteAsync(sql, new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt
            });
        }

        public async Task<PasswordResetToken?> GetValidPasswordResetToken(string tokenHash)
        {
            const string sql = @"
                SELECT Id, UserId, TokenHash, ExpiresAt, Used, CreatedAt
                FROM public.PasswordResetTokens
                WHERE TokenHash = @TokenHash
                  AND Used = FALSE
                  AND ExpiresAt > NOW()
                LIMIT 1;";

            using var conn = _db.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<PasswordResetToken>(
                sql,
                new { TokenHash = tokenHash }
            );
        }

        public async Task ResetPassword(Guid resetTokenId, Guid userId, string passwordHash)
        {
            using var conn = _db.CreateConnection();
            conn.Open();
            using var transaction = conn.BeginTransaction();

            try
            {
                const string updatePasswordSql = @"
                    UPDATE public.Users
                    SET PasswordHash = @PasswordHash
                    WHERE Id = @UserId;";

                const string invalidateTokenSql = @"
                    UPDATE public.PasswordResetTokens
                    SET Used = TRUE
                    WHERE Id = @ResetTokenId
                      AND Used = FALSE;";

                await conn.ExecuteAsync(
                    updatePasswordSql,
                    new { PasswordHash = passwordHash, UserId = userId },
                    transaction
                );

                await conn.ExecuteAsync(
                    invalidateTokenSql,
                    new { ResetTokenId = resetTokenId },
                    transaction
                );

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
