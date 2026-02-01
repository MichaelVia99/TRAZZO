using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace BitacoraApi.Data
{
    public class BitacoraDbConnectionFactory
    {
        private readonly string _connectionString;

        public BitacoraDbConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("BitacoraDb") ?? string.Empty;
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
