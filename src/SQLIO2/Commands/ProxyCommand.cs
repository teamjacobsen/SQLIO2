using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SQLIO2.Middlewares;
using SQLIO2.Protocols;
using SQLIO2.ProxyServices;
using System;
using System.Threading.Tasks;

namespace SQLIO2
{
    class ProxyCommand
    {
        [Option("-l|--listen-port")]
        public int? ListenPort { get; set; }

        [Option("-f|--fanout-port")]
        public int? FanoutPort { get; set; }


        [Option("-h|--host")]
        public string RemoteHost { get; set; }

        [Option("-p|--port")]
        public int? RemotePort { get; set; }

        [Option("-c|--chat-port")]
        public int? ChatPort { get; set; }


        [Option("-r|--protocol-name")]
        public string ProtocolName { get; set; }

        public async Task<int> HandleAsync()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureHostConfiguration(config => config.AddJsonFile("appsettings.json", optional: true))
                .ConfigureLogging(logging => logging.AddConsole())
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(20))
                        .Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(TimedLogger<>)))
                        .AddSingleton<ProtocolFactory>()
                        .Configure<SqlServerOptions>(hostContext.Configuration.GetSection("SqlServer"))
                        .Configure<ProxyOptions>(options =>
                        {
                            options.ListenPort = ListenPort ?? 0;
                            options.FanoutPort = FanoutPort ?? 0;
                            options.RemoteHost = RemoteHost;
                            options.RemotePort = RemotePort ?? 0;
                            options.ChatPort = ChatPort ?? 0;
                            options.ProtocolName = ProtocolName;
                        });

                    if (ListenPort != null)
                    {
                        services
                            .AddSingleton<FanoutHub>()
                            .AddHostedService<DeviceServer>();

                        if (FanoutPort != null)
                        {
                            services.AddHostedService<FanoutServer>();
                        }
                    }
                    else if (RemoteHost != null && RemotePort != null)
                    {
                        services
                            .AddSingleton<ChatHub>()
                            .AddHostedService<ChatConnection>();

                        if (ChatPort != null)
                        {
                            services.AddHostedService<ChatServer>();
                        }
                    }
                })
                .Build();

            await host.RunAsync();

            return 0;
        }
    }
}