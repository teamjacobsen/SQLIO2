using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Net.Sockets;
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

        protected readonly RequestDelegate _stack;
        private readonly ILogger _logger;

        public LineBasedProtocol(RequestDelegate stack, ILogger logger) : base(logger)
        {
            _stack = stack;
            _logger = logger;
        }

        protected override bool TryParseMessage(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> message)
        {
            var reader = new SequenceReader<byte>(buffer);

            if (reader.TryReadToAny(out ReadOnlySequence<byte> _, NewlineCharacters, advancePastDelimiter: true))
            {
                message = buffer.Slice(0, reader.Position);
                buffer = buffer.Slice(reader.Position);
                return true;
            }

            message = default;
            return false;
        }

        protected override Task ProcessMessageAsync(TcpClient client, ReadOnlySequence<byte> message)
        {
            _logger.LogInformation("Found line with length {LineLength}, including newline character: {LineHex}", message.Length, BitConverter.ToString(message.ToArray()).Replace("-", string.Empty));

            return ProcessLineAsync(client, message);
        }

        protected abstract Task ProcessLineAsync(TcpClient client, ReadOnlySequence<byte> line);
    }
}
