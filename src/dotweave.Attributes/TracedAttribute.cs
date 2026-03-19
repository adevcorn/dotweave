using System;
using System.Diagnostics;

namespace dotweave;

/// <summary>
/// Marks a method to be automatically traced with OpenTelemetry.
/// When applied, a source generator will intercept calls to this method
/// and wrap them in an Activity (OTel span).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class TracedAttribute : Attribute
{
    /// <summary>
    /// Optional custom span name. If not specified, "TypeName.MethodName" is used.
    /// </summary>
    public string? SpanName { get; }

    /// <summary>
    /// The kind of the span. Defaults to <see cref="ActivityKind.Internal"/>.
    /// </summary>
    public ActivityKind Kind { get; set; } = ActivityKind.Internal;

    /// <summary>
    /// Name of a static method that accepts the return value and returns bool.
    /// When set, the generator calls this predicate to determine whether the
    /// result is an error (true → span status Error, false → span status unchanged).
    /// Exceptions still always set status Error regardless of this setting.
    /// The predicate must be a static method on the same class as the attributed method.
    /// Example: ErrorWhen = nameof(IsFailure)
    /// </summary>
    public string? ErrorWhen { get; set; }

    public TracedAttribute() { }

    public TracedAttribute(string spanName)
    {
        SpanName = spanName;
    }
}
