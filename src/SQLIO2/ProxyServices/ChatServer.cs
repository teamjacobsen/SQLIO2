using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
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
        private TcpListener _listener;

        public ChatServer(ChatHub chatHub, IOptions<ProxyOptions> options, ILogger<ChatServer> logger)
        {
            _chatHub = chatHub;
            _options = options.Value;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _listener = new TcpListener(new IPEndPoint(IPAddress.Loopback, _options.ChatPort));
            _listener.Start();

            _logger.LogInformation("Listening for chat on {LocalEndpoint}", _listener.LocalEndpoint);

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() => _listener.Stop());

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();

                    _logger.LogInformation("Accepting chat client {RemoteEndpoint} on {LocalEndpoint}", client.Client.RemoteEndPoint, client.Client.LocalEndPoint);

                    // Only one chat client can be connected at a time
                    await Task.Run(() => AcceptAsync(client, stoppingToken));
                }
                catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested)
                {
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

            while (true)
            {
                var result = await reader.ReadAsync();

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

            _logger.LogInformation("Chat client {RemoteEndpoint} was disconnected", client.Client.RemoteEndPoint);
        }
    }
}
