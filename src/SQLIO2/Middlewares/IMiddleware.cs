using System.Threading.Tasks;

namespace SQLIO2
{
    interface IMiddleware
    {
        Task HandleAsync(Packet packet);
    }
}
