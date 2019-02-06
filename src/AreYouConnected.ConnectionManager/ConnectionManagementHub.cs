using AreYouConnected.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AreYouConnected.ConnectionManager
{
    public interface IConnectionManagementHub
    {
        Task ShowUsersOnLine(int count);
        Task Result(string result);
        Task ConnectionId(string connectionId);
        Task ConnectionsChanged(IDictionary<string,string> connections);
    }

    [Authorize(AuthenticationSchemes = "Bearer")]
    public class ConnectionManagementHub: Hub<IConnectionManagementHub> {
        
        private readonly IReliableStateManager _reliableStateManager;

        private readonly ILogger<ConnectionManagementHub> _logger;
        
        public ConnectionManagementHub(ILogger<ConnectionManagementHub> logger, IReliableStateManager reliableStateManager)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _reliableStateManager = reliableStateManager ?? throw new ArgumentNullException(nameof(reliableStateManager));
        }
            

        public override async Task OnConnectedAsync()
        {            
            if (!Context.User.IsInRole("System"))
            {
                var connections = await this._reliableStateManager.GetOrAddAsync<IReliableDictionary<string, string>>("Connections");

                using (ITransaction tx = this._reliableStateManager.CreateTransaction()) {
                    var success = await connections.TryAddAsync(tx, Context.UserIdentifier, Context.ConnectionId);
                    await tx.CommitAsync();

                    if(!success)
                    {
                        Context.Abort();
                        return;
                    }

                    await Groups.AddToGroupAsync(Context.ConnectionId, TenantId);

                    await Clients.Group(TenantId).ShowUsersOnLine((await GetConnectionsDictionary()).Where(x => x.Key.StartsWith(TenantId)).Count());

                    await Clients.Caller.ConnectionId(Context.ConnectionId);

                    await Clients.Group("System").ConnectionsChanged(await GetConnectionsDictionary());
                }
            }
            else
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "System");
            }
            await base.OnConnectedAsync();
        }
        
        public async Task<Dictionary<string,string>> GetConnectionsDictionary()
        {
            var connections = await this._reliableStateManager.GetOrAddAsync<IReliableDictionary<string, string>>("Connections");

            using (ITransaction tx = this._reliableStateManager.CreateTransaction())
            {
                Microsoft.ServiceFabric.Data.IAsyncEnumerable<KeyValuePair<string, string>> list = await connections.CreateEnumerableAsync(tx);

                Microsoft.ServiceFabric.Data.IAsyncEnumerator<KeyValuePair<string, string>> enumerator = list.GetAsyncEnumerator();

                var result = new Dictionary<string, string>();

                while (await enumerator.MoveNextAsync(default(CancellationToken)))
                {
                    result.TryAdd(enumerator.Current.Key, enumerator.Current.Value);
                }

                return result;
            }
            
        }

        [Authorize(Roles = "System")]
        public async Task SendResult(SendResultRequest request)
            => await Clients.User(request.UserId).Result(request.Result);

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            await base.OnDisconnectedAsync(exception);

            if (!Context.User.IsInRole("System") && (await TryToRemoveConnectedUser(Context.UserIdentifier, Context.ConnectionId)))
            {
                var connections = await GetConnectionsDictionary();

                await Clients.All.ShowUsersOnLine(connections.Count());
                
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, TenantId);

                await Clients.Group(TenantId).ShowUsersOnLine(connections.Where(x => x.Key.StartsWith(TenantId)).Count());

                await Clients.Group("System").ConnectionsChanged(connections);
            }            

            if(Context.User.IsInRole("System"))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "System");
        } 
        
        public async Task<bool> TryToRemoveConnectedUser(string uniqueIdentifier, string connectionId)
        {
            var result = false;
            var connections = await this._reliableStateManager.GetOrAddAsync<IReliableDictionary<string, string>>("Connections");

            using (ITransaction tx = this._reliableStateManager.CreateTransaction())
            {
                var connectionEntryId = await connections.TryGetValueAsync(tx, uniqueIdentifier);

                if (!string.IsNullOrEmpty(connectionEntryId.Value) && connectionEntryId.Value == connectionId)
                {
                    await connections.TryRemoveAsync(tx, uniqueIdentifier);
                    await tx.CommitAsync();
                    result = true;
                }
            }

            return result;
        }

        public string TenantId { get => Context.User?.FindFirst("TenantId")?.Value; }
    }

    public class UniqueIdentifierUserIdProvider : IUserIdProvider
    {
        public string GetUserId(HubConnectionContext connection)
            => connection.User.FindFirst("UniqueIdentifier").Value;
    }
}
