using AreYouConnected.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Threading.Tasks;

namespace AreYouConnected.Api
{

    public class ActiveConnectionRequirement : IAuthorizationRequirement { }

    public class ActiveConnectionHandler: AuthorizationHandler<ActiveConnectionRequirement>
    {
        private readonly ILogger<ActiveConnectionRequirement> _logger;
        private readonly IHubService _hubService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ActiveConnectionHandler(
            IHubService hubService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ActiveConnectionRequirement> logger
            )
        {
            _hubService = hubService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }
      
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveConnectionRequirement requirement)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var isSystem = context.User.IsInRole(Strings.System);

            if ((!isSystem && IsConnected(httpContext)) || isSystem)
                context.Succeed(requirement);

            await Task.CompletedTask;
        }

        private bool IsConnected(HttpContext httpContext)
        {
            var uniqueIdentifier = httpContext.User.FindFirst(Strings.UniqueIdentifier).Value;

            httpContext.Request.Headers.TryGetValue(Strings.ConnectionId, out StringValues connectionId);

            return _hubService.IsConnected(uniqueIdentifier, connectionId);
        }
    }
}
