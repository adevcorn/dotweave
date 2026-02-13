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
        messageFormat: "Method '{0}' has a ref struct parameter and cannot be intercepted by async dotweave. Remove [Traced]/[Measured] or avoid ref struct parameters on async methods.",
        category: "dotweave",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Phase 1: Collect the set of method symbols that carry [Traced] or [Measured].
        // ForAttributeWithMetadataName is far cheaper than scanning every invocation.
        var tracedMethods = context.SyntaxProvider.ForAttributeWithMetadataName(
            TracedAttributeFqn,
            predicate: static (node, _) => node is MethodDeclarationSyntax,
            transform: static (ctx, _) => GetMethodSymbolKey(ctx))
            .Where(static k => k is not null)
            .Select(static (k, _) => k!);

        var measuredMethods = context.SyntaxProvider.ForAttributeWithMetadataName(
            MeasuredAttributeFqn,
            predicate: static (node, _) => node is MethodDeclarationSyntax,
            transform: static (ctx, _) => GetMethodSymbolKey(ctx))
            .Where(static k => k is not null)
            .Select(static (k, _) => k!);

        var attributedMethodKeys = tracedMethods.Collect()
            .Combine(measuredMethods.Collect())
            .Select(static (pair, _) =>
                pair.Left.Concat(pair.Right).ToImmutableHashSet());

        // Phase 2: Scan invocations, but only resolve symbols for calls whose
        // member name matches an attributed method name (cheap syntactic filter).
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

        var collected = invocations.Collect();

        context.RegisterSourceOutput(collected, static (spc, candidates) =>
        {
            if (candidates.IsEmpty)
                return;

            // Report diagnostics for generic methods
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

    private static string? GetMethodSymbolKey(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is IMethodSymbol method)
            return $"{method.ContainingType.ToDisplayString()}.{method.Name}";
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
            IsAsync = IsAsyncReturn(method),
            IsValueTask = IsValueTaskReturn(method),
            IsStatic = method.IsStatic,
            IsGenericMethod = method.IsGenericMethod,
            EmitTracing = tracedAttr is not null,
            EmitMetrics = measuredAttr is not null,
            EmitCalls = emitCalls,
            EmitDuration = emitDuration,
            EmitInFlight = emitInFlight,
            CustomTags = customTags,
            Parameters = method.Parameters.Select(p => new ParamInfo
            {
                Name = p.Name,
                Type = p.Type.ToDisplayString(),
                RefKind = p.RefKind
            }).ToImmutableArray(),
            InstanceType = method.IsStatic ? null : method.ContainingType.ToDisplayString(),
        };
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
    public bool IsStatic { get; set; }
    public bool IsGenericMethod { get; set; }
    public bool EmitTracing { get; set; }
    public bool EmitMetrics { get; set; }
    public bool EmitCalls { get; set; }
    public bool EmitDuration { get; set; }
    public bool EmitInFlight { get; set; }
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
        IsStatic == other.IsStatic &&
        IsGenericMethod == other.IsGenericMethod &&
        EmitTracing == other.EmitTracing &&
        EmitMetrics == other.EmitMetrics &&
        EmitCalls == other.EmitCalls &&
        EmitDuration == other.EmitDuration &&
        EmitInFlight == other.EmitInFlight &&
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
            return hash;
        }
    }
}

internal struct ParamInfo : IEquatable<ParamInfo>
{
    public string Name { get; set; }
    public string Type { get; set; }
    public RefKind RefKind { get; set; }

    public bool Equals(ParamInfo other) =>
        Name == other.Name &&
        Type == other.Type &&
        RefKind == other.RefKind;

    public override bool Equals(object? obj) => obj is ParamInfo other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + (Name?.GetHashCode() ?? 0);
            hash = hash * 31 + (Type?.GetHashCode() ?? 0);
            hash = hash * 31 + (int)RefKind;
            return hash;
        }
    }
}
