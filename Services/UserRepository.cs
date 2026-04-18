using Dapper;
using RiderIntercom.Interfaces;
using RiderIntercom.Models;

namespace RiderIntercom.Services
{
    public class UserRepository
    {
        private readonly IDbConnectionFactory _dbConnectionFactory;
        public UserRepository(IDbConnectionFactory dbConnectionFactory) 
        {
            _dbConnectionFactory = dbConnectionFactory;
        }
        public async Task<User> GetById(Guid userId)
        {
            using var connection = _dbConnectionFactory.CreateConnection();

            var query = "SELECT Id, Name FROM public.Users WHERE Id = @Id";

            return await connection.QueryFirstOrDefaultAsync<User>(query, new { Id = userId });
        }
    }
}
