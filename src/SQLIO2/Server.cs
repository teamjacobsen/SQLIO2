using Microsoft.Extensions.Logging;
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
        private readonly Func<TcpClient, Task> _acceptHandler;
        private readonly ILogger<Server> _logger;
        private CancellationTokenSource _cts;
        private TaskCompletionSource<object> _tcs;

        public EndPoint LocalEndpoint => _listener.LocalEndpoint;

        public Server(IPEndPoint endpoint, Func<TcpClient, Task> acceptHandler, ILogger<Server> logger)
        {
            _listener = new TcpListener(endpoint);
            _acceptHandler = acceptHandler;
            _logger = logger;
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

                    _logger.LogInformation("Accepting client {RemoteEndpoint} on {LocalEndpoint}", client.Client.RemoteEndPoint, client.Client.LocalEndPoint);

                    _ = Task.Run(() => _acceptHandler(client));
                }
                catch (Exception) when (_cts.IsCancellationRequested)
                {
                }
            }

            _tcs.SetResult(null);
        }
    }
}
