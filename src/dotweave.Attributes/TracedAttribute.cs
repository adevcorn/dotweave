using System;

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

    public TracedAttribute() { }

    public TracedAttribute(string spanName)
    {
        SpanName = spanName;
    }
}
