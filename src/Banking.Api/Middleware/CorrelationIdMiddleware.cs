using System.Diagnostics;
using Banking.Infrastructure.Messaging;
using Serilog.Context;

namespace Banking.Api.Middleware;

/// <summary>
/// Gives every request a correlation id: taken from the X-Correlation-Id header
/// when the caller supplies one, generated otherwise. The id is returned in the
/// response, stamped on every log line via the log context, tagged on the trace,
/// and put into activity baggage so it rides along into outbox rows and queue
/// messages — one id connects the whole story of a request.
/// </summary>
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
