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
            if (!Context.User.IsInRole(Strings.System))
            {
                var connections = await _reliableStateManager.GetOrAddAsync<IReliableDictionary<string, string>>("Connections");

                using (ITransaction tx = _reliableStateManager.CreateTransaction()) {
                    var success = await connections.TryAddAsync(tx, Context.UserIdentifier, Context.ConnectionId);
                    await tx.CommitAsync();

                    if(!success)
                    {
                        Context.Abort();
                        return;
                    }

                    await Groups.AddToGroupAsync(Context.ConnectionId, TenantId);

                    await Clients.Group(TenantId).ShowUsersOnLine((await GetConnectionsDictionary(connections)).Where(x => x.Key.StartsWith(TenantId)).Count());

                    await Clients.Caller.ConnectionId(Context.ConnectionId);

                    await Clients.Group(Strings.System).ConnectionsChanged(await GetConnectionsDictionary(connections));
                }
            }
            else
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, Strings.System);
            }
            await base.OnConnectedAsync();
        }
        
        public async Task<Dictionary<string,string>> GetConnectionsDictionary(IReliableDictionary<string, string> connections = null)
        {
            connections = connections ?? await _reliableStateManager.GetOrAddAsync<IReliableDictionary<string, string>>("Connections");

            using (ITransaction tx = _reliableStateManager.CreateTransaction())
            {
                var list = await connections.CreateEnumerableAsync(tx);

                var enumerator = list.GetAsyncEnumerator();

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

            var reliableDictionary = await _reliableStateManager.GetOrAddAsync<IReliableDictionary<string, string>>("Connections");

            if (!Context.User.IsInRole(Strings.System) && (await TryToRemoveConnectedUser(Context.UserIdentifier, Context.ConnectionId,reliableDictionary)))
            {
                var connections = await GetConnectionsDictionary(reliableDictionary);

                await Clients.All.ShowUsersOnLine(connections.Count());
                
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, TenantId);

                await Clients.Group(TenantId).ShowUsersOnLine(connections.Where(x => x.Key.StartsWith(TenantId)).Count());

                await Clients.Group(Strings.System).ConnectionsChanged(connections);
            }            

            if(Context.User.IsInRole(Strings.System))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, Strings.System);
        } 
        
        public async Task<bool> TryToRemoveConnectedUser(string uniqueIdentifier, string connectionId, IReliableDictionary<string,string> connections)
        {
            var result = false;
            
            using (var tx = _reliableStateManager.CreateTransaction())
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
