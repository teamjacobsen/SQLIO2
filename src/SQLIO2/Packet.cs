using System;
using System.Net.Sockets;

namespace SQLIO2
{
    class Packet
    {
        public IServiceProvider ServiceProvider { get; }
        public TcpClient Client { get; }
        public byte[] Raw { get; }

        public Packet(IServiceProvider serviceProvider, TcpClient client, byte[] raw)
        {
            ServiceProvider = serviceProvider;
            Client = client;
            Raw = raw;
        }
    }
}
