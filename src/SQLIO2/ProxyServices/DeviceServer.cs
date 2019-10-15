using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SQLIO2.Middlewares;
using SQLIO2.Protocols;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SQLIO2.ProxyServices
{
    class DeviceServer : BackgroundService
    {
        private readonly FanoutHub _fanoutHub;
        private readonly ProxyOptions _options;
        private readonly ILogger<DeviceServer> _logger;
        private readonly Func<TcpClient, CancellationToken, Task> _protocol;
        private TcpListener _listener;

        public DeviceServer(ProtocolFactory protocolFactory, IServiceProvider services, FanoutHub fanoutHub, IOptions<ProxyOptions> options, IOptions<SqlServerOptions> sqlOptions, ILogger<DeviceServer> logger)
        {
            _fanoutHub = fanoutHub;
            _options = options.Value;
            _logger = logger;

            var stackBuilder = new StackBuilder(services);

            if (sqlOptions.Value.ConnectionString != null)
            {
                stackBuilder.Use<SqlServerMiddleware>();
            }

            var stack = stackBuilder.Build();

            _protocol = protocolFactory.Create(_options.ProtocolName, stack);
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _listener = new TcpListener(new IPEndPoint(IPAddress.Any, _options.ListenPort));
            _listener.Start();

            _logger.LogInformation("Listening for device on {LocalEndpoint}", _listener.LocalEndpoint);

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

                    _logger.LogInformation("Accepting device client {RemoteEndpoint} on {LocalEndpoint}", client.Client.RemoteEndPoint, client.Client.LocalEndPoint);

                    _ = Task.Run(() => AcceptAsync(client, stoppingToken), stoppingToken);
                }
                catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested)
                {
                }
            }

            _logger.LogInformation("Stopping device server");
        }

        private Task AcceptAsync(TcpClient client, CancellationToken cancellationToken)
        {
            if (_fanoutHub.TryRegister(client))
            {
                _logger.LogInformation("Registered connected device {RemoteEndpoint} for fanout through proxy", client.Client.RemoteEndPoint);
            }

            return _protocol(client, cancellationToken);
        }
    }
}
