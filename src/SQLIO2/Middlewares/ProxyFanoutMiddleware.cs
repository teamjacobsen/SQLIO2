using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace SQLIO2.Middlewares
{
    class ProxyFanoutMiddleware : IMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ProxyService _proxyService;
        private readonly ILogger<ProxyFanoutMiddleware> _logger;

        public ProxyFanoutMiddleware(RequestDelegate next, ProxyService proxyService, ILogger<ProxyFanoutMiddleware> logger)
        {
            _next = next;
            _proxyService = proxyService;
            _logger = logger;
        }

        public async Task HandleAsync(Packet packet)
        {
            var recipientCount = await _proxyService.FanoutAsync(packet.Raw);

            _logger.LogInformation("Fanout completed to {RecipientCount} recipients", recipientCount);

            await _next(packet);
        }
    }
}
