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
                    using (_chatHub.RemoteClient = new TcpClient())
                    {
                        try
                        {
                            await _chatHub.RemoteClient.ConnectAsync(_options.RemoteHost, _options.RemotePort);

                            _logger.LogInformation("Connected to device on {RemoteClientRemoteEndpoint}", _chatHub.RemoteClient.Client.RemoteEndPoint);

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

                            await protocol(_chatHub.RemoteClient);
                        }
                        catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionRefused)
                        {
                            // Unable to connect to server

                            // Retry in a second
                            await Task.Delay(1000, stoppingToken);
                        }
                        finally
                        {
                            if (_chatHub.RemoteClient.Connected)
                            {
                                _chatHub.RemoteClient.Client.Disconnect(reuseSocket: true);
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
