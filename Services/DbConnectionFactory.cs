using Microsoft.Data.SqlClient;
using Npgsql;
using RiderIntercom.Interfaces;
using System.Data;

namespace RiderIntercom.Services
{
    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly IConfiguration _config;

        public DbConnectionFactory(IConfiguration config)
        {
            _config = config;
        }

        public IDbConnection CreateConnection()
        {   
            return new NpgsqlConnection(_config.GetConnectionString("PostgresConnection"));
        }
    }
}
