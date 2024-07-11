using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace NotesHubApi
{
    public interface ISignalRService
    {
        Task NotifyClientsAsync(string method, object message);
    }

    public class SignalRService : ISignalRService
    {
        private readonly IHubContext<Hub> _hubContext;
        private readonly ILogger<SignalRService> _logger;

        public SignalRService(IHubContext<Hub> hubContext, ILogger<SignalRService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyClientsAsync(string method, object message)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync(method, message);
                _logger.LogInformation("Notified clients with method: {Method}. Message: {@Message}", method, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify clients with method: {Method}. Message: {@Message}", method, message);
                throw;
            }
        }
    }
}
