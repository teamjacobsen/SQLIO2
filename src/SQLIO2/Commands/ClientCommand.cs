using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SQLIO2.Protocols;
using System;
using System.ComponentModel.DataAnnotations;
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
            using (var client = new TcpClient())
            {
                if (Host != null)
                {
                    await client.ConnectAsync(Host, Port);
                }
                else
                {
                    await client.ConnectAsync(IPAddress.Loopback, Port);
                }

                try
                {
                    var stream = client.GetStream();

                    if (Filename is object)
                    {
                        using var file = File.OpenRead(Filename);
                        await file.CopyToAsync(stream);
                    }
                    else
                    {
                        var data = GetDataBytes();

                        await stream.WriteAsync(data);
                    }

                    await stream.FlushAsync();

                    if (TimeoutMs != null)
                    {
                        var services = new ServiceCollection()
                            .AddLogging(options =>
                            {
                                if (Verbose)
                                {
                                    options.AddConsole();
                                }
                            })
                            .Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(TimedLogger<>)))
                            .AddSingleton<ProtocolFactory>()
                            .BuildServiceProvider();

                        var protocolFactory = services.GetRequiredService<ProtocolFactory>();

                        var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

                        var protocol = protocolFactory.Create(ProtocolName, packet =>
                        {
                            Console.WriteLine(packet.ToString());

                            tcs.SetResult(null);

                            return Task.CompletedTask;
                        });

                        _ = Task.Run(() => protocol(client));

                        await Task.WhenAny(tcs.Task, Task.Delay(TimeoutMs.Value));

                        if (!tcs.Task.IsCompleted)
                        {
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
            if (ProtocolName.Equals("sc500", StringComparison.OrdinalIgnoreCase))
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
