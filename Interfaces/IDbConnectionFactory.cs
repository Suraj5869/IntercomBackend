using System.Data;

namespace RiderIntercom.Interfaces
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();

    }
}
