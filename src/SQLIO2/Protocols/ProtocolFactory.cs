using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SQLIO2.Protocols
{
    class ProtocolFactory
    {
        private readonly IServiceProvider _services;

        public ProtocolFactory(IServiceProvider services)
        {
            _services = services;
        }

        public Func<TcpClient, CancellationToken, Task> Create(string name, RequestDelegate stack)
        {
            if (name?.Equals("videojet", StringComparison.OrdinalIgnoreCase) == true)
            {
                return ActivatorUtilities.CreateInstance<VideojetProtocol>(_services, stack).ProcessAsync;
            }
            else if (name?.Equals("sc500", StringComparison.OrdinalIgnoreCase) == true)
            {
                return ActivatorUtilities.CreateInstance<Sc500Protocol>(_services, stack).ProcessAsync;
            }
            else
            {
                return ActivatorUtilities.CreateInstance<DefaultProtocol>(_services, stack).ProcessAsync;
            }
        }
    }
}
