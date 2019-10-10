using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SQLIO2.ProxyServices
{
    class ChatHub
    {
        private readonly ILogger<ChatHub> _logger;

        public TcpClient ChatClient { get; set; }
        public TcpClient RemoteClient { get; set; }

        public ChatHub(ILogger<ChatHub> logger)
        {
            _logger = logger;
        }

        public async Task<bool> TryWriteChatAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            var client = ChatClient;

            if (client is object && !await TryWriteAsync(client, data, cancellationToken))
            {
                lock (this)
                {
                    if (ChatClient == client)
                    {
                        ChatClient = null;
                    }
                }

                return false;
            }

            return true;
        }

        public async Task<bool> TryWriteRemoteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            var client = RemoteClient;

            if (client is object && !await TryWriteAsync(client, data, cancellationToken))
            {
                lock (this)
                {
                    if (RemoteClient == client)
                    {
                        RemoteClient = null;
                    }
                }

                return false;
            }

            return true;
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
