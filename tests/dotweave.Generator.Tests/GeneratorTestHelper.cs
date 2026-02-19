using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using dotweave.Generator;

namespace dotweave.Generator.Tests;

/// <summary>
/// Helper for running the TracedInterceptorGenerator against in-memory source code
/// and verifying the generated output compiles without errors.
/// </summary>
public static class GeneratorTestHelper
{
    /// <summary>
    /// The attribute source that would normally come from the dotweave.Attributes project.
    /// Included inline so generator tests don't need a project reference loop.
    /// </summary>
    private const string AttributeSource = """
        using System;
        namespace dotweave;

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
        public sealed class TracedAttribute : Attribute
        {
            public string? SpanName { get; }
            public TracedAttribute() { }
            public TracedAttribute(string spanName) { SpanName = spanName; }
        }

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
        public sealed class MeasuredAttribute : Attribute
        {
            public string? MetricName { get; }
            public bool Calls { get; set; } = true;
            public bool Duration { get; set; } = true;
            public bool InFlight { get; set; } = false;
            public string[]? Tags { get; set; }
            public string? ErrorWhen { get; set; }
            public MeasuredAttribute() { }
            public MeasuredAttribute(string metricName) { MetricName = metricName; }
        }
        """;

    /// <summary>
    /// Runs the generator on the given source code and returns the result.
    /// </summary>
    public static GeneratorRunResult RunGenerator(string source)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithFeatures([new KeyValuePair<string, string>("InterceptorsNamespaces", "dotweave.Generated")]);

        var attributeTree = CSharpSyntaxTree.ParseText(AttributeSource, parseOptions, path: "Attributes.cs");
        var sourceTree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "TestSource.cs");

        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [attributeTree, sourceTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var generator = new TracedInterceptorGenerator();
        var driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var result = driver.GetRunResult();
        var generatorResult = result.Results.Single();

        return new GeneratorRunResult(outputCompilation, generatorResult, diagnostics);
    }

    /// <summary>
    /// Runs the generator and asserts the output compiles with no errors.
    /// Returns the generated source text.
    /// </summary>
    public static string RunAndVerifyCompilation(string source)
    {
        var result = RunGenerator(source);
        result.AssertNoCompilationErrors();
        return result.GeneratedSource ?? "";
    }

    /// <summary>
    /// Runs the generator and asserts it produces no generated source.
    /// </summary>
    public static void RunAndVerifyNoOutput(string source)
    {
        var result = RunGenerator(source);
        Assert.Empty(result.Result.GeneratedSources);
    }

    /// <summary>
    /// Runs the generator and returns the diagnostics with the given ID.
    /// </summary>
    public static ImmutableArray<Diagnostic> RunAndGetDiagnostics(string source, string diagnosticId)
    {
        var result = RunGenerator(source);
        return result.Result.Diagnostics
            .Where(d => d.Id == diagnosticId)
            .ToImmutableArray();
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        // Collect all runtime assemblies available to this test process
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToList();

        // Ensure we have core types
        assemblies.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        assemblies.Add(MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.Task).Assembly.Location));
        assemblies.Add(MetadataReference.CreateFromFile(typeof(System.Threading.Tasks.ValueTask).Assembly.Location));
        assemblies.Add(MetadataReference.CreateFromFile(typeof(System.Diagnostics.Activity).Assembly.Location));
        assemblies.Add(MetadataReference.CreateFromFile(typeof(System.Diagnostics.Metrics.Meter).Assembly.Location));

        return assemblies.Distinct().ToArray();
    }
}

public class GeneratorRunResult
{
    public Compilation OutputCompilation { get; }
    public Microsoft.CodeAnalysis.GeneratorRunResult Result { get; }
    public ImmutableArray<Diagnostic> DriverDiagnostics { get; }

    public GeneratorRunResult(
        Compilation outputCompilation,
        Microsoft.CodeAnalysis.GeneratorRunResult result,
        ImmutableArray<Diagnostic> driverDiagnostics)
    {
        OutputCompilation = outputCompilation;
        Result = result;
        DriverDiagnostics = driverDiagnostics;
    }

    public string? GeneratedSource =>
        Result.GeneratedSources.Length > 0
            ? Result.GeneratedSources[0].SourceText.ToString()
            : null;

    public void AssertNoCompilationErrors()
    {
        var errors = OutputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
        {
            var messages = string.Join(Environment.NewLine, errors.Select(e =>
                $"  {e.Id}: {e.GetMessage()} at {e.Location}"));
            var generated = GeneratedSource ?? "(no generated source)";
            Assert.Fail(
                $"Compilation produced {errors.Count} error(s):{Environment.NewLine}{messages}" +
                $"{Environment.NewLine}{Environment.NewLine}Generated source:{Environment.NewLine}{generated}");
        }
    }
}
