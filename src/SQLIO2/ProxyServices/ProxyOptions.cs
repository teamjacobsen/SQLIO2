namespace SQLIO2.ProxyServices
{
    class ProxyOptions
    {
        public int ListenPort { get; set; }
        public int FanoutPort { get; set; }

        public string RemoteHost { get; set; }
        public int RemotePort { get; set; }
        public int ChatPort { get; set; }

        public string ProtocolName { get; set; }
    }
}
