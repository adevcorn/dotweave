# dotweave

Compile-time OpenTelemetry instrumentation for .NET using C# interceptors and source generators.

## Install

```xml
<PackageReference Include="dotweave" Version="0.4.0" />
```

No manual `InterceptorsNamespaces` wiring is required.

## Usage

```csharp
using dotweave;

public class GreetingService
{
    [Traced]
    [Measured]
    public string GetGreeting(string name) => $"Hello {name}";

    [Traced("custom.greeting")]
    [Measured("custom.greeting", InFlight = true, Tags = new[] { "style=fancy" })]
    public async Task<string> GetFancyGreetingAsync(string name)
    {
        await Task.Delay(100);
        return $"Greetings, {name}!";
    }
}
```

Register the OpenTelemetry sources:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("dotweave.Traced"))
    .WithMetrics(m => m.AddMeter("dotweave.Metrics"));
```

## `[Traced]` attribute

Creates an `Activity` (OpenTelemetry span) around each call site.

- Default span name: `TypeName.MethodName`
- Pass a string to use a custom name: `[Traced("my.span")]`
- On error: sets `ActivityStatusCode.Error` and adds an `exception` event with type, message, and stacktrace

## `[Measured]` attribute

Emits metrics for each call site.

| Property     | Type       | Default | Description                                |
|--------------|------------|---------|--------------------------------------------|
| `Calls`      | `bool`     | `true`  | Emit `{name}.calls` counter               |
| `Duration`   | `bool`     | `true`  | Emit `{name}.duration` histogram (ms)     |
| `InFlight`   | `bool`     | `false` | Emit `{name}.inflight` up/down counter    |
| `Tags`       | `string[]` | `null`  | Custom `key=value` tags on all recordings  |
| `ErrorWhen`  | `string`   | `null`  | Name of a static bool predicate on the same class; when it returns `true` the recording is tagged `status="error"` |

## Diagnostics

| Code      | Description                                     |
|-----------|-------------------------------------------------|
| `OTEL001` | Generic methods cannot be intercepted           |
| `OTEL002` | Ref struct parameters on async methods unsupported |
| `OTEL003` | `ErrorWhen` predicate not found or invalid — must be a `static bool` method on the same class accepting the method's return type |

## License

[MIT](https://github.com/adevcorn/dotweave/blob/main/LICENSE)
