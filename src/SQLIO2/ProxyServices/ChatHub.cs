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

        /// <summary>
        /// The current chat client. The hub does not own the client, and it may even be disposed when we use it.
        /// </summary>
        public TcpClient ChatClient { get; set; }

        /// <summary>
        /// The current remote client. The hub does not own the client, and it may even be disposed when we use it.
        /// </summary>
        public TcpClient RemoteClient { get; set; }

        public ChatHub(ILogger<ChatHub> logger)
        {
            _logger = logger;
        }

        public bool TryRemoveChatClient(TcpClient client)
        {
            lock (this)
            {
                if (ChatClient == client)
                {
                    ChatClient = null;
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> TryWriteChatAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            var client = ChatClient;

            if (client is null)
            {
                _logger.LogWarning("There is no chat client");

                return false;
            }
            else if (!await TryWriteAsync(client, data, cancellationToken))
            {
                _logger.LogError("Failed to write to chat client");

                TryRemoveChatClient(client);

                return false;
            }

            return true;
        }

        public bool TryRemoveRemoteClient(TcpClient client)
        {
            lock (this)
            {
                if (RemoteClient == client)
                {
                    RemoteClient = null;
                    return true;
                }
            }

            return false;
        }

        public async Task<bool> TryWriteRemoteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            var client = RemoteClient;

            if (client is null)
            {
                _logger.LogWarning("There is no remote client");

                return false;
            }
            else if (!await TryWriteAsync(client, data, cancellationToken))
            {
                _logger.LogError("Failed to write to remote client");

                TryRemoveRemoteClient(client);

                return false;
            }

            return true;
        }

        private async Task<bool> TryWriteAsync(TcpClient client, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            if (!client.Connected)
            {
                _logger.LogWarning("Client is disconnected");
                return false;
            }

            try
            {
                var stream = client.GetStream(); // Dispose on the client disposes the stream

                _logger.LogInformation("Writing {DataAscii} to {RemoteEndpoint}", Encoding.ASCII.GetString(data.Span).Replace("\r", "\\r").Replace("\n", "\\n"), client.Client.RemoteEndPoint);

                await stream.WriteAsync(data, cancellationToken);
                await stream.FlushAsync(cancellationToken);

                return true;
            }
            catch (IOException e)
            {
                _logger.LogWarning(e.Message);
                return false;
            }
            catch (SocketException e)
            {
                _logger.LogWarning(e.Message);
                return false;
            }
            catch (ObjectDisposedException)
            {
                _logger.LogWarning("Object was disposed");
                return false;
            }
        }
    }
}
