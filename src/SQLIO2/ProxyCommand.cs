using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        [Required]
        [Option("-l|--listen-port")]
        public int ListenPort { get; set; }

        [Option("-f|--fanout-port")]
        public int? FanoutPort { get; set; }

        [Option("-r|--protocol-name")]
        public string ProtocolName { get; set; }

        public async Task<int> OnExecuteAsync(IConsole console)
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
                    //.Use<SqlServerMiddleware>()
                    .Build();

                var protocol = protocolFactory.Create(ProtocolName, stack);
                
                deviceServer = serverFactory.Create(new IPEndPoint(IPAddress.Any, ListenPort), client =>
                {
                    var proxyService = services.GetRequiredService<ProxyService>();
                    var logger = services.GetRequiredService<ILogger<ProxyCommand>>();

                    if (proxyService.TryRegister(client))
                    {
                        logger.LogInformation("Registered {RemoteEndpoint} for fanout through proxy", client.Client.RemoteEndPoint);
                    }

                    return protocol(client);
                });

                await deviceServer.StartListeningAsync();

                console.WriteLine($"Listening for device on {deviceServer.LocalEndpoint}");
            }

            if (FanoutPort != null)
            {
                var stack = new StackBuilder(services)
                    .Use<ProxyFanoutMiddleware>()
                    .Build();

                var protocol = protocolFactory.Create(ProtocolName, stack);

                fanoutServer = serverFactory.Create(new IPEndPoint(IPAddress.Loopback, FanoutPort.Value), protocol);

                await fanoutServer.StartListeningAsync();

                console.WriteLine($"Listening for fanout on {fanoutServer.LocalEndpoint}");
            }

            console.WriteLine("Press Ctrl+C to exit the program");

            await shutdownTcs.Task;

            await deviceServer.StopListeningAsync();

            if (fanoutServer != null)
            {
                await fanoutServer.StopListeningAsync();
            }

            console.WriteLine("Goodbye...");

            return 0;
        }
    }
}
