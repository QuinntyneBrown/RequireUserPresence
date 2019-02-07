using AreYouConnected.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace AreYouConnected.Api
{
    public class ConnectionGuardMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHubService _hubService;

        public ConnectionGuardMiddleware(RequestDelegate next, IHubService hubService)
        {
            _next = next;
            _hubService = hubService;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var isAuthenticated = httpContext.User.Identity.IsAuthenticated;

            if ((isAuthenticated && !httpContext.User.IsInRole(Strings.System) && IsConnected(httpContext)) 
                || (isAuthenticated && !httpContext.User.IsInRole(Strings.System))
                || !isAuthenticated)
            {
                await _next.Invoke(httpContext);
            }
            else if(true) {
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;

                await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(
                    new ProblemDetails
                    {
                        Title = "Unauthorized",
                        Type = "https://api.areyouconnected.com/errors/unauthorized",
                        Detail = "Unauthorized",
                        Status = StatusCodes.Status401Unauthorized
                    }));
            } 
        }

        private bool IsConnected(HttpContext httpContext)
        {
            var uniqueIdentifier = httpContext.User.FindFirst("UniqueIdentifier").Value;

            httpContext.Request.Headers.TryGetValue("ConnectionId", out StringValues connectionId);

            return _hubService.IsConnected(uniqueIdentifier, connectionId);
        }
    }
}
