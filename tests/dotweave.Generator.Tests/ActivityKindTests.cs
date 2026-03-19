using System.Diagnostics;

namespace dotweave.Generator.Tests;

/// <summary>
/// Tests that the generator correctly passes ActivityKind to StartActivity
/// based on the Kind property of the [Traced] attribute.
/// </summary>
public class ActivityKindTests
{
    [Fact]
    public void DefaultKind_EmitsInternal()
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
        Assert.Contains("ActivityKind.Internal", generated);
    }

    [Fact]
    public void ExplicitInternalKind_EmitsInternal()
    {
        var source = """
            using dotweave;
            using System.Diagnostics;
            public class Svc
            {
                [Traced(Kind = ActivityKind.Internal)]
                public void DoWork() { }
            }
            public class Caller
            {
                public void Call(Svc svc) => svc.DoWork();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("ActivityKind.Internal", generated);
    }

    [Fact]
    public void ClientKind_EmitsClient()
    {
        var source = """
            using dotweave;
            using System.Diagnostics;
            public class Svc
            {
                [Traced(Kind = ActivityKind.Client)]
                public string CallDownstream() => "ok";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.CallDownstream();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("ActivityKind.Client", generated);
        Assert.DoesNotContain("ActivityKind.Internal", generated);
    }

    [Fact]
    public void ServerKind_EmitsServer()
    {
        var source = """
            using dotweave;
            using System.Diagnostics;
            public class Svc
            {
                [Traced(Kind = ActivityKind.Server)]
                public string HandleRequest() => "ok";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.HandleRequest();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("ActivityKind.Server", generated);
        Assert.DoesNotContain("ActivityKind.Internal", generated);
    }

    [Fact]
    public void ProducerKind_EmitsProducer()
    {
        var source = """
            using dotweave;
            using System.Diagnostics;
            public class Svc
            {
                [Traced(Kind = ActivityKind.Producer)]
                public void Publish() { }
            }
            public class Caller
            {
                public void Call(Svc svc) => svc.Publish();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("ActivityKind.Producer", generated);
        Assert.DoesNotContain("ActivityKind.Internal", generated);
    }

    [Fact]
    public void ConsumerKind_EmitsConsumer()
    {
        var source = """
            using dotweave;
            using System.Diagnostics;
            public class Svc
            {
                [Traced(Kind = ActivityKind.Consumer)]
                public void Process() { }
            }
            public class Caller
            {
                public void Call(Svc svc) => svc.Process();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("ActivityKind.Consumer", generated);
        Assert.DoesNotContain("ActivityKind.Internal", generated);
    }

    [Fact]
    public void ClientKind_WithCustomSpanName_BothApplied()
    {
        var source = """
            using dotweave;
            using System.Diagnostics;
            public class Svc
            {
                [Traced("my.span", Kind = ActivityKind.Client)]
                public string CallDownstream() => "ok";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.CallDownstream();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("\"my.span\"", generated);
        Assert.Contains("ActivityKind.Client", generated);
    }

    [Fact]
    public void ClientKind_AsyncTask_EmitsClient()
    {
        var source = """
            using dotweave;
            using System.Diagnostics;
            using System.Threading.Tasks;
            public class Svc
            {
                [Traced(Kind = ActivityKind.Client)]
                public async Task<string> CallDownstreamAsync()
                {
                    await Task.Delay(1);
                    return "ok";
                }
            }
            public class Caller
            {
                public async Task<string> Call(Svc svc) => await svc.CallDownstreamAsync();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("ActivityKind.Client", generated);
    }

    [Fact]
    public void ClientKind_ValueTask_EmitsClient()
    {
        var source = """
            using dotweave;
            using System.Diagnostics;
            using System.Threading.Tasks;
            public class Svc
            {
                [Traced(Kind = ActivityKind.Client)]
                public ValueTask<string> CallDownstreamAsync() => new ValueTask<string>("ok");
            }
            public class Caller
            {
                public ValueTask<string> Call(Svc svc) => svc.CallDownstreamAsync();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("ActivityKind.Client", generated);
    }
}
