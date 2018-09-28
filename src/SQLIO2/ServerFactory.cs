using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SQLIO2
{
    class ServerFactory
    {
        private readonly IServiceProvider _services;

        public ServerFactory(IServiceProvider services)
        {
            _services = services;
        }

        public Server Create(IPEndPoint endpoint, Func<TcpClient, Task> acceptHandler)
        {
            return ActivatorUtilities.CreateInstance<Server>(_services, endpoint, acceptHandler);
        }
    }
}
