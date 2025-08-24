using System.Collections.Concurrent;

namespace ApiCausality360.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private static readonly ConcurrentDictionary<string, DateTime> _clients = new();

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Solo aplicar a endpoint costoso
            if (context.Request.Path.StartsWithSegments("/api/events/process-today-news"))
            {
                var clientIp = GetClientIpAddress(context);
                var now = DateTime.UtcNow;

                if (_clients.TryGetValue(clientIp, out var lastCall))
                {
                    var timeDifference = now - lastCall;
                    
                    // Máximo 1 llamada cada 30 minutos
                    if (timeDifference < TimeSpan.FromMinutes(30))
                    {
                        _logger.LogWarning($" Rate limit exceeded for IP: {clientIp}");
                        context.Response.StatusCode = 429;
                        await context.Response.WriteAsync($"{{\"error\": \"Rate limit exceeded. Try again in {30 - (int)timeDifference.TotalMinutes} minutes.\"}}");
                        return;
                    }
                }

                _clients.AddOrUpdate(clientIp, now, (key, value) => now);
            }

            await _next(context);
        }

        private static string GetClientIpAddress(HttpContext context)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1")
                ipAddress = "127.0.0.1";
            return ipAddress;
        }
    }
}