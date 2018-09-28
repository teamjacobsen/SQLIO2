using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Threading.Tasks;

namespace SQLIO2.Middlewares
{
    class SqlServerMiddleware : IMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IOptions<SqlServerOptions> _options;
        private readonly ILogger<SqlServerMiddleware> _logger;

        public SqlServerMiddleware(RequestDelegate next, IOptions<SqlServerOptions> options, ILogger<SqlServerMiddleware> logger)
        {
            _next = next;
            _options = options;
            _logger = logger;
        }

        public async Task HandleAsync(Packet packet)
        {
            using (var connection = new SqlConnection(_options.Value.ConnectionString))
            {
                var wasOpen = connection.State == ConnectionState.Open;

                if (!wasOpen)
                {
                    await connection.OpenAsync();
                }

                try
                {
                    using (var cmd = new SqlCommand(_options.Value.StoredProcedureName, connection))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.Add("@LocalEndpoint", SqlDbType.NVarChar, 50).Value = packet.Client.Client.LocalEndPoint.ToString();
                        cmd.Parameters.Add("@RemoteEndpoint", SqlDbType.NVarChar, 50).Value = packet.Client.Client.RemoteEndPoint.ToString();
                        cmd.Parameters.Add("@Request", SqlDbType.VarBinary, 2048).Value = packet;
                        var replyParameter = cmd.Parameters.Add("@Reply", SqlDbType.VarBinary, 2048);
                        replyParameter.Direction = ParameterDirection.Output;

                        cmd.ExecuteNonQuery();

                        var replyValue = (SqlBinary)replyParameter.SqlValue;

                        if (!replyValue.IsNull)
                        {
                            var reply = (byte[])replyParameter.Value;

                            await packet.Client.GetStream().WriteAsync(reply);
                        }
                    }
                }
                catch (SqlException e)
                {
                    _logger.LogError(e, "Could not send packet {Data} to database", packet.Raw);

                    throw;
                }
                finally
                {
                    if (!wasOpen)
                    {
                        connection.Close();
                    }
                }
            }

            await _next(packet);
        }
    }
}
