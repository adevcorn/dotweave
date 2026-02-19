using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace dotweave.Generator;

[Generator]
public sealed class TracedInterceptorGenerator : IIncrementalGenerator
{
    private const string TracedAttributeFqn = "dotweave.TracedAttribute";
    private const string MeasuredAttributeFqn = "dotweave.MeasuredAttribute";

    private static readonly DiagnosticDescriptor GenericMethodNotSupported = new(
        id: "OTEL001",
        title: "Generic methods are not supported by dotweave interceptors",
        messageFormat: "Method '{0}' is generic and cannot be intercepted by dotweave. Remove [Traced]/[Measured] or make the method non-generic.",
        category: "dotweave",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnsupportedRefStructParam = new(
        id: "OTEL002",
        title: "Methods with ref struct parameters are not supported by dotweave interceptors",
        messageFormat: "Method '{0}' has a ref struct parameter '{1}' and cannot be intercepted by dotweave async wrappers. Remove [Traced]/[Measured] or avoid ref struct parameters on async methods.",
        category: "dotweave",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidErrorWhenPredicate = new(
        id: "OTEL003",
        title: "ErrorWhen predicate method not found or invalid",
        messageFormat: "ErrorWhen predicate '{0}' on method '{1}' was not resolved. The predicate must be a static method on the same type that returns bool and accepts a single parameter matching the method's return type.",
        category: "dotweave",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Phase 1: Collect the set of method names that carry [Traced] or [Measured].
        // ForAttributeWithMetadataName is far cheaper than scanning every invocation.
        // We collect just the method names for a syntactic pre-filter on invocations.
        var tracedMethodNames = context.SyntaxProvider.ForAttributeWithMetadataName(
            TracedAttributeFqn,
            predicate: static (node, _) => node is MethodDeclarationSyntax,
            transform: static (ctx, _) => GetMethodName(ctx))
            .Where(static k => k is not null)
            .Select(static (k, _) => k!);

        var measuredMethodNames = context.SyntaxProvider.ForAttributeWithMetadataName(
            MeasuredAttributeFqn,
            predicate: static (node, _) => node is MethodDeclarationSyntax,
            transform: static (ctx, _) => GetMethodName(ctx))
            .Where(static k => k is not null)
            .Select(static (k, _) => k!);

        var attributedMethodNames = tracedMethodNames.Collect()
            .Combine(measuredMethodNames.Collect())
            .Select(static (pair, _) =>
                pair.Left.Concat(pair.Right).ToImmutableHashSet());

        // Phase 2: Scan invocations, using the attributed method names as a
        // cheap syntactic pre-filter so we only call GetSymbolInfo on calls
        // whose method name matches an attributed method.
        var invocations = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) =>
            {
                if (node is not InvocationExpressionSyntax invocation)
                    return false;

                // Syntactic pre-filter: only consider member access or simple name calls
                return invocation.Expression is MemberAccessExpressionSyntax or IdentifierNameSyntax;
            },
            transform: static (ctx, ct) => TransformInvocation(ctx, ct))
            .Where(static c => c is not null)
            .Select(static (c, _) => c!.Value);

        // Combine invocations with attributed method names to filter out
        // invocations that don't target an attributed method name.
        var filteredInvocations = invocations.Collect()
            .Combine(attributedMethodNames)
            .Select(static (pair, _) =>
            {
                var candidates = pair.Left;
                var methodNames = pair.Right;
                return candidates
                    .Where(c => methodNames.Contains(c.MethodName))
                    .ToImmutableArray();
            });

        context.RegisterSourceOutput(filteredInvocations, static (spc, candidates) =>
        {
            if (candidates.IsEmpty)
                return;

            // Report diagnostics and filter out unsupported methods
            var valid = new List<InvocationInfo>();
            foreach (var c in candidates)
            {
                if (c.IsGenericMethod)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        GenericMethodNotSupported,
                        Location.Create(c.FilePath,
                            default,
                            new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                                new Microsoft.CodeAnalysis.Text.LinePosition(c.Line - 1, c.Column - 1),
                                new Microsoft.CodeAnalysis.Text.LinePosition(c.Line - 1, c.Column - 1))),
                        c.MethodName));
                    continue;
                }

                // Report OTEL002 for async methods with ref struct parameters
                if (c.IsAsync && c.HasRefStructParam)
                {
                    var refStructParamName = c.Parameters.First(p => p.IsRefStruct).Name;
                    spc.ReportDiagnostic(Diagnostic.Create(
                        UnsupportedRefStructParam,
                        Location.Create(c.FilePath,
                            default,
                            new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                                new Microsoft.CodeAnalysis.Text.LinePosition(c.Line - 1, c.Column - 1),
                                new Microsoft.CodeAnalysis.Text.LinePosition(c.Line - 1, c.Column - 1))),
                        c.MethodName, refStructParamName));
                    continue;
                }

                // Report OTEL003 when ErrorWhen was specified but the predicate could not be resolved
                if (c.ErrorWhenMethodName is not null && c.ErrorWhenPredicate is null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        InvalidErrorWhenPredicate,
                        Location.Create(c.FilePath,
                            default,
                            new Microsoft.CodeAnalysis.Text.LinePositionSpan(
                                new Microsoft.CodeAnalysis.Text.LinePosition(c.Line - 1, c.Column - 1),
                                new Microsoft.CodeAnalysis.Text.LinePosition(c.Line - 1, c.Column - 1))),
                        c.ErrorWhenMethodName, c.MethodName));
                    // Don't skip the method — it still compiles, just falls back to default status="ok"
                }

                valid.Add(c);
            }

            if (valid.Count == 0)
                return;

            // Deduplicate by (FilePath, Line, Column) to prevent duplicate InterceptsLocation
            var deduplicated = valid
                .GroupBy(c => (c.FilePath, c.Line, c.Column))
                .Select(g => g.First())
                .ToImmutableArray();

            var source = Emitter.Generate(deduplicated);
            spc.AddSource("TracedInterceptors.g.cs", source);
        });
    }

    /// <summary>
    /// Extracts just the method name for the syntactic pre-filter.
    /// </summary>
    private static string? GetMethodName(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is IMethodSymbol method)
            return method.Name;
        return null;
    }

    private static InvocationInfo? TransformInvocation(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;
        var symbolInfo = ctx.SemanticModel.GetSymbolInfo(invocation, ct);

        if (symbolInfo.Symbol is not IMethodSymbol method)
            return null;

        var tracedAttr = method.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == TracedAttributeFqn);
        var measuredAttr = method.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.ToDisplayString() == MeasuredAttributeFqn);

        if (tracedAttr is null && measuredAttr is null)
            return null;

        // Get the location of the method name in the invocation expression.
        var expressionSyntax = invocation.Expression;
        SyntaxNode targetNode = expressionSyntax;
        if (expressionSyntax is MemberAccessExpressionSyntax memberAccess)
            targetNode = memberAccess.Name;

        var location = targetNode.GetLocation();
        var lineSpan = location.GetLineSpan();

        string? customSpanName = null;
        if (tracedAttr is not null &&
            tracedAttr.ConstructorArguments.Length > 0 &&
            tracedAttr.ConstructorArguments[0].Value is string s)
        {
            customSpanName = s;
        }

        string? customMetricName = null;
        // Metric config defaults
        bool emitCalls = true;
        bool emitDuration = true;
        bool emitInFlight = false;
        string? errorWhenPredicate = null;
        string? errorWhenMethodName = null;
        ImmutableArray<string> customTags = ImmutableArray<string>.Empty;

        if (measuredAttr is not null)
        {
            // Read positional constructor arg (MetricName)
            if (measuredAttr.ConstructorArguments.Length > 0 &&
                measuredAttr.ConstructorArguments[0].Value is string m)
            {
                customMetricName = m;
            }

            // Read named properties: Calls, Duration, InFlight, Tags
            foreach (var named in measuredAttr.NamedArguments)
            {
                switch (named.Key)
                {
                    case "Calls" when named.Value.Value is bool bCalls:
                        emitCalls = bCalls;
                        break;
                    case "Duration" when named.Value.Value is bool bDuration:
                        emitDuration = bDuration;
                        break;
                    case "InFlight" when named.Value.Value is bool bInFlight:
                        emitInFlight = bInFlight;
                        break;
                    case "ErrorWhen" when named.Value.Value is string errorWhenName:
                        // Determine the effective return type that the predicate must accept.
                        // For Task<T>/ValueTask<T>, the predicate inspects T, not the wrapper.
                        ITypeSymbol? effectiveReturnType = null;
                        if (method.ReturnsVoid)
                        {
                            // Void methods have no result to inspect — cannot use ErrorWhen.
                        }
                        else if (IsAsyncReturn(method))
                        {
                            var namedRt = method.ReturnType as INamedTypeSymbol;
                            if (namedRt is { IsGenericType: true, TypeArguments.Length: 1 })
                                effectiveReturnType = namedRt.TypeArguments[0];
                            // else plain Task/ValueTask — no result to inspect
                        }
                        else
                        {
                            effectiveReturnType = method.ReturnType;
                        }

                        if (effectiveReturnType is not null)
                        {
                            // Resolve the predicate method on the containing type.
                            // Must be static, return bool, accept exactly one parameter
                            // whose type matches the method's effective return type.
                            var predicateMethod = method.ContainingType.GetMembers(errorWhenName)
                                .OfType<IMethodSymbol>()
                                .FirstOrDefault(m =>
                                    m.IsStatic &&
                                    m.ReturnType.SpecialType == SpecialType.System_Boolean &&
                                    m.Parameters.Length == 1 &&
                                    SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, effectiveReturnType));
                            if (predicateMethod is not null)
                            {
                                errorWhenPredicate = $"{method.ContainingType.ToDisplayString()}.{errorWhenName}";
                            }
                        }
                        errorWhenMethodName = errorWhenName;
                        break;
                    case "Tags" when !named.Value.IsNull:
                        var tagValues = named.Value.Values;
                        var builder = ImmutableArray.CreateBuilder<string>(tagValues.Length);
                        foreach (var tv in tagValues)
                        {
                            if (tv.Value is string tagStr)
                                builder.Add(tagStr);
                        }
                        customTags = builder.ToImmutable();
                        break;
                }
            }
        }

        // Detect async return type using the semantic model (OriginalDefinition)
        // instead of fragile string matching.
        var isAsync = IsAsyncReturn(method);
        var isValueTask = IsValueTaskReturn(method);

        // Detect the inner return type for generic Task<T>/ValueTask<T> using the
        // semantic model, avoiding fragile EndsWith("Task") string checks.
        var hasReturnValue = false;
        string? innerReturnType = null;
        if (isAsync)
        {
            var namedReturnType = method.ReturnType as INamedTypeSymbol;
            if (namedReturnType is { IsGenericType: true, TypeArguments.Length: 1 })
            {
                hasReturnValue = true;
                innerReturnType = namedReturnType.TypeArguments[0].ToDisplayString();
            }
        }

        // Detect ref struct parameters
        bool hasRefStructParam = method.Parameters.Any(p => p.Type.IsRefLikeType);

        return new InvocationInfo
        {
            MethodName = method.Name,
            ContainingType = method.ContainingType.ToDisplayString(),
            ContainingTypeName = method.ContainingType.Name,
            SpanName = customSpanName ?? $"{method.ContainingType.Name}.{method.Name}",
            MetricName = customMetricName ?? $"{method.ContainingType.Name}.{method.Name}",
            FilePath = lineSpan.Path,
            Line = lineSpan.StartLinePosition.Line + 1,
            Column = lineSpan.StartLinePosition.Character + 1,
            ReturnType = method.ReturnType.ToDisplayString(),
            ReturnsVoid = method.ReturnsVoid,
            IsAsync = isAsync,
            IsValueTask = isValueTask,
            HasAsyncReturnValue = hasReturnValue,
            InnerReturnType = innerReturnType,
            IsStatic = method.IsStatic,
            IsGenericMethod = method.IsGenericMethod,
            HasRefStructParam = hasRefStructParam,
            EmitTracing = tracedAttr is not null,
            EmitMetrics = measuredAttr is not null,
            EmitCalls = emitCalls,
            EmitDuration = emitDuration,
            EmitInFlight = emitInFlight,
            ErrorWhenPredicate = errorWhenPredicate,
            ErrorWhenMethodName = errorWhenMethodName,
            CustomTags = customTags,
            Parameters = method.Parameters.Select(p => new ParamInfo
            {
                Name = p.Name,
                Type = p.Type.ToDisplayString(),
                RefKind = p.RefKind,
                IsParams = p.IsParams,
                HasDefaultValue = p.HasExplicitDefaultValue,
                DefaultValueExpression = p.HasExplicitDefaultValue ? FormatDefaultValue(p) : null,
                IsRefStruct = p.Type.IsRefLikeType
            }).ToImmutableArray(),
            InstanceType = method.IsStatic ? null : method.ContainingType.ToDisplayString(),
        };
    }

    /// <summary>
    /// Formats the default value of an optional parameter as a C# expression string.
    /// </summary>
    private static string FormatDefaultValue(IParameterSymbol p)
    {
        if (!p.HasExplicitDefaultValue)
            return "default";

        var value = p.ExplicitDefaultValue;

        if (value is null)
            return "default";

        if (value is string strVal)
            return $"\"{strVal.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

        if (value is char charVal)
            return $"'{charVal}'";

        if (value is bool boolVal)
            return boolVal ? "true" : "false";

        if (value is float floatVal)
            return floatVal.ToString("R") + "f";

        if (value is double doubleVal)
            return doubleVal.ToString("R") + "d";

        if (value is decimal decimalVal)
            return decimalVal.ToString() + "m";

        if (value is long longVal)
            return longVal.ToString() + "L";

        if (value is ulong ulongVal)
            return ulongVal.ToString() + "UL";

        // int, short, byte, etc. -- ToString() is sufficient
        return value.ToString();
    }

    private static bool IsAsyncReturn(IMethodSymbol method)
    {
        var returnType = method.ReturnType.OriginalDefinition;
        var name = returnType.ToDisplayString();
        return name is "System.Threading.Tasks.Task"
            or "System.Threading.Tasks.Task<TResult>"
            or "System.Threading.Tasks.ValueTask"
            or "System.Threading.Tasks.ValueTask<TResult>";
    }

    private static bool IsValueTaskReturn(IMethodSymbol method)
    {
        var returnType = method.ReturnType.OriginalDefinition;
        var name = returnType.ToDisplayString();
        return name is "System.Threading.Tasks.ValueTask"
            or "System.Threading.Tasks.ValueTask<TResult>";
    }
}

internal struct InvocationInfo : IEquatable<InvocationInfo>
{
    public string MethodName { get; set; }
    public string ContainingType { get; set; }
    public string ContainingTypeName { get; set; }
    public string SpanName { get; set; }
    public string MetricName { get; set; }
    public string FilePath { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }
    public string ReturnType { get; set; }
    public bool ReturnsVoid { get; set; }
    public bool IsAsync { get; set; }
    public bool IsValueTask { get; set; }
    /// <summary>Whether the async return type is generic (e.g. Task&lt;T&gt; vs Task).</summary>
    public bool HasAsyncReturnValue { get; set; }
    /// <summary>The inner type argument of Task&lt;T&gt;/ValueTask&lt;T&gt;, or null for non-generic.</summary>
    public string? InnerReturnType { get; set; }
    public bool IsStatic { get; set; }
    public bool IsGenericMethod { get; set; }
    /// <summary>Whether any parameter is a ref struct (Span, ReadOnlySpan, etc.).</summary>
    public bool HasRefStructParam { get; set; }
    public bool EmitTracing { get; set; }
    public bool EmitMetrics { get; set; }
    public bool EmitCalls { get; set; }
    public bool EmitDuration { get; set; }
    public bool EmitInFlight { get; set; }
    /// <summary>
    /// The fully-qualified call expression for the ErrorWhen predicate, e.g. "Svc.IsFailure".
    /// Null when no predicate is configured.
    /// </summary>
    public string? ErrorWhenPredicate { get; set; }
    /// <summary>
    /// The raw ErrorWhen method name as specified in the attribute.
    /// Stored so the output phase can report OTEL003 when ErrorWhenPredicate is null
    /// but this is non-null (meaning the predicate was requested but not resolved).
    /// </summary>
    public string? ErrorWhenMethodName { get; set; }
    public ImmutableArray<string> CustomTags { get; set; }
    public ImmutableArray<ParamInfo> Parameters { get; set; }
    public string? InstanceType { get; set; }

    public bool Equals(InvocationInfo other) =>
        MethodName == other.MethodName &&
        ContainingType == other.ContainingType &&
        ContainingTypeName == other.ContainingTypeName &&
        SpanName == other.SpanName &&
        MetricName == other.MetricName &&
        FilePath == other.FilePath &&
        Line == other.Line &&
        Column == other.Column &&
        ReturnType == other.ReturnType &&
        ReturnsVoid == other.ReturnsVoid &&
        IsAsync == other.IsAsync &&
        IsValueTask == other.IsValueTask &&
        HasAsyncReturnValue == other.HasAsyncReturnValue &&
        InnerReturnType == other.InnerReturnType &&
        IsStatic == other.IsStatic &&
        IsGenericMethod == other.IsGenericMethod &&
        HasRefStructParam == other.HasRefStructParam &&
        EmitTracing == other.EmitTracing &&
        EmitMetrics == other.EmitMetrics &&
        EmitCalls == other.EmitCalls &&
        EmitDuration == other.EmitDuration &&
        EmitInFlight == other.EmitInFlight &&
        ErrorWhenPredicate == other.ErrorWhenPredicate &&
        ErrorWhenMethodName == other.ErrorWhenMethodName &&
        CustomTags.SequenceEqual(other.CustomTags) &&
        Parameters.SequenceEqual(other.Parameters) &&
        InstanceType == other.InstanceType;

    public override bool Equals(object? obj) => obj is InvocationInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (FilePath?.GetHashCode() ?? 0);
            hash = hash * 31 + Line;
            hash = hash * 31 + Column;
            hash = hash * 31 + (MethodName?.GetHashCode() ?? 0);
            hash = hash * 31 + (ContainingType?.GetHashCode() ?? 0);
            hash = hash * 31 + (SpanName?.GetHashCode() ?? 0);
            hash = hash * 31 + (MetricName?.GetHashCode() ?? 0);
            hash = hash * 31 + (ReturnType?.GetHashCode() ?? 0);
            hash = hash * 31 + ReturnsVoid.GetHashCode();
            hash = hash * 31 + IsAsync.GetHashCode();
            hash = hash * 31 + IsValueTask.GetHashCode();
            hash = hash * 31 + HasAsyncReturnValue.GetHashCode();
            hash = hash * 31 + (InnerReturnType?.GetHashCode() ?? 0);
            hash = hash * 31 + IsStatic.GetHashCode();
            hash = hash * 31 + IsGenericMethod.GetHashCode();
            hash = hash * 31 + HasRefStructParam.GetHashCode();
            hash = hash * 31 + EmitTracing.GetHashCode();
            hash = hash * 31 + EmitMetrics.GetHashCode();
            hash = hash * 31 + EmitCalls.GetHashCode();
            hash = hash * 31 + EmitDuration.GetHashCode();
            hash = hash * 31 + EmitInFlight.GetHashCode();
            hash = hash * 31 + (ErrorWhenPredicate?.GetHashCode() ?? 0);
            hash = hash * 31 + (ErrorWhenMethodName?.GetHashCode() ?? 0);
            hash = hash * 31 + (InstanceType?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

internal struct ParamInfo : IEquatable<ParamInfo>
{
    public string Name { get; set; }
    public string Type { get; set; }
    public RefKind RefKind { get; set; }
    /// <summary>Whether the parameter uses the params modifier.</summary>
    public bool IsParams { get; set; }
    /// <summary>Whether the parameter has a default value.</summary>
    public bool HasDefaultValue { get; set; }
    /// <summary>The default value expression as a C# string, or null.</summary>
    public string? DefaultValueExpression { get; set; }
    /// <summary>Whether the parameter type is a ref struct.</summary>
    public bool IsRefStruct { get; set; }

    public bool Equals(ParamInfo other) =>
        Name == other.Name &&
        Type == other.Type &&
        RefKind == other.RefKind &&
        IsParams == other.IsParams &&
        HasDefaultValue == other.HasDefaultValue &&
        DefaultValueExpression == other.DefaultValueExpression &&
        IsRefStruct == other.IsRefStruct;

    public override bool Equals(object? obj) => obj is ParamInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (Name?.GetHashCode() ?? 0);
            hash = hash * 31 + (Type?.GetHashCode() ?? 0);
            hash = hash * 31 + (int)RefKind;
            hash = hash * 31 + IsParams.GetHashCode();
            hash = hash * 31 + HasDefaultValue.GetHashCode();
            hash = hash * 31 + (DefaultValueExpression?.GetHashCode() ?? 0);
            hash = hash * 31 + IsRefStruct.GetHashCode();
            return hash;
        }
    }
}
