using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net.Sockets;

namespace SQLIO2.Protocols
{
    class DefaultProtocol : LineBasedProtocol
    {
        private const byte StartByte = (byte)'@';

        public DefaultProtocol(RequestDelegate next, IServiceScopeFactory serviceScopeFactory, ILogger<DefaultProtocol> logger)
            : base(next, serviceScopeFactory, logger)
        {
        }

        protected override void ProcessLine(TcpClient client, ReadOnlySequence<byte> line)
        {
            var position = line.PositionOf(StartByte);

            if (position != null)
            {
                var data = line.Slice(position.Value);
                
                if (data.Length > 0)
                {
                    RunStack(client, data.ToArray());
                }
            }
        }
    }
}
