namespace dotweave.Generator.Tests;

/// <summary>
/// Tests for the ErrorWhen property on [Measured] — verifying that the generator
/// emits predicate-based status determination instead of always using "ok",
/// validates predicate signatures, reports diagnostics for invalid predicates,
/// and integrates with [Traced] span status.
/// </summary>
public class ErrorWhenTests
{
    [Fact]
    public void ErrorWhen_SyncReturn_EmitsPredicateCall()
    {
        var source = """
            using dotweave;
            public class Result
            {
                public bool IsSuccess { get; set; }
                public string? Value { get; set; }
            }
            public class Svc
            {
                [Measured(ErrorWhen = nameof(IsFailure))]
                public Result GetValue() => new Result { IsSuccess = true, Value = "hello" };

                public static bool IsFailure(Result r) => !r.IsSuccess;
            }
            public class Caller
            {
                public Result Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Svc.IsFailure(__result)", generated);
        Assert.Contains("\"status\", \"error\"", generated);
        Assert.Contains("\"status\", \"ok\"", generated);
    }

    [Fact]
    public void ErrorWhen_AsyncTaskOfT_EmitsPredicateCall()
    {
        var source = """
            using dotweave;
            using System.Threading.Tasks;
            public class Result
            {
                public bool IsSuccess { get; set; }
                public string? Value { get; set; }
            }
            public class Svc
            {
                [Measured(ErrorWhen = nameof(IsFailure))]
                public async Task<Result> GetValueAsync()
                {
                    await Task.Delay(1);
                    return new Result { IsSuccess = true, Value = "hello" };
                }

                public static bool IsFailure(Result r) => !r.IsSuccess;
            }
            public class Caller
            {
                public async Task<Result> Call(Svc svc) => await svc.GetValueAsync();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Svc.IsFailure(__result)", generated);
        Assert.Contains("\"status\", \"error\"", generated);
        Assert.Contains("\"status\", \"ok\"", generated);
    }

    [Fact]
    public void ErrorWhen_ValueTaskOfT_EmitsPredicateInBothPaths()
    {
        var source = """
            using dotweave;
            using System.Threading.Tasks;
            public class Result
            {
                public bool IsSuccess { get; set; }
            }
            public class Svc
            {
                [Measured(ErrorWhen = nameof(IsFailure))]
                public ValueTask<Result> GetValueAsync()
                    => new ValueTask<Result>(new Result { IsSuccess = true });

                public static bool IsFailure(Result r) => !r.IsSuccess;
            }
            public class Caller
            {
                public async Task<Result> Call(Svc svc) => await svc.GetValueAsync();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        // Fast path uses __task.Result
        Assert.Contains("Svc.IsFailure(__task.Result)", generated);
        // Slow path uses __result
        Assert.Contains("Svc.IsFailure(__result)", generated);
    }

    [Fact]
    public void ErrorWhen_VoidMethod_ReportsOTEL003()
    {
        // Void methods have no return value to inspect — OTEL003 should be reported.
        var source = """
            using dotweave;
            public class Svc
            {
                [Measured(ErrorWhen = "IsFailure")]
                public void DoWork() { }

                public static bool IsFailure(string r) => false;
            }
            public class Caller
            {
                public void Call(Svc svc) => svc.DoWork();
            }
            """;
        var diagnostics = GeneratorTestHelper.RunAndGetDiagnostics(source, "OTEL003");
        Assert.Single(diagnostics);
        Assert.Contains("IsFailure", diagnostics[0].GetMessage());
        Assert.Contains("DoWork", diagnostics[0].GetMessage());

        // Should still compile — falls back to default status="ok"
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.DoesNotContain("IsFailure(", generated);
        Assert.Contains("\"status\", \"ok\"", generated);
    }

    [Fact]
    public void ErrorWhen_AsyncTask_NoReturnValue_ReportsOTEL003()
    {
        // Plain Task (no return value) with ErrorWhen — OTEL003 should be reported.
        var source = """
            using dotweave;
            using System.Threading.Tasks;
            public class Svc
            {
                [Measured(ErrorWhen = "IsFailure")]
                public async Task DoWorkAsync() { await Task.Delay(1); }

                public static bool IsFailure(string r) => false;
            }
            public class Caller
            {
                public async Task Call(Svc svc) => await svc.DoWorkAsync();
            }
            """;
        var diagnostics = GeneratorTestHelper.RunAndGetDiagnostics(source, "OTEL003");
        Assert.Single(diagnostics);

        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.DoesNotContain("IsFailure(", generated);
    }

    [Fact]
    public void ErrorWhen_TypeMismatch_ReportsOTEL003()
    {
        // Predicate accepts int, but method returns Result — type mismatch.
        var source = """
            using dotweave;
            public class Result
            {
                public bool IsSuccess { get; set; }
            }
            public class Svc
            {
                [Measured(ErrorWhen = nameof(IsFailure))]
                public Result GetValue() => new Result { IsSuccess = true };

                public static bool IsFailure(int x) => x < 0;
            }
            public class Caller
            {
                public Result Call(Svc svc) => svc.GetValue();
            }
            """;
        var diagnostics = GeneratorTestHelper.RunAndGetDiagnostics(source, "OTEL003");
        Assert.Single(diagnostics);
        Assert.Contains("IsFailure", diagnostics[0].GetMessage());
        Assert.Contains("GetValue", diagnostics[0].GetMessage());

        // Should still compile with default status="ok"
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.DoesNotContain("IsFailure(", generated);
    }

    [Fact]
    public void ErrorWhen_NonexistentMethod_ReportsOTEL003()
    {
        // Typo in method name — predicate doesn't exist.
        var source = """
            using dotweave;
            public class Result
            {
                public bool IsSuccess { get; set; }
            }
            public class Svc
            {
                [Measured(ErrorWhen = "IsFailur")]
                public Result GetValue() => new Result { IsSuccess = true };

                public static bool IsFailure(Result r) => !r.IsSuccess;
            }
            public class Caller
            {
                public Result Call(Svc svc) => svc.GetValue();
            }
            """;
        var diagnostics = GeneratorTestHelper.RunAndGetDiagnostics(source, "OTEL003");
        Assert.Single(diagnostics);
        Assert.Contains("IsFailur", diagnostics[0].GetMessage());
    }

    [Fact]
    public void ErrorWhen_NonStaticPredicate_ReportsOTEL003()
    {
        // Predicate exists but is not static.
        var source = """
            using dotweave;
            public class Result
            {
                public bool IsSuccess { get; set; }
            }
            public class Svc
            {
                [Measured(ErrorWhen = nameof(IsFailure))]
                public Result GetValue() => new Result { IsSuccess = true };

                public bool IsFailure(Result r) => !r.IsSuccess;
            }
            public class Caller
            {
                public Result Call(Svc svc) => svc.GetValue();
            }
            """;
        var diagnostics = GeneratorTestHelper.RunAndGetDiagnostics(source, "OTEL003");
        Assert.Single(diagnostics);
    }

    [Fact]
    public void ErrorWhen_WithTraced_SetsSpanErrorStatus()
    {
        var source = """
            using dotweave;
            public class Result
            {
                public bool IsSuccess { get; set; }
                public string? Value { get; set; }
            }
            public class Svc
            {
                [Traced]
                [Measured(ErrorWhen = nameof(IsFailure))]
                public Result GetValue() => new Result { IsSuccess = true, Value = "hello" };

                public static bool IsFailure(Result r) => !r.IsSuccess;
            }
            public class Caller
            {
                public Result Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        // Tracing is present
        Assert.Contains("ActivitySource", generated);
        Assert.Contains("StartActivity", generated);
        // Predicate-based metrics
        Assert.Contains("Svc.IsFailure(__result)", generated);
        // Span status is set to Error when predicate returns true
        Assert.Contains("SetStatus(System.Diagnostics.ActivityStatusCode.Error", generated);
    }

    [Fact]
    public void ErrorWhen_MeasuredOnly_NoSpanStatusCode()
    {
        // Without [Traced], no span status code should be emitted.
        var source = """
            using dotweave;
            public class Result
            {
                public bool IsSuccess { get; set; }
            }
            public class Svc
            {
                [Measured(ErrorWhen = nameof(IsFailure))]
                public Result GetValue() => new Result { IsSuccess = true };

                public static bool IsFailure(Result r) => !r.IsSuccess;
            }
            public class Caller
            {
                public Result Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Svc.IsFailure(__result)", generated);
        Assert.DoesNotContain("ActivityStatusCode", generated);
    }

    [Fact]
    public void ErrorWhen_WithCustomTags_CombinesTags()
    {
        var source = """
            using dotweave;
            public class Result
            {
                public bool IsSuccess { get; set; }
            }
            public class Svc
            {
                [Measured(ErrorWhen = nameof(IsFailure), Tags = new[] { "endpoint=test" })]
                public Result GetValue() => new Result { IsSuccess = true };

                public static bool IsFailure(Result r) => !r.IsSuccess;
            }
            public class Caller
            {
                public Result Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Svc.IsFailure(__result)", generated);
        Assert.Contains("\"endpoint\"", generated);
        Assert.Contains("\"test\"", generated);
    }

    [Fact]
    public void ErrorWhen_ExceptionStillRecordsError()
    {
        // Even with ErrorWhen, exceptions should still record status="error"
        // via the catch block (unchanged behavior).
        var source = """
            using dotweave;
            public class Result
            {
                public bool IsSuccess { get; set; }
            }
            public class Svc
            {
                [Measured(ErrorWhen = nameof(IsFailure))]
                public Result GetValue() => new Result { IsSuccess = true };

                public static bool IsFailure(Result r) => !r.IsSuccess;
            }
            public class Caller
            {
                public Result Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        // Catch block should still emit status="error"
        Assert.Contains("catch (System.Exception __ex)", generated);
        Assert.Contains("\"status\", \"error\"", generated);
    }

    [Fact]
    public void ErrorWhen_WithoutPredicate_DefaultBehavior()
    {
        // Without ErrorWhen, the default behavior should be unchanged:
        // success path always records status="ok".
        var source = """
            using dotweave;
            public class Result
            {
                public bool IsSuccess { get; set; }
            }
            public class Svc
            {
                [Measured]
                public Result GetValue() => new Result { IsSuccess = true };
            }
            public class Caller
            {
                public Result Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.DoesNotContain("IsFailure(", generated);
        Assert.Contains("\"status\", \"ok\"", generated);
    }

    [Fact]
    public void ErrorWhen_ValidPredicate_NoOTEL003()
    {
        // A correctly configured ErrorWhen should not produce OTEL003.
        var source = """
            using dotweave;
            public class Result
            {
                public bool IsSuccess { get; set; }
            }
            public class Svc
            {
                [Measured(ErrorWhen = nameof(IsFailure))]
                public Result GetValue() => new Result { IsSuccess = true };

                public static bool IsFailure(Result r) => !r.IsSuccess;
            }
            public class Caller
            {
                public Result Call(Svc svc) => svc.GetValue();
            }
            """;
        var diagnostics = GeneratorTestHelper.RunAndGetDiagnostics(source, "OTEL003");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ErrorWhen_AsyncTaskOfT_TypeMismatch_ReportsOTEL003()
    {
        // Predicate accepts string, but Task<Result> unwraps to Result.
        var source = """
            using dotweave;
            using System.Threading.Tasks;
            public class Result
            {
                public bool IsSuccess { get; set; }
            }
            public class Svc
            {
                [Measured(ErrorWhen = nameof(IsFailure))]
                public async Task<Result> GetValueAsync()
                {
                    await Task.Delay(1);
                    return new Result { IsSuccess = true };
                }

                public static bool IsFailure(string s) => string.IsNullOrEmpty(s);
            }
            public class Caller
            {
                public async Task<Result> Call(Svc svc) => await svc.GetValueAsync();
            }
            """;
        var diagnostics = GeneratorTestHelper.RunAndGetDiagnostics(source, "OTEL003");
        Assert.Single(diagnostics);
    }

    [Fact]
    public void ErrorWhen_WithTraced_Async_SetsSpanErrorStatus()
    {
        var source = """
            using dotweave;
            using System.Threading.Tasks;
            public class Result
            {
                public bool IsSuccess { get; set; }
            }
            public class Svc
            {
                [Traced]
                [Measured(ErrorWhen = nameof(IsFailure))]
                public async Task<Result> GetValueAsync()
                {
                    await Task.Delay(1);
                    return new Result { IsSuccess = true };
                }

                public static bool IsFailure(Result r) => !r.IsSuccess;
            }
            public class Caller
            {
                public async Task<Result> Call(Svc svc) => await svc.GetValueAsync();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Svc.IsFailure(__result)", generated);
        Assert.Contains("SetStatus(System.Diagnostics.ActivityStatusCode.Error", generated);
    }
}
