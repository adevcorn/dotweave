namespace dotweave.Generator.Tests;

/// <summary>
/// Tests for the [Measured] attribute — verifying that the generator emits
/// correct metric fields and recording code for calls, duration, inflight,
/// and custom tags.
/// </summary>
public class MetricsTests
{
    [Fact]
    public void MeasuredOnly_EmitsCallsAndDuration()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Measured]
                public string GetValue() => "hello";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Calls_", generated);
        Assert.Contains("DurationMs_", generated);
        Assert.Contains("Meter.CreateCounter<long>", generated);
        Assert.Contains("Meter.CreateHistogram<double>", generated);
        // Should NOT contain ActivitySource since only [Measured]
        Assert.DoesNotContain("ActivitySource", generated);
    }

    [Fact]
    public void MeasuredCallsOnly_NoDuration()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Measured(Duration = false)]
                public string GetValue() => "hello";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Calls_", generated);
        Assert.DoesNotContain("DurationMs_", generated);
        Assert.DoesNotContain("GetElapsedMs", generated);
    }

    [Fact]
    public void MeasuredWithInFlight_EmitsUpDownCounter()
    {
        var source = """
            using dotweave;
            using System.Threading.Tasks;
            public class Svc
            {
                [Measured(InFlight = true)]
                public async Task DoWorkAsync() { await Task.Delay(1); }
            }
            public class Caller
            {
                public async Task Call(Svc svc) => await svc.DoWorkAsync();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("InFlight_", generated);
        Assert.Contains("CreateUpDownCounter<long>", generated);
        // InFlight decrement should be in a finally block
        Assert.Contains("finally", generated);
        Assert.Contains(".Add(-1", generated);
    }

    [Fact]
    public void MeasuredWithCustomTags_EmitsTagArgs()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Measured(Tags = new[] { "endpoint=greeting", "tier=free" })]
                public string GetValue() => "hello";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("\"endpoint\"", generated);
        Assert.Contains("\"greeting\"", generated);
        Assert.Contains("\"tier\"", generated);
        Assert.Contains("\"free\"", generated);
    }

    [Fact]
    public void MeasuredWithCustomMetricName_UsesIt()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Measured("custom.metric.name")]
                public string GetValue() => "hello";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("custom.metric.name.calls", generated);
        Assert.Contains("custom.metric.name.duration", generated);
    }

    [Fact]
    public void TracedAndMeasured_EmitsBoth()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                [Measured]
                public string GetValue() => "hello";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("ActivitySource", generated);
        Assert.Contains("Meter", generated);
        Assert.Contains("StartActivity", generated);
        Assert.Contains("Calls_", generated);
    }

    [Fact]
    public void MeasuredNoCalls_NoDuration_OnlyInFlight()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Measured(Calls = false, Duration = false, InFlight = true)]
                public string GetValue() => "hello";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("InFlight_", generated);
        Assert.DoesNotContain("Calls_", generated);
        Assert.DoesNotContain("DurationMs_", generated);
    }

    [Fact]
    public void InFlightDecrement_InFinallyBlock_SyncMethod()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Measured(InFlight = true)]
                public string GetValue() => "hello";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        // The finally block should contain the InFlight decrement
        var finallyIdx = generated.IndexOf("finally");
        var decrementIdx = generated.IndexOf(".Add(-1", finallyIdx);
        Assert.True(finallyIdx > 0, "Expected a finally block");
        Assert.True(decrementIdx > finallyIdx, "InFlight decrement should be in the finally block");
    }
}
