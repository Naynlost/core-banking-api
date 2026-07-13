using System.Diagnostics;

namespace Banking.Infrastructure.Messaging;

/// <summary>
/// Telemetry instruments and propagation keys for the messaging layer. Publish
/// and consume spans are children of the trace stored on the outbox row, so a
/// single trace covers request → handler → database → queue → consumer even
/// though publication is asynchronous.
/// </summary>
public static class MessagingDiagnostics
{
    public const string ActivitySourceName = "Banking.Messaging";

    /// <summary>Baggage key the API middleware sets; flows into outbox rows and AMQP headers.</summary>
    public const string CorrelationBaggageKey = "correlation_id";

    /// <summary>AMQP header carrying the W3C trace context.</summary>
    public const string TraceParentHeader = "traceparent";

    /// <summary>AMQP header carrying the originating request's correlation id.</summary>
    public const string CorrelationIdHeader = "correlation_id";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>Parses a stored W3C traceparent into a context usable as a span parent.</summary>
    internal static ActivityContext ParseTraceParent(string? traceParent) =>
        ActivityContext.TryParse(traceParent, traceState: null, isRemote: true, out var context)
            ? context
            : default;
}
