using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SQLIO2.Middlewares;
using SQLIO2.Protocols;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SQLIO2
{
    class ProxyCommand
    {
        private IHost _host;
        private ILogger<ProxyCommand> _logger;
        private ServerFactory _serverFactory;
        private ProxyService _proxyService;

        [Option("-l|--listen-port")]
        public int? ListenPort { get; set; }

        [Option("-h|--host")]
        public string RemoteHost { get; set; }

        [Option("-p|--port")]
        public int? RemotePort { get; set; }

        [Option("-f|--fanout-port")]
        public int? FanoutPort { get; set; }

        [Option("-r|--protocol-name")]
        public string ProtocolName { get; set; }

        public async Task<int> HandleAsync()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureHostConfiguration(config => config.AddJsonFile("appsettings.json", optional: true))
                .ConfigureLogging(logging => logging.AddConsole())
                .ConfigureServices((hostContext, services) => services
                    .Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(TimedLogger<>)))
                    .AddSingleton<ProtocolFactory>()
                    .AddSingleton<ServerFactory>()
                    .AddSingleton<ProxyService>()
                    .Configure<SqlServerOptions>(hostContext.Configuration.GetSection("SqlServer"))
                )
                .Build();

            _logger = _host.Services.GetRequiredService<ILogger<ProxyCommand>>();
            _serverFactory = _host.Services.GetRequiredService<ServerFactory>();
            _proxyService = _host.Services.GetRequiredService<ProxyService>();

            Server deviceServer = null;
            TcpClient remoteClient = null;
            if (ListenPort != null)
            {
                deviceServer = CreateDeviceServer();
                await deviceServer.StartListeningAsync();
                _logger.LogInformation("Listening for device on {DeviceServerLocalEndpoint}", deviceServer.LocalEndpoint);
            }
            else if (RemoteHost != null && RemotePort != null)
            {
                remoteClient = new TcpClient();
                await remoteClient.ConnectAsync(RemoteHost, RemotePort.Value);
                _logger.LogInformation("Connected to device on {RemoteClientRemoteEndpoint}", remoteClient.Client.RemoteEndPoint);

                if (FanoutPort != null)
                {
                    if (_proxyService.TryRegister(remoteClient))
                    {
                        _logger.LogInformation("Registered remote device {RemoteEndpoint} for fanout through proxy", remoteClient.Client.RemoteEndPoint);
                    }
                }

                var stack = new StackBuilder(_host.Services)
                    .Use<SqlServerMiddleware>()
                    .Build();

                var protocolFactory = _host.Services.GetRequiredService<ProtocolFactory>();
                var protocol = protocolFactory.Create(ProtocolName, stack);

                _ = Task.Run(() => protocol(remoteClient));
            }

            Server fanoutServer = null;
            if (FanoutPort != null)
            {
                fanoutServer = CreateFanoutServer();
                await fanoutServer.StartListeningAsync();
                _logger.LogInformation("Listening for fanout on {FanoutServerLocalEndpoint}", fanoutServer.LocalEndpoint);
            }

            await _host.RunAsync();

            if (deviceServer != null)
            {
                await deviceServer.StopListeningAsync();
            }

            if (remoteClient != null)
            {
                remoteClient.Client.Disconnect(reuseSocket: true);
                remoteClient.Dispose();
            }

            if (fanoutServer != null)
            {
                await fanoutServer.StopListeningAsync();
            }

            return 0;
        }

        private Server CreateDeviceServer()
        {
            var stack = new StackBuilder(_host.Services)
                    .Use<SqlServerMiddleware>()
                    .Build();

            var protocolFactory = _host.Services.GetRequiredService<ProtocolFactory>();
            var protocol = protocolFactory.Create(ProtocolName, stack);

            return _serverFactory.Create(new IPEndPoint(IPAddress.Any, ListenPort.Value), client =>
            {
                if (FanoutPort != null)
                {
                    if (_proxyService.TryRegister(client))
                    {
                        _logger.LogInformation("Registered {RemoteEndpoint} for fanout through proxy", client.Client.RemoteEndPoint);
                    }
                }

                return protocol(client);
            });
        }

        private Server CreateFanoutServer()
        {
            return _serverFactory.Create(new IPEndPoint(IPAddress.Loopback, FanoutPort.Value), async client =>
            {
                using var clientStream = client.GetStream();
                var reader = PipeReader.Create(clientStream);

                while (true)
                {
                    var result = await reader.ReadAsync();

                    if (result.IsCompleted || result.Buffer.IsEmpty)
                    {
                        break;
                    }

                    foreach (var segment in result.Buffer)
                    {
                        await _proxyService.FanoutAsync(segment);
                    }

                    reader.AdvanceTo(result.Buffer.End);
                }

                _logger.LogInformation("Fanout client {RemoteEndpoint} was disconnected", client.Client.RemoteEndPoint);
            });
        }
    }
}
