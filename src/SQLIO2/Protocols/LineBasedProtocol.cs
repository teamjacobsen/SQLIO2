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

        protected override void Read(TcpClient client, ref ReadOnlySequence<byte> buffer)
        {
            while (TryReadLine(ref buffer, out var lineWithNewline))
            {
                if (lineWithNewline.Length > 1)
                {
                    _logger.LogInformation("Found line with length {LineLength}, including newline character: {LineHex}", lineWithNewline.Length, BitConverter.ToString(lineWithNewline.ToArray()).Replace("-", string.Empty));

                    ProcessLine(client, lineWithNewline);
                }
            }

            if (buffer.Length >= 10)
            {
                _logger.LogWarning("There are {RemainingBytes} unprocessed bytes: {RemainingHex}", buffer.Length, BitConverter.ToString(buffer.ToArray()).Replace("-", string.Empty));
            }
        }

        private bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> lineWithNewlineCharacter)
        {
            var reader = new SequenceReader<byte>(buffer);

            if (reader.TryReadToAny(out ReadOnlySequence<byte> _, NewlineCharacters, advancePastDelimiter: true))
            {
                lineWithNewlineCharacter = buffer.Slice(0, reader.Position);
                buffer = buffer.Slice(reader.Position);
                return true;
            }

            lineWithNewlineCharacter = default;
            return false;
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
