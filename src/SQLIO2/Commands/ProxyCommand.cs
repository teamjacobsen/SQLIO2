using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SQLIO2.Middlewares;
using SQLIO2.Protocols;
using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Threading.Tasks;

namespace SQLIO2
{
    class ProxyCommand
    {
        private IHost _host;
        private ILogger<ProxyCommand> _logger;
        private ServerFactory _serverFactory;

        [Option("-l|--listen-port")]
        [Required]
        public int ListenPort { get; set; }

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

            var deviceServer = CreateDeviceServer();
            await deviceServer.StartListeningAsync();
            _logger.LogInformation("Listening for device on {DeviceServerLocalEndpoint}", deviceServer.LocalEndpoint);

            Server fanoutServer = null;
            if (FanoutPort != null)
            {
                fanoutServer = CreateFanoutServer();
                await fanoutServer.StartListeningAsync();
                _logger.LogInformation("Listening for fanout on {FanoutServerLocalEndpoint}", fanoutServer.LocalEndpoint);
            }

            await _host.RunAsync();

            await deviceServer.StopListeningAsync();

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

            return _serverFactory.Create(new IPEndPoint(IPAddress.Any, ListenPort), client =>
            {
                if (FanoutPort != null)
                {
                    var proxyService = _host.Services.GetRequiredService<ProxyService>();

                    if (proxyService.TryRegister(client))
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
                var proxyService = _host.Services.GetRequiredService<ProxyService>();

                var clientStream = client.GetStream();

                int bytesRead;
                var buffer = new byte[1024];
                while ((bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await proxyService.FanoutAsync(buffer.AsMemory(0, bytesRead));
                }

                _logger.LogInformation("Fanout client {RemoteEndpoint} was disconnected", client.Client.RemoteEndPoint);
            });
        }
    }
}
