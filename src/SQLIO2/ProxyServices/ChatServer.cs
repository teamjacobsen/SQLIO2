using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SQLIO2.ProxyServices
{
    class ChatServer : BackgroundService
    {
        private readonly ChatHub _chatHub;
        private readonly ProxyOptions _options;
        private readonly ILogger<ChatServer> _logger;

        public ChatServer(ChatHub chatHub, IOptions<ProxyOptions> options, ILogger<ChatServer> logger)
        {
            _chatHub = chatHub;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var listener = new TcpListener(new IPEndPoint(IPAddress.Loopback, _options.ChatPort));
            listener.Start();

            _logger.LogInformation("Listening for chat on {LocalEndpoint}", listener.LocalEndpoint);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptAsync(stoppingToken);

                    _logger.LogInformation("Accepting chat client {RemoteEndpoint} on {LocalEndpoint}", client.Client.RemoteEndPoint, client.Client.LocalEndPoint);

                    // Only one chat client can be connected at a time
                    await AcceptAsync(client, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                }
            }

            _logger.LogInformation("Stopping chat server");
        }

        private async Task AcceptAsync(TcpClient client, CancellationToken cancellationToken)
        {
            _chatHub.ChatClient = client;

            using var stream = client.GetStream();
            var reader = PipeReader.Create(stream);

            _logger.LogInformation("Waiting for chat client to send data");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await reader.ReadAsync(cancellationToken);

                    if (result.IsCompleted || result.Buffer.IsEmpty)
                    {
                        break;
                    }

                    _logger.LogDebug("Received {BufferLength} bytes", result.Buffer.Length);

                    foreach (var segment in result.Buffer)
                    {
                        if (!await _chatHub.TryWriteRemoteAsync(segment, cancellationToken))
                        {
                            _logger.LogWarning("Unable to forward {SegmentLength} bytes", segment.Length);
                        }
                    }

                    reader.AdvanceTo(result.Buffer.End);
                }
                catch (IOException e) when (e.InnerException is SocketException se && se.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // Client disconnected abruptly
                    break;
                }
            }

            _logger.LogInformation("Chat client {RemoteEndpoint} was disconnected", client.Client.RemoteEndPoint);
        }
    }
}
