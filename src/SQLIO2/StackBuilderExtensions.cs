using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace SQLIO2
{
    static class StackBuilderExtensions
    {
        public static IStackBuilder Use<TMiddleware>(this IStackBuilder builder, params object[] args) where TMiddleware : IMiddleware
        {
            return builder.Use(typeof(TMiddleware), args);
        }

        public static IStackBuilder Use(this IStackBuilder builder, Type middlewareType, params object[] args)
        {
            return builder.Use(next =>
            {
                var ctorArgs = new List<object>();
                ctorArgs.Add(next);
                ctorArgs.AddRange(args);

                var middleware = (IMiddleware)ActivatorUtilities.CreateInstance(builder.ApplicationServices, middlewareType, ctorArgs.ToArray());

                return middleware.HandleAsync;
            });
        }
    }
}
