namespace dotweave.Generator.Tests;

/// <summary>
/// Tests that the generator produces valid, compilable code for each
/// return type variant: void, sync return, Task, Task&lt;T&gt;, ValueTask, ValueTask&lt;T&gt;.
/// </summary>
public class ReturnTypeTests
{
    [Fact]
    public void VoidMethod_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public void DoWork() { }
            }
            public class Caller
            {
                public void Call(Svc svc) => svc.DoWork();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_DoWork_0", generated);
        Assert.Contains("public static void", generated);
    }

    [Fact]
    public void SyncReturnMethod_Compiles()
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
        Assert.Contains("Intercepted_GetValue_0", generated);
        Assert.Contains("return __result;", generated);
    }

    [Fact]
    public void AsyncTask_Compiles()
    {
        var source = """
            using dotweave;
            using System.Threading.Tasks;
            public class Svc
            {
                [Traced]
                public async Task DoWorkAsync() { await Task.Delay(1); }
            }
            public class Caller
            {
                public async Task Call(Svc svc) => await svc.DoWorkAsync();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("async System.Threading.Tasks.Task Intercepted_DoWorkAsync_0", generated);
        Assert.DoesNotContain("return __result;", generated);
    }

    [Fact]
    public void AsyncTaskOfT_Compiles()
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
                public async Task<string> Call(Svc svc) => await svc.GetValueAsync();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("async System.Threading.Tasks.Task<string> Intercepted_GetValueAsync_0", generated);
        Assert.Contains("return __result;", generated);
    }

    [Fact]
    public void ValueTask_Compiles()
    {
        var source = """
            using dotweave;
            using System.Threading.Tasks;
            public class Svc
            {
                [Traced]
                public ValueTask DoWorkAsync() => default;
            }
            public class Caller
            {
                public async Task Call(Svc svc) => await svc.DoWorkAsync();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_DoWorkAsync_0", generated);
        Assert.Contains("IsCompletedSuccessfully", generated);
        Assert.Contains("AwaitSlowPath_0", generated);
    }

    [Fact]
    public void ValueTaskOfT_Compiles()
    {
        var source = """
            using dotweave;
            using System.Threading.Tasks;
            public class Svc
            {
                [Traced]
                public ValueTask<int> GetValueAsync() => new ValueTask<int>(42);
            }
            public class Caller
            {
                public async Task<int> Call(Svc svc) => await svc.GetValueAsync();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Intercepted_GetValueAsync_0", generated);
        Assert.Contains("IsCompletedSuccessfully", generated);
        Assert.Contains("__task.Result", generated);
    }

    [Fact]
    public void AsyncTaskOfT_NestedGeneric_Compiles()
    {
        var source = """
            using dotweave;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            public class Svc
            {
                [Traced]
                public async Task<Dictionary<string, int>> GetMapAsync()
                {
                    await Task.Delay(1);
                    return new Dictionary<string, int>();
                }
            }
            public class Caller
            {
                public async Task Call(Svc svc) => await svc.GetMapAsync();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("Task<System.Collections.Generic.Dictionary<string, int>>", generated);
    }

    [Fact]
    public void IntReturn_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public int Add(int a, int b) => a + b;
            }
            public class Caller
            {
                public int Call(Svc svc) => svc.Add(1, 2);
            }
            """;
        GeneratorTestHelper.RunAndVerifyCompilation(source);
    }
}
