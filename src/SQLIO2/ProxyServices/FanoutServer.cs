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
    class FanoutServer : BackgroundService
    {
        private readonly FanoutHub _fanoutHub;
        private readonly ProxyOptions _options;
        private readonly ILogger<FanoutServer> _logger;

        public FanoutServer(FanoutHub fanoutHub, IOptions<ProxyOptions> options, ILogger<FanoutServer> logger)
        {
            _fanoutHub = fanoutHub;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var listener = new TcpListener(new IPEndPoint(IPAddress.Loopback, _options.FanoutPort));
            listener.Start();
            stoppingToken.Register(() => listener.Stop());
            
            _logger.LogInformation("Listening for fanout on {LocalEndpoint}", listener.LocalEndpoint);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var client = await Task.Run(listener.AcceptTcpClientAsync, stoppingToken);

                    _logger.LogInformation("Accepting fanout client {RemoteEndpoint} on {LocalEndpoint}", client.Client.RemoteEndPoint, client.Client.LocalEndPoint);

                    _ = Task.Run(() => AcceptAsync(client, stoppingToken));
                }
                catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested)
                {
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                }
            }

            _logger.LogInformation("Stopping fanout server");
        }

        private async Task AcceptAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using var clientStream = client.GetStream();
            var reader = PipeReader.Create(clientStream);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await reader.ReadAsync(cancellationToken);

                    if (result.IsCompleted || result.Buffer.IsEmpty)
                    {
                        break;
                    }

                    foreach (var segment in result.Buffer)
                    {
                        await _fanoutHub.WriteFanoutAsync(segment, cancellationToken);
                    }

                    reader.AdvanceTo(result.Buffer.End);
                }
                catch (IOException e) when (e.InnerException is SocketException se && se.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // Client disconnected abruptly
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            }

            _logger.LogInformation("Fanout client {RemoteEndpoint} was disconnected", client.Client.RemoteEndPoint);
        }
    }
}
