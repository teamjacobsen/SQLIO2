﻿using System;
using System.Net.Sockets;
using System.Text;
using System.Xml;

namespace SQLIO2
{
    class Packet
    {
        public TcpClient Client { get; }
        public byte[] Raw { get; }
        public XmlDocument Xml { get; }

        public Packet(TcpClient client, byte[] raw)
        {
            Client = client;
            Raw = raw;
        }

        public Packet(TcpClient client, byte[] raw, XmlDocument xml)
        {
            Client = client;
            Raw = raw;
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
