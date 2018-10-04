using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        [Option("-l|--listen-port")]
        [Required]
        public int ListenPort { get; set; }

        [Option("-f|--fanout-port")]
        public int? FanoutPort { get; set; }

        [Option("-r|--protocol-name")]
        public string ProtocolName { get; set; }

        public async Task<int> HandleAsync()
        {
            var shutdownTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Shutdown() => shutdownTcs.TrySetResult(null);

            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => Shutdown();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;

                Shutdown();
            };

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var services = new ServiceCollection()
                .AddLogging(options => options.AddConsole())
                .Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(TimedLogger<>)))
                .AddSingleton<ProtocolFactory>()
                .AddSingleton<ServerFactory>()
                .AddSingleton<ProxyService>()
                .Configure<SqlServerOptions>(configuration.GetSection("SqlServer"))
                .BuildServiceProvider();

            var protocolFactory = services.GetRequiredService<ProtocolFactory>();
            var serverFactory = services.GetRequiredService<ServerFactory>();

            Server deviceServer;
            Server fanoutServer = null;

            {
                var stack = new StackBuilder(services)
                    .Use<SqlServerMiddleware>()
                    .Build();

                var protocol = protocolFactory.Create(ProtocolName, stack);
                
                deviceServer = serverFactory.Create(new IPEndPoint(IPAddress.Any, ListenPort), client =>
                {
                    if (FanoutPort != null)
                    {
                        var proxyService = services.GetRequiredService<ProxyService>();
                        var logger = services.GetRequiredService<ILogger<ProxyCommand>>();

                        if (proxyService.TryRegister(client))
                        {
                            logger.LogInformation("Registered {RemoteEndpoint} for fanout through proxy", client.Client.RemoteEndPoint);
                        }
                    }

                    return protocol(client);
                });

                await deviceServer.StartListeningAsync();

                Console.WriteLine($"Listening for device on {deviceServer.LocalEndpoint}");
            }

            if (FanoutPort != null)
            {
                fanoutServer = serverFactory.Create(new IPEndPoint(IPAddress.Loopback, FanoutPort.Value), async client =>
                {
                    var proxyService = services.GetRequiredService<ProxyService>();
                    var logger = services.GetRequiredService<ILogger<ProxyCommand>>();

                    var clientStream = client.GetStream();

                    int bytesRead;
                    var buffer = new byte[1024];
                    while ((bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await proxyService.FanoutAsync(buffer.AsMemory(0, bytesRead));
                    }

                    logger.LogInformation("Fanout client {RemoteEndpoint} was disconnected", client.Client.RemoteEndPoint);
                });

                await fanoutServer.StartListeningAsync();

                Console.WriteLine($"Listening for fanout on {fanoutServer.LocalEndpoint}");
            }

            Console.WriteLine("Press Ctrl+C to exit the program");

            await shutdownTcs.Task;

            await deviceServer.StopListeningAsync();

            if (fanoutServer != null)
            {
                await fanoutServer.StopListeningAsync();
            }

            Console.WriteLine("Goodbye...");

            return 0;
        }
    }
}
