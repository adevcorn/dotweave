namespace dotweave.Generator.Tests;

/// <summary>
/// Tests for [Traced]/[Measured] on methods called through an interface.
///
/// The C# interceptor mechanism resolves call sites by the *static* (compile-time) type
/// of the receiver. This creates two distinct scenarios:
///
///   A) [Traced] on the concrete class, called via a concrete-typed variable — works by default.
///   B) [Traced] on the concrete class, called via an interface-typed variable — NOT supported:
///      the generator cannot safely intercept because the concrete type is unknown at compile time.
///   C) [Traced] on the interface method, called via a concrete-typed variable — now supported:
///      the generator walks the concrete type's AllInterfaces to find the attributed interface member.
///   D) [Traced] on the interface method, called via an interface-typed variable — now supported:
///      GetSymbolInfo returns the interface method directly, which carries [Traced].
/// </summary>
public class InterfaceTests
{
    // ── Scenario A: regression ────────────────────────────────────────────────

    [Fact]
    public void Traced_OnConcreteClass_CalledViaConcrete_Compiles()
    {
        // Baseline: [Traced] on the concrete class, call site uses the concrete type.
        // This has always worked and must continue to work.
        var source = """
            using dotweave;
            public interface IGreeter
            {
                string Greet(string name);
            }
            public class Greeter : IGreeter
            {
                [Traced]
                public string Greet(string name) => $"Hello, {name}!";
            }
            public class Caller
            {
                public string Call(Greeter svc) => svc.Greet("world");
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_Greet_0", generated);
        // Span name must use the concrete type name
        Assert.Contains("Greeter.Greet", generated);
    }

    // ── Scenario B: unsupported case — no interceptor, no crash ──────────────

    [Fact]
    public void Traced_OnConcreteClass_CalledViaInterface_ProducesNoOutput()
    {
        // [Traced] on the concrete class, but the call site uses an interface-typed variable.
        // GetSymbolInfo returns the interface method symbol, which has no [Traced].
        // The generator cannot safely intercept this (it would need to choose a concrete
        // implementation at compile time), so it must produce no interceptor silently.
        var source = """
            using dotweave;
            public interface IGreeter
            {
                string Greet(string name);
            }
            public class Greeter : IGreeter
            {
                [Traced]
                public string Greet(string name) => $"Hello, {name}!";
            }
            public class Caller
            {
                public string Call(IGreeter svc) => svc.Greet("world");
            }
            """;
        // No interceptor should be generated — no infinite-recursion risk
        GeneratorTestHelper.RunAndVerifyNoOutput(source);
    }

    // ── Scenario C: [Traced] on interface, called via concrete type ───────────

    [Fact]
    public void Traced_OnInterface_CalledViaConcrete_Compiles()
    {
        // [Traced] is on the interface method. Call site uses the concrete type.
        // GetSymbolInfo returns the concrete method (Inherited=false means no attribute there),
        // but the generator now walks AllInterfaces to find the attributed interface member.
        var source = """
            using dotweave;
            public interface IGreeter
            {
                [Traced]
                string Greet(string name);
            }
            public class Greeter : IGreeter
            {
                public string Greet(string name) => $"Hello, {name}!";
            }
            public class Caller
            {
                public string Call(Greeter svc) => svc.Greet("world");
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_Greet_0", generated);
        // Span name must use the concrete type name, not the interface name
        Assert.Contains("Greeter.Greet", generated);
        Assert.DoesNotContain("IGreeter.Greet", generated);
    }

    [Fact]
    public void Measured_OnInterface_CalledViaConcrete_Compiles()
    {
        // Same as above but with [Measured] to confirm the same chain-walk works for metrics.
        var source = """
            using dotweave;
            public interface ICounter
            {
                [Measured]
                void Increment();
            }
            public class Counter : ICounter
            {
                public void Increment() { }
            }
            public class Caller
            {
                public void Call(Counter svc) => svc.Increment();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_Increment_0", generated);
        Assert.Contains("Counter.Increment", generated);
    }

    // ── Scenario D: [Traced] on interface, called via interface ──────────────

    [Fact]
    public void Traced_OnInterface_CalledViaInterface_Compiles()
    {
        // [Traced] is on the interface method AND the call site uses the interface type.
        // GetSymbolInfo returns the interface method directly, which carries [Traced].
        // This is the straightforward path — regression test.
        var source = """
            using dotweave;
            public interface IGreeter
            {
                [Traced]
                string Greet(string name);
            }
            public class Greeter : IGreeter
            {
                public string Greet(string name) => $"Hello, {name}!";
            }
            public class Caller
            {
                public string Call(IGreeter svc) => svc.Greet("world");
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_Greet_0", generated);
        // When called via interface, ContainingType IS the interface — span uses interface name
        Assert.Contains("IGreeter.Greet", generated);
    }

    // ── Scenario C async: interface attribute on async method ─────────────────

    [Fact]
    public void Traced_OnInterface_AsyncMethod_CalledViaConcrete_Compiles()
    {
        var source = """
            using dotweave;
            using System.Threading.Tasks;
            public interface IGreeter
            {
                [Traced]
                Task<string> GreetAsync(string name);
            }
            public class Greeter : IGreeter
            {
                public async Task<string> GreetAsync(string name)
                {
                    await Task.Yield();
                    return $"Hello, {name}!";
                }
            }
            public class Caller
            {
                public Task<string> Call(Greeter svc) => svc.GreetAsync("world");
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_GreetAsync_0", generated);
        Assert.Contains("async", generated);
        Assert.Contains("Greeter.GreetAsync", generated);
    }

    // ── Multiple interface implementations ────────────────────────────────────

    [Fact]
    public void Traced_OnInterface_MultipleImplementors_EachIntercepted()
    {
        // Two different concrete types implement the same [Traced] interface method.
        // Each call site (via its concrete type) should get its own interceptor with
        // the correct concrete type name in the span.
        var source = """
            using dotweave;
            public interface IGreeter
            {
                [Traced]
                string Greet(string name);
            }
            public class EnglishGreeter : IGreeter
            {
                public string Greet(string name) => $"Hello, {name}!";
            }
            public class SpanishGreeter : IGreeter
            {
                public string Greet(string name) => $"Hola, {name}!";
            }
            public class Caller
            {
                public string CallEn(EnglishGreeter svc) => svc.Greet("world");
                public string CallEs(SpanishGreeter svc) => svc.Greet("world");
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_Greet_0", generated);
        Assert.Contains("Intercepted_Greet_1", generated);
        Assert.Contains("EnglishGreeter.Greet", generated);
        Assert.Contains("SpanishGreeter.Greet", generated);
    }
}
