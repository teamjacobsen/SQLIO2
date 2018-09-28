using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SQLIO2
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var endpoint = new IPEndPoint(IPAddress.Any, 1234);
            var server = new Server(endpoint);

            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Shutdown() => tcs.TrySetResult(null);

            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => Shutdown();
            Console.CancelKeyPress += (s, e) => {
                e.Cancel = true;

                Shutdown();
            };

            await server.StartListeningAsync();

            await tcs.Task;

            await server.StopListeningAsync();

            Console.WriteLine("Goodbye");
        }
    }
}
