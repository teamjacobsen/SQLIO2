using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SQLIO2.Protocols
{
    class Sc500Protocol : ProtocolBase
    {
        private readonly RequestDelegate _stack;
        private readonly ILogger _logger;
        private static readonly byte[] StartBytes = Encoding.UTF8.GetBytes("<msg");
        private static readonly byte[] EndBytes = Encoding.UTF8.GetBytes("</msg>");

        public Sc500Protocol(RequestDelegate stack, ILogger logger) : base(logger)
        {
            _stack = stack;
            _logger = logger;
        }

        protected override bool TryParseMessage(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> message)
        {
            var reader = new SequenceReader<byte>(buffer);

            if (reader.TryReadTo(out ReadOnlySequence<byte> untilEndBytes, EndBytes, advancePastDelimiter: true))
            {
                var untilStartReader = new SequenceReader<byte>(untilEndBytes);

                if (untilStartReader.TryReadTo(out var _, StartBytes, advancePastDelimiter: false))
                {
                    message = reader.Sequence.Slice(untilStartReader.Position, untilStartReader.Remaining + EndBytes.Length);
                    buffer = buffer.Slice(reader.Position);
                    return true;
                }
            }

            message = default;
            return false;
        }

        protected override async Task ProcessMessageAsync(TcpClient client, ReadOnlySequence<byte> message)
        {
            var raw = message.ToArray();
            var xml = new XmlDocument();
            xml.LoadXml(Encoding.UTF8.GetString(raw));

            var packet = new Packet(client, raw, xml);

            _logger.LogInformation("Handling sc500 protocol packet {Xml} from {RemoteEndpoint}", xml.OuterXml, client.Client.RemoteEndPoint);

            await _stack(packet);
        }
    }
}
