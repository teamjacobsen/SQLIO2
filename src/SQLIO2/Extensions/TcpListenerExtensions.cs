using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SQLIO2
{
    static class TcpListenerExtensions
    {
        public static async Task<TcpClient> AcceptAsync(this TcpListener listener, CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(listener.Stop))
            {
                try
                {
                    return await listener.AcceptTcpClientAsync();
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }
        }
    }
}
