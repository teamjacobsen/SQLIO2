using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SQLIO2.Protocols;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SQLIO2
{
    class ClientCommand
    {
        [Option("-h|--host")]
        public string Host { get; set; }

        [Option("-p|--port")]
        [Required]
        public int Port { get; set; }

        [Argument(0)]
        public string DataHexOrXml { get; set; }

        [Option("-f|--filename")]
        public string Filename { get; set; }

        [Option("-t|--reply-timeout")]
        public int? TimeoutMs { get; set; }

        [Option("-r|--reply-protocol-name")]
        public string ProtocolName { get; set; }

        [Option("-v|--verbose")]
        public bool Verbose { get; set; }

        public async Task<int> HandleAsync()
        {
            var elapsedOnEntering = Program.Started.ElapsedMilliseconds;

            var serviceCollection = new ServiceCollection();

            if (Verbose)
            {
                serviceCollection
                    .AddLogging(options => options.ClearProviders().AddConsole())
                    .Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(TimedLogger<>)));
            }
            else
            {
                serviceCollection.AddLogging(options => options.ClearProviders());
            }

            var services = serviceCollection
                .AddSingleton<ProtocolFactory>()
                .BuildServiceProvider();
            var logger = services.GetRequiredService<ILogger<ClientCommand>>();

            logger.LogInformation("Booted after {ElapsedOnEntering}/{ElapsedMilliseconds}ms", elapsedOnEntering, Program.Started.ElapsedMilliseconds);

            using (var client = new TcpClient())
            {
                var stopwatch = Stopwatch.StartNew();
                if (Host != null)
                {
                    // https://github.com/dotnet/corefx/issues/41588
                    if (Host == "localhost")
                    {
                        await client.ConnectAsync(IPAddress.Loopback, Port);
                    }
                    else
                    {
                        await client.ConnectAsync(Host, Port);
                    }
                }
                else
                {
                    await client.ConnectAsync(IPAddress.Loopback, Port);
                }

                logger.LogInformation("Connected in {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);

                try
                {
                    TaskCompletionSource<Packet> tcs = null;

                    if (TimeoutMs != null)
                    {
                        var protocolFactory = services.GetRequiredService<ProtocolFactory>();

                        tcs = new TaskCompletionSource<Packet>(TaskCreationOptions.RunContinuationsAsynchronously);

                        var protocol = protocolFactory.Create(ProtocolName, packet =>
                        {
                            tcs.SetResult(packet);

                            return Task.CompletedTask;
                        });

                        _ = Task.Run(() => protocol(client, default));
                    }

                    stopwatch.Restart();

                    var stream = client.GetStream();

                    if (Filename is object)
                    {
                        using var file = File.OpenRead(Filename);
                        await file.CopyToAsync(stream);
                        await stream.FlushAsync();
                    }
                    else
                    {
                        var data = GetDataBytes();

                        await stream.WriteAsync(data);
                        await stream.FlushAsync();
                    }

                    logger.LogInformation("Sent in {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);

                    stopwatch.Restart();

                    if (TimeoutMs != null)
                    {
                        await Task.WhenAny(tcs.Task, Task.Delay(TimeoutMs.Value));

                        if (tcs.Task.IsCompleted)
                        {
                            logger.LogInformation("Got reply after {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);

                            var replyTask = tcs.Task;
                            var reply = await replyTask;

                            Console.WriteLine(reply.ToString());
                        }
                        else
                        {
                            logger.LogWarning("No reply received");
                            return 1;
                        }
                    }
                }
                finally
                {
                    client.Client.Disconnect(reuseSocket: false);
                }
            }

            return 0;
        }

        private byte[] GetDataBytes()
        {
            if (ProtocolName?.Equals("sc500", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Encoding.UTF8.GetBytes(DataHexOrXml);
            }

            return ToByteArray(DataHexOrXml);
        }

        private static byte[] ToByteArray(string hexString)
        {
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException("The hex string does not have an even number of characters");
            }

            var byteArray = new byte[hexString.Length / 2];

            for (var i = 0; i < hexString.Length / 2; i++)
            {
                byteArray[i] = (byte)(
                    (byte.Parse(hexString.AsSpan(2 * i, 1), NumberStyles.HexNumber) << 4) |
                    byte.Parse(hexString.AsSpan(2 * i + 1, 1), NumberStyles.HexNumber));
            }

            return byteArray;
        }
    }
}
