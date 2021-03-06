﻿namespace SQLIO2.Middlewares
{
    class SqlServerOptions
    {
        public string ConnectionString { get; set; }
        public string StoredProcedureName { get; set; } = "SQLIO_IncomingPacket";
        public string XmlStoredProcedureName { get; set; } = "SQLIO_IncomingXML";
    }
}
