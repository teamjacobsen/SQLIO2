using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SQLIO2.Middlewares;
using SQLIO2.Protocols;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SQLIO2.ProxyServices
{
    class ChatConnection : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ChatHub _chatHub;
        private readonly ProtocolFactory _protocolFactory;
        private readonly ProxyOptions _options;
        private readonly SqlServerOptions _sqlOptions;
        private readonly ILogger<ChatConnection> _logger;

        public ChatConnection(IServiceProvider services, ChatHub chatHub, ProtocolFactory protocolFactory, IOptions<ProxyOptions> options, IOptions<SqlServerOptions> sqlOptions, ILogger<ChatConnection> logger)
        {
            _services = services;
            _chatHub = chatHub;
            _protocolFactory = protocolFactory;
            _options = options.Value;
            _sqlOptions = sqlOptions.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var remoteClient = _chatHub.RemoteClient = new TcpClient())
                    {
                        try
                        {
                            _logger.LogInformation("Connecting to device on {RemoteHost}:{RemotePort}", _options.RemoteHost, _options.RemotePort);

                            await remoteClient.ConnectAsync(_options.RemoteHost, _options.RemotePort);

                            _logger.LogInformation("Connected to device on {RemoteClientRemoteEndpoint}", remoteClient.Client.RemoteEndPoint);

                            var stackBuilder = new StackBuilder(_services);

                            if (_sqlOptions.ConnectionString != null)
                            {
                                stackBuilder.Use<SqlServerMiddleware>();
                            }

                            var stack = stackBuilder
                                .Use(next =>
                                {
                                    return async (packet) =>
                                    {
                                        await _chatHub.TryWriteChatAsync(packet.Raw, stoppingToken);
                                        await next(packet);
                                    };
                                })
                                .Build();

                            var protocol = _protocolFactory.Create(_options.ProtocolName, stack);

                            await protocol(_chatHub.RemoteClient, stoppingToken);

                            _logger.LogInformation("Connection to device on {RemoteClientRemoteEndpoint} was closed", remoteClient.Client.RemoteEndPoint);
                        }
                        catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionRefused)
                        {
                            // Unable to connect to server

                            _logger.LogWarning("Unable to connect to device server, retrying in one second");

                            // Retry in a second
                            await Task.Delay(1000, stoppingToken);
                        }
                        finally
                        {
                            if (remoteClient.Connected)
                            {
                                remoteClient.Client.Disconnect(reuseSocket: true);

                                _logger.LogWarning("Closed connection to device");
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                }
            }
        }
    }
}
