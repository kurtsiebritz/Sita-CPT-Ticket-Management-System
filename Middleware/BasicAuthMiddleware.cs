using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading.Tasks;

namespace SitaCptTicketApp.Middleware
{
    public class BasicAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BasicAuthMiddleware> _logger;

        public BasicAuthMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<BasicAuthMiddleware> logger)
        {
            _next = next;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/Login"))
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                context.Response.Headers["WWW-Authenticate"] = "Basic";
                context.Response.StatusCode = 401;
                return;
            }

            try
            {
                var authHeader = context.Request.Headers["Authorization"].ToString();
                if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                {
                    var encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
                    var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encodedCredentials));
                    var parts = credentials.Split(':', 2);
                    var username = parts[0];
                    var password = parts.Length > 1 ? parts[1] : "";

                    var expectedUsername = _configuration["SharedLogin:Username"];
                    var expectedPassword = _configuration["SharedLogin:Password"];

                    if (username == expectedUsername && password == expectedPassword)
                    {
                        await _next(context);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Authentication error: {ex.Message}");
            }

            context.Response.Headers["WWW-Authenticate"] = "Basic";
            context.Response.StatusCode = 401;
        }
    }
}