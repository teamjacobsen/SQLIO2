﻿using Microsoft.Extensions.DependencyInjection;
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
        public string DataHexOrXml { get; set; } = string.Empty;

        [Option("-f|--filename")]
        public string Filename { get; set; }

        [Option("-t|--reply-timeout")]
        public int? TimeoutMs { get; set; }

        [Option("-r|--reply-protocol-name")]
        public string ProtocolName { get; set; }

        [Option("-v|--verbose")]
        public bool Verbose { get; set; }

        [Option("-n|--count")]
        public int Count { get; set; } = 1;

        [Option("-i|--interval")]
        public int Interval { get; set; } = 1000;

        public async Task<int> HandleAsync()
        {
            var elapsedOnEntering = Program.Started.ElapsedMilliseconds;

            ILogger logger;

            if (Verbose)
            {
                var services = new ServiceCollection()
                    .AddLogging(options => options.ClearProviders().AddConsole())
                    .Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(TimedLogger<>)))
                    .AddSingleton<ProtocolFactory>()
                    .BuildServiceProvider();

                logger = services.GetRequiredService<ILogger<ClientCommand>>();
            }
            else
            {
                logger = new FakeLogger();
            }

            logger.LogInformation("Booted after {ElapsedOnEntering}/{ElapsedMilliseconds}ms", elapsedOnEntering, Program.Started.ElapsedMilliseconds);

            using (var client = new TcpClient())
            {
                try
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

                    var clientStream = client.GetStream(); // Dispose on the client disposes the stream

                    logger.LogInformation("Connected in {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);

                    TaskCompletionSource<Packet> replyTcs = null;

                    if (TimeoutMs != null)
                    {
                        var protocolFactory = new ProtocolFactory(logger);
                        var protocol = protocolFactory.Create(ProtocolName, packet =>
                        {
                            replyTcs.SetResult(packet);

                            return Task.CompletedTask;
                        });

                        _ = Task.Run(() => protocol(client, default));
                    }

                    var timeouts = 0;
                    for (var sent = 0; sent < Count || Count == -1; sent++)
                    {
                        if (sent > 0)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(Interval));
                        }

                        stopwatch.Restart();

                        if (Filename is object)
                        {
                            using var file = File.OpenRead(Filename);
                            await file.CopyToAsync(clientStream);
                            await clientStream.FlushAsync();
                        }
                        else
                        {
                            var data = GetDataBytes();

                            await clientStream.WriteAsync(data);
                            await clientStream.FlushAsync();
                        }

                        logger.LogInformation("Sent in {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);

                        if (TimeoutMs != null)
                        {
                            replyTcs = new TaskCompletionSource<Packet>(TaskCreationOptions.RunContinuationsAsynchronously);

                            stopwatch.Restart();

                            await Task.WhenAny(replyTcs.Task, Task.Delay(TimeoutMs.Value));

                            if (replyTcs.Task.IsCompleted)
                            {
                                logger.LogInformation("Got reply after {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);

                                var replyTask = replyTcs.Task;
                                var reply = await replyTask;

                                Console.WriteLine(reply.ToString());
                            }
                            else
                            {
                                logger.LogError("No reply received");
                                timeouts++;
                            }
                        }
                    }

                    if (timeouts > 0)
                    {
                        return 1;
                    }
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    logger.LogError("Unable to connect");
                    return 2;
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
