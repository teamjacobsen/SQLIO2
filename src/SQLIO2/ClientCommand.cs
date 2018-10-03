﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SQLIO2.Protocols;
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
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

        [Required]
        [Argument(0)]
        public string DataHex { get; set; }

        [Option("-t|--reply-timeout")]
        public int? TimeoutMs { get; set; }

        [Option("-r|--reply-protocol-name")]
        public string ProtocolName { get; set; }

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

                var data = ToByteArray(DataHex);

                var stream = client.GetStream();

                await stream.WriteAsync(data);
                await stream.FlushAsync();

                if (TimeoutMs != null)
                {
                    var services = new ServiceCollection()
                        .AddLogging(options => options.AddConsole())
                        .AddSingleton<ProtocolFactory>()
                        .BuildServiceProvider();

                    var protocolFactory = services.GetRequiredService<ProtocolFactory>();

                    var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

                    var protocol = protocolFactory.Create(ProtocolName, packet =>
                    {
                        Console.Error.WriteLine(ToHexString(packet.Raw));

                        tcs.SetResult(null);

                        return Task.CompletedTask;
                    });

                    client.ReceiveTimeout = TimeoutMs.Value;

                    await Task.WhenAny(protocol(client), tcs.Task);
                }
            }

            return 0;
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

        private static string ToHexString(byte[] byteArray)
        {
            var builder = new StringBuilder(byteArray.Length * 2);

            foreach (var @byte in byteArray)
            {
                builder.AppendFormat("{0:x2}", @byte);
            }

            return builder.ToString();
        }
    }
}
