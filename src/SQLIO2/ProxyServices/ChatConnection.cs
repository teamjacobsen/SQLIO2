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
                    using var remoteClient = new TcpClient();
                    var timeout = TimeSpan.Zero;

                    try
                    {
                        _logger.LogInformation("Connecting to device on {RemoteHost}:{RemotePort}", _options.RemoteHost, _options.RemotePort);

                        await remoteClient.ConnectAsync(_options.RemoteHost, _options.RemotePort);

                        _logger.LogInformation("Connected to device on {RemoteClientRemoteEndpoint}", remoteClient.Client.RemoteEndPoint);

                        _chatHub.RemoteClient = remoteClient;

                        var stack = CreateStack(stoppingToken);
                        var protocol = _protocolFactory.Create(_options.ProtocolName, stack);
                        await protocol(remoteClient, stoppingToken);

                        _logger.LogInformation("Connection to device on {RemoteClientRemoteEndpoint} was closed", remoteClient.Client.RemoteEndPoint);
                    }
                    catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionRefused)
                    {
                        // Unable to connect to server

                        _logger.LogWarning("Unable to connect to device server, retrying in one second");

                        // Retry in a second
                        timeout = TimeSpan.FromMilliseconds(1000);
                    }
                    catch (SocketException e)
                    {
                        // Some other socket exception

                        _logger.LogError(e, "Got exception while being connected to device");

                        timeout = TimeSpan.FromMilliseconds(500);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Unknown error");

                        timeout = TimeSpan.FromMilliseconds(500);
                    }

                    if (timeout > TimeSpan.Zero)
                    {
                        await Task.Delay(timeout, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                }
            }

            _logger.LogInformation("Exiting chat connection service");
        }

        private RequestDelegate CreateStack(CancellationToken cancellationToken)
        {
            var stackBuilder = new StackBuilder(_services);

            if (_sqlOptions.ConnectionString != null)
            {
                stackBuilder.Use<SqlServerMiddleware>();
            }
            else
            {
                _logger.LogWarning("No ConnectionString found, disabling SqlServer injection");
            }

            return stackBuilder
                .Use(next =>
                {
                    return async (packet) =>
                    {
                        if (!await _chatHub.TryWriteChatAsync(packet.Raw, cancellationToken))
                        {
                            _logger.LogWarning("Unable to write {RawCount} bytes to chat client", packet.Raw.Length);
                        }
                        await next(packet);
                    };
                })
                .Build();
        }
    }
}
