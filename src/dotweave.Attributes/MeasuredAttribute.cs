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

    public MeasuredAttribute() { }

    public MeasuredAttribute(string metricName)
    {
        MetricName = metricName;
    }
}
