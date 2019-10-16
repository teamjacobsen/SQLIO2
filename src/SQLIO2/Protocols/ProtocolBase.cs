using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SQLIO2.Protocols
{
    abstract class ProtocolBase
    {
        private readonly ILogger _logger;

        public ProtocolBase(ILogger logger)
        {
            _logger = logger;
        }

        public Task ProcessAsync(TcpClient client, CancellationToken cancellationToken)
        {
            // See https://docs.microsoft.com/en-us/dotnet/standard/io/pipelines
            var pipe = new Pipe();
            var writingTask = FillPipeAsync(client.Client, pipe.Writer, cancellationToken);
            var readingTask = ReadPipeAsync(client, pipe.Reader, cancellationToken);

            return Task.WhenAll(readingTask, writingTask);
        }

        private async Task FillPipeAsync(Socket socket, PipeWriter writer, CancellationToken cancellationToken)
        {
            const int minimumBufferSize = 512;

            while (true)
            {
                // Allocate at least 512 bytes from the PipeWriter
                var memory = writer.GetMemory(minimumBufferSize);

                try
                {
                    var bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None, cancellationToken);

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
            await writer.CompleteAsync();

            _logger.LogInformation("Client {RemoteEndpoint} was disconnected", socket.RemoteEndPoint);
        }

        private async Task ReadPipeAsync(TcpClient client, PipeReader reader, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken);
                var buffer = result.Buffer;
                
                Read(client, ref buffer);

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }

            await reader.CompleteAsync();
        }

        protected abstract void Read(TcpClient client, ref ReadOnlySequence<byte> buffer);
    }
}
