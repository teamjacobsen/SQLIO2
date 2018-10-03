using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SQLIO2
{
    class ProxyService
    {
        private readonly ILogger<ProxyService> _logger;
        private readonly ConcurrentDictionary<EndPoint, TcpClient> _devices = new ConcurrentDictionary<EndPoint, TcpClient>();

        public ProxyService(ILogger<ProxyService> logger)
        {
            _logger = logger;
        }

        public bool TryRegister(TcpClient client)
        {
            return _devices.TryAdd(client.Client.RemoteEndPoint, client);
        }

        public async Task<int> FanoutAsync(Memory<byte> data)
        {
            var count = 0;

            foreach (var (endpoint, client) in _devices)
            {
                if (!client.Connected)
                {
                    LogErrorAndRemoveEndpoint("NotConnected");

                    continue;
                }

                try
                {
                    var stream = client.GetStream();

                    _logger.LogInformation("Writing: {Data}", data);

                    await stream.WriteAsync(data);
                    await stream.FlushAsync();

                    count++;
                }
                catch (IOException)
                {
                    LogErrorAndRemoveEndpoint("IO");
                }
                catch (SocketException)
                {
                    LogErrorAndRemoveEndpoint("Socket");
                }
                catch (ObjectDisposedException)
                {
                    LogErrorAndRemoveEndpoint("Disposed");
                }

                void LogErrorAndRemoveEndpoint(string reasonName)
                {
                    _logger.LogError("Fanout failed to send to {Endpoint}. Reason: {ReasonName}", endpoint, reasonName);

                    _devices.TryRemove(endpoint, out _);
                }
            }

            return count;
        }
    }
}
