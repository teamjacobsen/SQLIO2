using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net.Sockets;

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

        public VideojetProtocol(RequestDelegate next, IServiceScopeFactory serviceScopeFactory, ILogger<VideojetProtocol> logger)
            : base(next, serviceScopeFactory, logger)
        {
        }

        protected override void ProcessLine(TcpClient client, ReadOnlySequence<byte> line)
        {
            var reader = new SequenceReader<byte>(line);

            if (reader.TryAdvanceToAny(StartBytes, advancePastDelimiter: false))
            {
                var data = line.Slice(reader.Position);

                RunStack(client, data.ToArray());
            }
        }
    }
}
