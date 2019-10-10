using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SQLIO2.ProxyServices
{
    class FanoutHub
    {
        private readonly ILogger<FanoutHub> _logger;
        private readonly ConcurrentDictionary<EndPoint, TcpClient> _clients = new ConcurrentDictionary<EndPoint, TcpClient>();

        public FanoutHub(ILogger<FanoutHub> logger)
        {
            _logger = logger;
        }

        public bool TryRegister(TcpClient client)
        {
            return _clients.TryAdd(client.Client.RemoteEndPoint, client);
        }

        public async Task<int> WriteFanoutAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            var count = 0;

            foreach (var (endpoint, client) in _clients)
            {
                if (await TryWriteAsync(client, data, cancellationToken))
                {
                    count++;
                }
                else
                {
                    _clients.TryRemove(endpoint, out _);
                }
            }

            return count;
        }

        private async Task<bool> TryWriteAsync(TcpClient client, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (!client.Connected)
            {
                return false;
            }

            try
            {
                var stream = client.GetStream();

                _logger.LogInformation("Writing {DataAscii} to {RemoteEndpoint}", Encoding.ASCII.GetString(data.Span).Replace("\r", "\\r").Replace("\n", "\\n"), client.Client.RemoteEndPoint);

                await stream.WriteAsync(data, cancellationToken);
                await stream.FlushAsync(cancellationToken);

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
    }
}
