using System.Diagnostics;
using Banking.Infrastructure.Messaging;
using Serilog.Context;

namespace Banking.Api.Middleware;

// X-Correlation-Id header'dan alınır veya üretilir; log/trace/baggage'a işlenip outbox ve kuyruğa kadar taşınır
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(HeaderName, out var supplied)
            && !string.IsNullOrWhiteSpace(supplied)
                ? supplied.ToString()
                : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;
        Activity.Current?.SetTag("banking.correlation_id", correlationId);
        Activity.Current?.AddBaggage(MessagingDiagnostics.CorrelationBaggageKey, correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
