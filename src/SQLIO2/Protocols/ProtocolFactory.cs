using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SQLIO2.Protocols
{
    class ProtocolFactory
    {
        private readonly ILogger _logger;

        public ProtocolFactory(ILogger logger)
        {
            _logger = logger;
        }

        public ProtocolFactory(ILogger<ProtocolFactory> logger)
        {
            _logger = logger;
        }

        public Func<TcpClient, CancellationToken, Task> Create(string name, RequestDelegate stack)
        {
            if (name?.Equals("videojet", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new VideojetProtocol(stack, _logger).ProcessAsync;
            }
            else if (name?.Equals("sc500", StringComparison.OrdinalIgnoreCase) == true)
            {
                return new Sc500Protocol(stack, _logger).ProcessAsync;
            }
            else
            {
                return new DefaultProtocol(stack, _logger).ProcessAsync;
            }
        }
    }
}
