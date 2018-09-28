using System;

namespace SQLIO2
{
    interface IStackBuilder
    {
        IServiceProvider ApplicationServices { get; }

        IStackBuilder Use(Func<RequestDelegate, RequestDelegate> layer);
        RequestDelegate Build();
    }
}
