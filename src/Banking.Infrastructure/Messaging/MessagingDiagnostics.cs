using System.Diagnostics;

namespace Banking.Infrastructure.Messaging;

// Publish/consume span'leri outbox satırındaki trace'in çocuğu olur, publish asenkron olsa da tek trace kalır
public static class MessagingDiagnostics
{
    public const string ActivitySourceName = "Banking.Messaging";

    public const string CorrelationBaggageKey = "correlation_id";

    public const string TraceParentHeader = "traceparent";

    public const string CorrelationIdHeader = "correlation_id";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    internal static ActivityContext ParseTraceParent(string? traceParent) =>
        ActivityContext.TryParse(traceParent, traceState: null, isRemote: true, out var context)
            ? context
            : default;
}
