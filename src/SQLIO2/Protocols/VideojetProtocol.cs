using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SQLIO2.Protocols
{
    class VideojetProtocol : LineBasedProtocol
    {
        private static readonly byte[] StartBytes = new byte[]
        {
            (byte)'$',
            (byte)'!',
            0x06,
            0x15
        };

        private readonly ILogger _logger;

        public VideojetProtocol(RequestDelegate next, ILogger logger)
            : base(next, logger)
        {
            _logger = logger;
        }

        protected override async Task ProcessLineAsync(TcpClient client, ReadOnlySequence<byte> line)
        {
            var data = GetData(line);

            if (data is null)
            {
                _logger.LogError("Start byte not found");
                return;
            }

            if (data.Value.Length == 0)
            {
                _logger.LogWarning("Data length is zero");
                return;
            }

            var dataArray = data.Value.ToArray();
            var packet = new Packet(client, dataArray);

            _logger.LogInformation("Handling videojet protocol packet {DataAscii} from {RemoteEndpoint}", Encoding.ASCII.GetString(dataArray).Replace("\r", "\\r").Replace("\n", "\\n"), client.Client.RemoteEndPoint);

            await _stack(packet);
        }

        private ReadOnlySequence<byte>? GetData(ReadOnlySequence<byte> line)
        {
            var reader = new SequenceReader<byte>(line);

            if (!reader.TryAdvanceToAny(StartBytes, advancePastDelimiter: false))
            {
                return null;
            }

            return line.Slice(reader.Position);
        }
    }
}
