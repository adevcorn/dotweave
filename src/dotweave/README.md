# dotweave

Single-package setup for OpenTelemetry method tracing with C# interceptors.

## Install

```xml
<PackageReference Include="dotweave" Version="0.1.0" />
```

No manual `InterceptorsNamespaces` wiring is required.

## Use

```csharp
using dotweave;

public class GreetingService
{
    [Traced]
    public string GetGreeting(string name) => $"Hello {name}";

    [Traced("custom.greeting")]
    [Measured("custom.greeting")]
    public string GetFancyGreeting(string name) => $"Greetings {name}";
}
```

Ensure your app registers OpenTelemetry tracing and includes:

```csharp
tracing.AddSource("dotweave.Traced");
```

For generated metrics, also register:

```csharp
metrics.AddMeter("dotweave.Metrics");
```
