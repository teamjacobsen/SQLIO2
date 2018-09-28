using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SQLIO2
{
    class StackBuilder : IStackBuilder
    {
        private readonly List<Func<RequestDelegate, RequestDelegate>> _layers = new List<Func<RequestDelegate, RequestDelegate>>();

        public IServiceProvider ApplicationServices { get; }

        public StackBuilder(IServiceProvider services)
        {
            ApplicationServices = services;
        }

        public IStackBuilder Use(Func<RequestDelegate, RequestDelegate> layer)
        {
            _layers.Add(layer);

            return this;
        }

        public RequestDelegate Build()
        {
            RequestDelegate stack = (_) => Task.CompletedTask;

            _layers.Reverse();

            foreach (var layer in _layers)
            {
                stack = layer(stack);
            }

            return stack;
        }
    }
}
