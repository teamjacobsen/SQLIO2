using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SQLIO2
{
    public class Server
    {
        private readonly TcpListener _listener;
        private CancellationTokenSource _cts;
        private TaskCompletionSource<object> _tcs;

        public Server(IPEndPoint endpoint)
        {
            _listener = new TcpListener(endpoint);
        }

        public Task StartListeningAsync()
        {
            if (_cts != null)
            {
                throw new InvalidOperationException();
            }

            _listener.Start();

            _cts = new CancellationTokenSource();
            _cts.Token.Register(() => _listener.Stop());

            _tcs = new TaskCompletionSource<object>();

            _ = Task.Run(AcceptConnectionsAsync);

            return Task.CompletedTask;
        }

        public Task StopListeningAsync()
        {
            if (_cts == null)
            {
                throw new InvalidOperationException();
            }

            _cts.Cancel();

            return _tcs.Task;
        }

        private async Task AcceptConnectionsAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();

                    _ = Task.Run(() => new Scanner().ProcessAsync(client.Client));
                }
                catch (Exception) when (_cts.IsCancellationRequested)
                {
                }
            }

            _tcs.SetResult(null);
        }
    }
}
