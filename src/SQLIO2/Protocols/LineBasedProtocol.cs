using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SQLIO2
{
    abstract class LineBasedProtocol
    {
        private static readonly byte[] NewlineCharacters = new byte[]
        {
            (byte)'\r',
            (byte)'\n'
        };

        private readonly RequestDelegate _stack;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger _logger;

        public LineBasedProtocol(RequestDelegate stack, IServiceScopeFactory serviceScopeFactory, ILogger logger)
        {
            _stack = stack;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public Task ProcessAsync(TcpClient client)
        {
            var pipe = new Pipe();
            var writingTask = FillPipeAsync(client.Client, pipe.Writer);
            var readingTask = ReadPipeAsync(client, pipe.Reader);

            return Task.WhenAll(readingTask, writingTask);
        }

        private async Task FillPipeAsync(Socket socket, PipeWriter writer)
        {
            const int minimumBufferSize = 512;

            while (true)
            {
                // Allocate at least 512 bytes from the PipeWriter
                var memory = writer.GetMemory(minimumBufferSize);

                try
                {
                    var bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    // Tell the PipeWriter how much was read from the Socket
                    writer.Advance(bytesRead);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to receive on socket");

                    break;
                }

                // Make the data available to the PipeReader
                var result = await writer.FlushAsync();

                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Tell the PipeReader that there's no more data coming
            writer.Complete();

            _logger.LogInformation("Client {RemoteEndpoint} was disconnected", socket.RemoteEndPoint);
        }

        private async Task ReadPipeAsync(TcpClient client, PipeReader reader)
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync();

                var buffer = result.Buffer;
                SequencePosition? position = null;

                do
                {
                    // Look for a EOL in the buffer
                    position = buffer.PositionOfAny(NewlineCharacters);

                    if (position != null)
                    {
                        var nextPosition = buffer.GetPosition(1, position.Value);

                        // Process the line (including the newline character)
                        ProcessLine(client, buffer.Slice(0, nextPosition));

                        // Skip the line + the newline character
                        buffer = buffer.Slice(nextPosition);
                    }
                }
                while (position != null);

                // Tell the PipeReader how much of the buffer we have consumed
                reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming
                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Mark the PipeReader as complete
            reader.Complete();
        }

        protected abstract void ProcessLine(TcpClient client, ReadOnlySequence<byte> line);

        protected void RunStack(TcpClient client, byte[] data)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var packet = new Packet(scope.ServiceProvider, client, data);

                        _logger.LogInformation("Handling packet {DataAscii} from {RemoteEndpoint}", Encoding.ASCII.GetString(data).Replace("\r", "\\r").Replace("\n", "\\n"), client.Client.RemoteEndPoint);

                        await _stack(packet);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unknown stack error while handling packet from {RemoteEndpoint}", client.Client.RemoteEndPoint);
                }
            });
        }
    }
}
