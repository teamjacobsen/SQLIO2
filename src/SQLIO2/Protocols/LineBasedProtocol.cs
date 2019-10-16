using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SQLIO2.Protocols
{
    abstract class LineBasedProtocol : ProtocolBase
    {
        private static readonly byte[] NewlineCharacters = new byte[]
        {
            (byte)'\r',
            (byte)'\n'
        };

        private readonly RequestDelegate _stack;
        private readonly ILogger _logger;

        public LineBasedProtocol(RequestDelegate stack, ILogger logger) : base(logger)
        {
            _stack = stack;
            _logger = logger;
        }

        protected override SequencePosition Read(TcpClient client, in ReadOnlySequence<byte> sequence)
        {
            var reader = new SequenceReader<byte>(sequence);

            while (reader.TryReadToAny(out ReadOnlySequence<byte> lineBytes, NewlineCharacters, advancePastDelimiter: true))
            {
                var lineBytesWithNewline = reader.Sequence.Slice(lineBytes.Start, lineBytes.Length + 1);

                ProcessLine(client, lineBytesWithNewline);
            }

            return reader.Position;
        }

        protected abstract void ProcessLine(TcpClient client, ReadOnlySequence<byte> line);

        protected void RunStack(TcpClient client, byte[] data)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var packet = new Packet(client, data);

                    _logger.LogInformation("Handling packet {DataAscii} from {RemoteEndpoint}", Encoding.ASCII.GetString(data).Replace("\r", "\\r").Replace("\n", "\\n"), client.Client.RemoteEndPoint);

                    await _stack(packet);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unknown stack error while handling packet from {RemoteEndpoint}", client.Client.RemoteEndPoint);
                }
            });
        }
    }
}
