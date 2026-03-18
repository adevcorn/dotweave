namespace dotweave.Generator.Tests;

/// <summary>
/// Tests for edge cases: no attribute (no output), static methods,
/// custom span names, multiple call sites, and methods without invocations.
/// </summary>
public class EdgeCaseTests
{
    [Fact]
    public void NoAttributes_NoOutput()
    {
        var source = """
            public class Svc
            {
                public string GetValue() => "hello";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue();
            }
            """;
        GeneratorTestHelper.RunAndVerifyNoOutput(source);
    }

    [Fact]
    public void AttributedButNeverCalled_NoOutput()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public string GetValue() => "hello";
            }
            """;
        GeneratorTestHelper.RunAndVerifyNoOutput(source);
    }

    [Fact]
    public void ExtensionMethod_OnSelf_Compiles()
    {
        // An extension method that is itself [Traced] must be called via static syntax
        // in the interceptor (ContainingType.Method(__this, args...)), not instance syntax
        // (__this.Method(args...)), because the method is not a member of the receiver type → CS1061.
        var source = """
            using dotweave;
            public interface IBuilder { }
            public static class BuilderExtensions
            {
                [Traced]
                public static IBuilder MapFoo(this IBuilder app)
                {
                    return app;
                }
            }
            public class Caller
            {
                public IBuilder Call(IBuilder app) => app.MapFoo();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_MapFoo_0", generated);
        // Call-through must be static: BuilderExtensions.MapFoo(__this)
        Assert.Contains("BuilderExtensions.MapFoo(__this)", generated);
        // Must NOT use instance syntax: __this.MapFoo()
        Assert.DoesNotContain("__this.MapFoo()", generated);
    }

    [Fact]
    public void ExtensionMethod_Compiles()
    {
        var source = """
            using dotweave;
            public static class StringExtensions
            {
                [Traced]
                [Measured]
                public static string Shout(this string input) => input.ToUpper();
            }
            public class Caller
            {
                public string Call() => "hello".Shout();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_Shout_0", generated);
        // Receiver type (string) must be the 'this' parameter, NOT the static class
        Assert.Contains("this string __this", generated);
        Assert.DoesNotContain("this StringExtensions", generated);
        // Call-through must use static syntax: StringExtensions.Shout(__this)
        Assert.Contains("StringExtensions.Shout(__this)", generated);
        Assert.DoesNotContain("__this.Shout()", generated);
    }

    [Fact]
    public void StaticMethod_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public static string GetValue() => "hello";
            }
            public class Caller
            {
                public string Call() => Svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_GetValue_0", generated);
        // Static method should NOT have "this" parameter
        Assert.DoesNotContain("this Svc", generated);
        // Should call the static method via the type name
        Assert.Contains("Svc.GetValue", generated);
    }

    [Fact]
    public void CustomSpanName_UsesIt()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced("custom.span.name")]
                public string GetValue() => "hello";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("custom.span.name", generated);
    }

    [Fact]
    public void DefaultSpanName_UsesTypeAndMethodName()
    {
        var source = """
            using dotweave;
            public class GreetingService
            {
                [Traced]
                public string GetGreeting() => "hello";
            }
            public class Caller
            {
                public string Call(GreetingService svc) => svc.GetGreeting();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("GreetingService.GetGreeting", generated);
    }

    [Fact]
    public void MultipleCallSites_SameMethod_ProducesMultipleInterceptors()
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
                public void Call(Svc svc)
                {
                    var a = svc.GetValue();
                    var b = svc.GetValue();
                }
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_GetValue_0", generated);
        Assert.Contains("Intercepted_GetValue_1", generated);
    }

    [Fact]
    public void MultipleMethods_DifferentAttributes_Compiles()
    {
        var source = """
            using dotweave;
            using System.Threading.Tasks;
            public class Svc
            {
                [Traced]
                public string GetSync() => "hello";

                [Measured]
                public async Task<int> GetAsync()
                {
                    await Task.Delay(1);
                    return 42;
                }

                [Traced]
                [Measured(InFlight = true)]
                public void DoWork() { }
            }
            public class Caller
            {
                public async Task Call(Svc svc)
                {
                    svc.GetSync();
                    await svc.GetAsync();
                    svc.DoWork();
                }
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_GetSync_0", generated);
        Assert.Contains("Intercepted_GetAsync_1", generated);
        Assert.Contains("Intercepted_DoWork_2", generated);
    }

    [Fact]
    public void TracedOnly_NoMetricFields()
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
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("ActivitySource", generated);
        Assert.DoesNotContain("Meter", generated);
        Assert.DoesNotContain("Calls_", generated);
    }

    [Fact]
    public void GeneratedCode_HasAutoGeneratedComment()
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
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("// <auto-generated />", generated);
        Assert.Contains("#nullable enable", generated);
    }

    [Fact]
    public void GeneratedCode_HasInterceptsLocationAttribute()
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
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("InterceptsLocationAttribute", generated);
        Assert.Contains("InterceptsLocation(@", generated);
    }

    [Fact]
    public void TracedAsync_HasActivityErrorHandling()
    {
        var source = """
            using dotweave;
            using System.Threading.Tasks;
            public class Svc
            {
                [Traced]
                public async Task<string> GetValueAsync()
                {
                    await Task.Delay(1);
                    return "hello";
                }
            }
            public class Caller
            {
                public async Task Call(Svc svc) => await svc.GetValueAsync();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("SetStatus(System.Diagnostics.ActivityStatusCode.Error", generated);
        Assert.Contains("exception.type", generated);
        Assert.Contains("exception.message", generated);
        Assert.Contains("exception.stacktrace", generated);
    }
}
