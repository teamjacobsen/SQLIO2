using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SQLIO2.Middlewares
{
    class SqlServerMiddleware : IMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly SqlServerOptions _options;
        private readonly ILogger<SqlServerMiddleware> _logger;

        public SqlServerMiddleware(RequestDelegate next, IOptions<SqlServerOptions> options, ILogger<SqlServerMiddleware> logger)
        {
            _next = next;
            _options = options.Value;
            _logger = logger;

            logger.LogInformation("Using connection string '{ConnectionString}'", _options.ConnectionString);
        }

        public async Task HandleAsync(Packet packet)
        {
            using var connection = new SqlConnection(_options.ConnectionString);
            var wasOpen = connection.State == ConnectionState.Open;

            if (!wasOpen)
            {
                await connection.OpenAsync();
            }

            bool handled;
            try
            {
                _logger.LogInformation("Handling packet for SQL Server");

                if (packet.Xml is object)
                {
                    handled = await HandleXmlAsync(connection, packet);
                }
                else
                {
                    handled = await HandleRawAsync(connection, packet);
                }
            }
            catch (SqlException e)
            {
                _logger.LogError(e, "Could not send packet {Data} to database", packet.ToString());

                throw;
            }
            finally
            {
                if (!wasOpen)
                {
                    connection.Close();
                }
            }

            if (!handled)
            {
                await _next(packet);
            }
        }

        private async Task<bool> HandleXmlAsync(SqlConnection connection, Packet packet)
        {
            var local = GetEndpoint(packet.Client.Client.LocalEndPoint);
            var remote = GetEndpoint(packet.Client.Client.RemoteEndPoint);

            using var cmd = new SqlCommand(_options.XmlStoredProcedureName, connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            using var xmlReader = new XmlNodeReader(packet.Xml);

            cmd.Parameters.Add("@LocalHost", SqlDbType.NVarChar, 256).Value = local.Host;
            cmd.Parameters.Add("@LocalPort", SqlDbType.Int).Value = local.Port;
            cmd.Parameters.Add("@RemoteHost", SqlDbType.NVarChar, 256).Value = remote.Host;
            cmd.Parameters.Add("@RemotePort", SqlDbType.Int).Value = remote.Port;
            cmd.Parameters.Add("@Request", SqlDbType.Xml).Value = new SqlXml(xmlReader);
            var replyParameter = cmd.Parameters.Add("@Reply", SqlDbType.Xml);
            replyParameter.Direction = ParameterDirection.Output;

            cmd.ExecuteNonQuery();

            var replyValue = (SqlXml)replyParameter.SqlValue;

            if (!replyValue.IsNull)
            {
                using var reader = replyValue.CreateReader();
                var msg = new XmlDocument();
                msg.Load(reader);

                _logger.LogInformation("Sending immediate {ReplyXmlLength} char reply", msg.OuterXml.Length);

                var stream = packet.Client.GetStream();

                await stream.WriteAsync(Encoding.UTF8.GetBytes(msg.OuterXml));
                await stream.FlushAsync();

                return true;
            }

            return false;
        }

        private async Task<bool> HandleRawAsync(SqlConnection connection, Packet packet)
        {
            var local = GetEndpoint(packet.Client.Client.LocalEndPoint);
            var remote = GetEndpoint(packet.Client.Client.RemoteEndPoint);

            using var cmd = new SqlCommand(_options.StoredProcedureName, connection)
            {
                CommandType = CommandType.StoredProcedure
            };

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

                _logger.LogInformation("Sending immediate {ReplyLength} byte reply", reply.Length);

                var stream = packet.Client.GetStream();

                await stream.WriteAsync(reply);
                await stream.FlushAsync();

                return true;
            }

            return false;
        }

        private static EndpointDetails GetEndpoint(EndPoint endpoint)
        {
            switch (endpoint)
            {
                case DnsEndPoint dns:
                    return new EndpointDetails(dns.Host, dns.Port);
                case IPEndPoint ip:
                    return new EndpointDetails(ip.Address.ToString(), ip.Port);
                default:
                    return new EndpointDetails(endpoint.ToString(), 0);
            }
        }

        readonly struct EndpointDetails
        {
            public string Host { get; }
            public int Port { get; }

            public EndpointDetails(string host, int port) => (Host, Port) = (host, port);
        }
    }
}
