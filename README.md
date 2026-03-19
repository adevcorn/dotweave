# dotweave

Compile-time OpenTelemetry instrumentation for .NET using C# interceptors and source generators.

Mark your methods with `[Traced]` and `[Measured]` — dotweave generates interceptor code at compile time that wraps every call site with OpenTelemetry spans and metrics. Zero reflection, zero runtime overhead, AOT-compatible.

## Install

```xml
<PackageReference Include="dotweave" Version="0.4.0" />
```

That's it. The package auto-configures `InterceptorsNamespaces` via MSBuild props.

## Quick start

```csharp
using dotweave;

public class GreetingService
{
    [Traced]
    [Measured]
    public string GetGreeting(string name) => $"Hello, {name}!";
}
```

Register the OpenTelemetry sources in your app:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("dotweave.Traced"))
    .WithMetrics(m => m.AddMeter("dotweave.Metrics"));
```

Every call to `GetGreeting` is now automatically wrapped in a tracing span and emits call count + duration metrics.

## Attributes

### `[Traced]`

Creates an `Activity` (OpenTelemetry span) around each call site.

```csharp
[Traced]                                        // span name defaults to "TypeName.MethodName"
[Traced("custom.span-name")]                    // explicit span name
[Traced(Kind = ActivityKind.Client)]            // set the span kind
[Traced("db.query", Kind = ActivityKind.Client)] // both
[Traced(ErrorWhen = nameof(IsFailure))]         // set span status=Error when predicate returns true
```

| Property    | Type           | Default                   | Description                          |
|-------------|----------------|---------------------------|--------------------------------------|
| _(positional)_ | `string`    | `"TypeName.MethodName"`   | Custom span name                     |
| `Kind`      | `ActivityKind` | `ActivityKind.Internal`   | OTel span kind (Internal, Client, Server, Producer, Consumer) |
| `ErrorWhen` | `string`       | `null`                    | Name of a static bool predicate on the same class; when it returns `true` the span status is set to `Error` |

The interceptor starts the span before invoking the method, so `Activity.Current` is set during the method body. Use it to add tags or events from inside the method:

```csharp
[Traced]
public string GetOrder(int id)
{
    Activity.Current?.SetTag("order.id", id);
    // ...
}
```

On error, the span status is set to `Error` with an `exception` event containing type, message, and stacktrace.

### `[Measured]`

Emits metrics for each call site. All options are optional.

```csharp
[Measured]                        // emits .calls counter + .duration histogram
[Measured("custom.metric-name")]  // explicit metric base name
[Measured(Calls = false)]         // duration only, no call counter
[Measured(InFlight = true)]       // adds .inflight UpDownCounter for concurrency tracking
[Measured(Tags = new[] { "endpoint=api", "tier=free" })]  // custom tags on all recordings
[Measured(ErrorWhen = nameof(IsFailure))]  // call a predicate to classify results as errors
```

| Property     | Type       | Default | Description                                    |
|--------------|------------|---------|------------------------------------------------|
| `Calls`      | `bool`     | `true`  | Emit `{name}.calls` counter                   |
| `Duration`   | `bool`     | `true`  | Emit `{name}.duration` histogram (ms)         |
| `InFlight`   | `bool`     | `false` | Emit `{name}.inflight` up/down counter        |
| `Tags`       | `string[]` | `null`  | Custom `key=value` tags on all recordings      |
| `ErrorWhen`  | `string`   | `null`  | Name of a static bool predicate on the same class; when it returns `true` the recording is tagged `status="error"` |

Both attributes can be combined on the same method.

## Supported method signatures

- Sync methods (void and non-void)
- `Task` / `Task<T>`
- `ValueTask` / `ValueTask<T>` (with synchronous fast-path optimization)
- Instance and static methods
- Methods with `ref`, `out`, `in` parameters

**Not supported:** Generic methods (diagnostic `OTEL001`) and ref struct parameters on async methods (diagnostic `OTEL002`).

## Diagnostics

| Code      | Description |
|-----------|-------------|
| `OTEL001` | Generic methods cannot be intercepted — remove `[Traced]`/`[Measured]` or make the method non-generic |
| `OTEL002` | Ref struct parameters on async methods are unsupported |
| `OTEL003` | `ErrorWhen` predicate not found or invalid — must be a `static bool` method on the same class accepting the method's return type |

## How it works

dotweave is a Roslyn incremental source generator. At compile time it:

1. Finds all methods marked with `[Traced]` or `[Measured]`
2. Locates every call site that invokes those methods
3. Emits interceptor methods with `[InterceptsLocation]` that replace the original calls

The generated interceptors wrap the original method call with `ActivitySource.StartActivity()` for tracing and `Counter`/`Histogram`/`UpDownCounter` recordings for metrics. No runtime reflection, no dynamic proxies — just compiled code.

## Project structure

```
src/
  dotweave/                  # NuGet meta-package (ships attributes + generator)
  dotweave.Attributes/       # [Traced] and [Measured] attribute definitions
  dotweave.Generator/        # Roslyn source generator
  HelloWorld.Api/            # Demo app with built-in telemetry dashboard
```

## Demo app

The `HelloWorld.Api` project is a working ASP.NET Core app with four instrumented endpoints and a built-in dashboard at `/dashboard`.

```bash
cd src/HelloWorld.Api
dotnet run
# Visit http://localhost:5199/dashboard
# Hit http://localhost:5199/hello/world        — sync, default instrumentation
# Hit http://localhost:5199/hello-async/world  — async with InFlight tracking
# Hit http://localhost:5199/hello-custom/world — custom span/metric names
# Hit http://localhost:5199/hello-static/world — static method interception
```

## Requirements

- .NET 10.0+ SDK (uses C# interceptors)
- An OpenTelemetry-compatible backend or the built-in dashboard for viewing telemetry

## License

[MIT](LICENSE)
