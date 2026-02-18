namespace dotweave.Generator.Tests;

/// <summary>
/// Tests that the generator reports correct diagnostics:
/// OTEL001 for generic methods, OTEL002 for ref struct params on async methods.
/// </summary>
public class DiagnosticTests
{
    [Fact]
    public void GenericMethod_ReportsOTEL001()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public T GetValue<T>() => default!;
            }
            public class Caller
            {
                public int Call(Svc svc) => svc.GetValue<int>();
            }
            """;
        var diagnostics = GeneratorTestHelper.RunAndGetDiagnostics(source, "OTEL001");
        Assert.Single(diagnostics);
        Assert.Contains("GetValue", diagnostics[0].GetMessage());
        Assert.Contains("generic", diagnostics[0].GetMessage());
    }

    [Fact]
    public void RefStructOnAsyncMethod_ReportsOTEL002()
    {
        var source = """
            using dotweave;
            using System;
            using System.Threading.Tasks;
            public class Svc
            {
                [Traced]
                public async Task Process(ReadOnlySpan<byte> data)
                {
                    await Task.Delay(1);
                }
            }
            public class Caller
            {
                public async Task Call(Svc svc)
                {
                    await svc.Process(ReadOnlySpan<byte>.Empty);
                }
            }
            """;
        var diagnostics = GeneratorTestHelper.RunAndGetDiagnostics(source, "OTEL002");
        Assert.Single(diagnostics);
        Assert.Contains("Process", diagnostics[0].GetMessage());
        Assert.Contains("data", diagnostics[0].GetMessage());
    }

    [Fact]
    public void RefStructOnSyncMethod_NoDiagnostic()
    {
        // ref structs on sync methods should be fine (no async wrapper needed)
        var source = """
            using dotweave;
            using System;
            public class Svc
            {
                [Traced]
                public int Process(ReadOnlySpan<byte> data) => data.Length;
            }
            public class Caller
            {
                public int Call(Svc svc) => svc.Process(ReadOnlySpan<byte>.Empty);
            }
            """;
        var diagnostics = GeneratorTestHelper.RunAndGetDiagnostics(source, "OTEL002");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void NonGenericMethod_NoOTEL001()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public string GetValue() => "hello";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue();
            }
            """;
        var diagnostics = GeneratorTestHelper.RunAndGetDiagnostics(source, "OTEL001");
        Assert.Empty(diagnostics);
    }
}
