using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Net.Sockets;

namespace SQLIO2.Protocols
{
    class DefaultProtocol : LineBasedProtocol
    {
        private const byte StartByte = (byte)'@';

        private readonly ILogger _logger;

        public DefaultProtocol(RequestDelegate next, ILogger logger)
            : base(next, logger)
        {
            _logger = logger;
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
                else
                {
                    _logger.LogWarning("Data length is zero");
                }
            }
            else
            {
                _logger.LogError("Start byte not found");
            }
        }
    }
}
