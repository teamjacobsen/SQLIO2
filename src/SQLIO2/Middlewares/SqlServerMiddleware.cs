using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Net;
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
            var local = GetEndpoint(packet.Client.Client.LocalEndPoint);
            var remote = GetEndpoint(packet.Client.Client.RemoteEndPoint);

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
                        
                        cmd.Parameters.Add("@LocalHost", SqlDbType.NVarChar, 256).Value = local.Host;
                        cmd.Parameters.Add("@LocalPort", SqlDbType.Int).Value = local.Port;
                        cmd.Parameters.Add("@RemoteHost", SqlDbType.NVarChar, 256).Value = remote.Host;
                        cmd.Parameters.Add("@RemotePort", SqlDbType.Int).Value = remote.Port;
                        cmd.Parameters.Add("@Request", SqlDbType.VarBinary, 2048).Value = packet.Raw;
                        var replyParameter = cmd.Parameters.Add("@Reply", SqlDbType.VarBinary, 2048);
                        replyParameter.Direction = ParameterDirection.Output;

                        cmd.ExecuteNonQuery();

                        var replyValue = (SqlBinary)replyParameter.SqlValue;

                        if (!replyValue.IsNull)
                        {
                            var reply = (byte[])replyParameter.Value;

                            var stream = packet.Client.GetStream();

                            await stream.WriteAsync(reply);
                            await stream.FlushAsync();
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

        private static (string Host, int Port) GetEndpoint(EndPoint endpoint)
        {
            switch (endpoint)
            {
                case DnsEndPoint dns:
                    return (dns.Host, dns.Port);
                case IPEndPoint ip:
                    return (ip.Address.ToString(), ip.Port);
                default:
                    return (endpoint.ToString(), 0);
            }
        }
    }
}
