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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var listener = new TcpListener(new IPEndPoint(IPAddress.Any, _options.ListenPort));
            listener.Start();

            _logger.LogInformation("Listening for device on {LocalEndpoint}", listener.LocalEndpoint);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptAsync(stoppingToken);

                    _logger.LogInformation("Accepting device client {RemoteEndpoint} on {LocalEndpoint}", client.Client.RemoteEndPoint, client.Client.LocalEndPoint);

                    _ = Task.Run(() => AcceptAsync(client, stoppingToken));
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
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
