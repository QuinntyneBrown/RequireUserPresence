using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using AreYouConnected.Core;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace AreYouConnected.Api
{
    public class Worker: BackgroundService
    {
        private readonly IHubService _hubService;
        private readonly ISecurityTokenFactory _securityTokenFactory;

        public Worker(
            IHubService hubService,
            ISecurityTokenFactory securityTokenFactory)
        {
            _hubService = hubService;
            _securityTokenFactory = securityTokenFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl("https://localhost:44337/hubs/connectionManagement", options => {
                    options.AccessTokenProvider = () => Task.FromResult(_securityTokenFactory.Create("System"));
                })
                .Build();

            connection.On<Dictionary<string,string>>("ConnectionsChanged", (connections)
                => _hubService.Connections = new ConcurrentDictionary<string, string>(connections));

            _hubService.HubConnection = connection;

            await connection.StartAsync();
        }
    }
}
