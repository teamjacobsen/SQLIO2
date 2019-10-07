using System;
using System.Net.Sockets;
using System.Text;
using System.Xml;

namespace SQLIO2
{
    class Packet
    {
        public IServiceProvider ServiceProvider { get; }
        public TcpClient Client { get; }
        public byte[] Raw { get; }
        public XmlDocument Xml { get; }

        public Packet(IServiceProvider serviceProvider, TcpClient client, byte[] raw)
        {
            ServiceProvider = serviceProvider;
            Client = client;
            Raw = raw;
        }

        public Packet(IServiceProvider serviceProvider, TcpClient client, XmlDocument xml)
        {
            ServiceProvider = serviceProvider;
            Client = client;
            Xml = xml;
        }

        public override string ToString()
        {
            if (Xml is object)
            {
                return Xml.OuterXml;
            }

            var builder = new StringBuilder(Raw.Length * 2);

            foreach (var @byte in Raw)
            {
                builder.AppendFormat("{0:x2}", @byte);
            }

            return builder.ToString();
        }
    }
}
