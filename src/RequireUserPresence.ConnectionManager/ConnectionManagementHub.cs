using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace RequireUserPresence.ConnectionManager
{
    public interface IConnectionManagementHub
    {
        Task ShowUsersOnLine(int count);
        Task Result(string result);
        Task ConnectedUsersChanged(string[] connectedUsers);
    }

    [Authorize(AuthenticationSchemes = "Bearer")]
    public class ConnectionManagementHub: Hub<IConnectionManagementHub> {
        
        public static ConcurrentDictionary<string, byte> Users  = new ConcurrentDictionary<string, byte>();
        
        private readonly ILogger<ConnectionManagementHub> _logger;

        public ConnectionManagementHub(ILogger<ConnectionManagementHub> logger)
            => _logger = logger;

        public override async Task OnConnectedAsync()
        {            
            if (Context.UserIdentifier != "System" && !Users.TryAdd(Context.UserIdentifier, 0))
                throw new Exception("User is already connected");

            var tenantId = Context.User.FindFirst("TenantId")?.Value;

            if (!string.IsNullOrEmpty(tenantId)) {

                await Groups.AddToGroupAsync(Context.ConnectionId, Context.User.FindFirst("TenantId").Value);

                await Clients.Group(tenantId).ShowUsersOnLine(Users.Where(x => x.Key.StartsWith(tenantId)).Count());
            }

            await Clients.User("System").ConnectedUsersChanged(Users.Select(x => x.Key).ToArray());

            await base.OnConnectedAsync();
        }

        public async Task SendResult(string uniqueIdentifier, string result)
            => await Clients.User(uniqueIdentifier).Result(result);

        public override async Task OnDisconnectedAsync(Exception exception)
        {            
            Users.TryRemove(Context.UserIdentifier, out _);

            await Clients.All.ShowUsersOnLine(Users.Count);

            var tenantId = Context.User.FindFirst("TenantId")?.Value;

            if (!string.IsNullOrEmpty(tenantId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, Context.User.FindFirst("TenantId").Value);

                await Clients.Group(tenantId).ShowUsersOnLine(Users.Where(x => x.Key.StartsWith(tenantId)).Count());
            }

            await Clients.User("System").ConnectedUsersChanged(Users.Select(x => x.Key).ToArray());

            await base.OnDisconnectedAsync(exception);
        }        
    }
}
