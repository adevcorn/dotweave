using System;

namespace dotweave;

/// <summary>
/// Marks a method to emit OpenTelemetry metrics via generated interceptors.
/// By default, emits a calls counter and a duration histogram.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MeasuredAttribute : Attribute
{
    /// <summary>
    /// Optional custom metric base name. If not specified, "TypeName.MethodName" is used.
    /// </summary>
    public string? MetricName { get; }

    /// <summary>
    /// Whether to emit a calls counter ({name}.calls). Default: true.
    /// </summary>
    public bool Calls { get; set; } = true;

    /// <summary>
    /// Whether to emit a duration histogram ({name}.duration). Default: true.
    /// </summary>
    public bool Duration { get; set; } = true;

    /// <summary>
    /// Whether to emit an in-flight UpDownCounter ({name}.inflight) that tracks
    /// concurrent executions. Particularly useful for async methods. Default: false.
    /// </summary>
    public bool InFlight { get; set; } = false;

    /// <summary>
    /// Custom tags as "key=value" pairs attached to all metric recordings.
    /// Example: Tags = new[] { "endpoint=greeting", "tier=free" }
    /// </summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// Name of a static method that accepts the return value and returns bool.
    /// When set, the generator calls this predicate to determine whether the
    /// result is an error (true → status="error", false → status="ok").
    /// Exceptions still always record status="error" regardless of this setting.
    /// The predicate must be a static method on the same class as the attributed method.
    /// Example: ErrorWhen = nameof(IsFailure)
    /// </summary>
    public string? ErrorWhen { get; set; }

    public MeasuredAttribute() { }

    public MeasuredAttribute(string metricName)
    {
        MetricName = metricName;
    }
}
