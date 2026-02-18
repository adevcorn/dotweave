namespace dotweave.Generator.Tests;

/// <summary>
/// Tests that the generator correctly handles params arrays and optional parameters
/// with default values in generated interceptor signatures.
/// </summary>
public class ParameterTests
{
    [Fact]
    public void OptionalStringParameter_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public string Greet(string name, string prefix = "Hello") => $"{prefix}, {name}!";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.Greet("World");
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("= \"Hello\"", generated);
    }

    [Fact]
    public void OptionalIntParameter_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public int Add(int a, int b = 10) => a + b;
            }
            public class Caller
            {
                public int Call(Svc svc) => svc.Add(5);
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("= 10", generated);
    }

    [Fact]
    public void OptionalBoolParameter_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public string GetValue(bool flag = true) => flag ? "yes" : "no";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("= true", generated);
    }

    [Fact]
    public void OptionalNullDefault_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public string GetValue(string? name = null) => name ?? "default";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue();
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("= default", generated);
    }

    [Fact]
    public void ParamsArray_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public int Sum(params int[] values)
                {
                    int sum = 0;
                    foreach (var v in values) sum += v;
                    return sum;
                }
            }
            public class Caller
            {
                public int Call(Svc svc) => svc.Sum(1, 2, 3);
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("params int[]", generated);
    }

    [Fact]
    public void RefParameter_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public void Increment(ref int value) { value++; }
            }
            public class Caller
            {
                public void Call(Svc svc)
                {
                    int x = 0;
                    svc.Increment(ref x);
                }
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("ref int", generated);
    }

    [Fact]
    public void OutParameter_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public bool TryParse(string input, out int result)
                {
                    result = 0;
                    return int.TryParse(input, out result);
                }
            }
            public class Caller
            {
                public bool Call(Svc svc)
                {
                    return svc.TryParse("123", out int val);
                }
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("out int", generated);
    }

    [Fact]
    public void InParameter_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public int ReadOnly(in int value) => value * 2;
            }
            public class Caller
            {
                public int Call(Svc svc)
                {
                    int x = 5;
                    return svc.ReadOnly(in x);
                }
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("in int", generated);
    }

    [Fact]
    public void KeywordParameterName_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public string GetValue(string @class) => @class;
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.GetValue("test");
            }
            """;
        GeneratorTestHelper.RunAndVerifyCompilation(source);
    }

    [Fact]
    public void MultipleParameters_Compiles()
    {
        var source = """
            using dotweave;
            public class Svc
            {
                [Traced]
                public string Format(string name, int age, bool active = true) => $"{name} {age} {active}";
            }
            public class Caller
            {
                public string Call(Svc svc) => svc.Format("Alice", 30);
            }
            """;
        var generated = GeneratorTestHelper.RunAndVerifyCompilation(source);
        Assert.Contains("= true", generated);
    }
}
