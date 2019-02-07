using AreYouConnected.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
            _hubService = hubService ?? throw new ArgumentNullException(nameof(hubService));
            _securityTokenFactory = securityTokenFactory ?? throw new ArgumentNullException(nameof(securityTokenFactory));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl("https://localhost:44337/hubs/connectionManagement", options => {
                    options.AccessTokenProvider = () => Task.FromResult(_securityTokenFactory.Create(Strings.System));
                })
                .Build();

            connection.On<Dictionary<string,string>>(Strings.ConnectionsChanged, connections
                => _hubService.Connections = new ConcurrentDictionary<string, string>(connections));

            _hubService.HubConnection = connection;

            await connection.StartAsync();
        }
    }
}
