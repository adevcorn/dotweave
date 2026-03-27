# dotweave

Compile-time OpenTelemetry instrumentation for .NET using C# interceptors and source generators.

Mark your methods with `[Traced]` and `[Measured]` — dotweave generates interceptor code at compile time that wraps every call site with OpenTelemetry spans and metrics. Zero reflection, zero runtime overhead, AOT-compatible.

**[Documentation site →](https://adevcorn.github.io/dotweave)**

## Install

```sh
dotnet add package dotweave
```

The package auto-configures `InterceptorsNamespaces` via MSBuild props. No other setup is needed.

## Quick start

### 1. Annotate your methods

```csharp
using dotweave;
using System.Diagnostics;

public class OrderResult
{
    public bool IsSuccess { get; init; }
    public string? OrderId { get; init; }
    public string? Error { get; init; }
}

public class OrderService
{
    // Traced span + metrics counter/histogram. Span name: "OrderService.PlaceOrder"
    [Traced]
    [Measured]
    public async Task<OrderResult> PlaceOrderAsync(string customerId, decimal amount)
    {
        // Activity.Current is already set — add tags directly from the method body
        Activity.Current?.SetTag("order.customer_id", customerId);
        Activity.Current?.SetTag("order.amount", amount);

        var orderId = await SubmitToBackendAsync(customerId, amount);
        return new OrderResult { IsSuccess = true, OrderId = orderId };
    }

    // Span kind = Client (outbound call), ErrorWhen marks the span as failed
    // when the result indicates a business-level error — without throwing.
    [Traced("db.query", Kind = ActivityKind.Client, ErrorWhen = nameof(IsFailedResult))]
    [Measured(ErrorWhen = nameof(IsFailedResult), InFlight = true,
              Tags = new[] { "db=orders" })]
    public async Task<OrderResult> GetOrderAsync(string orderId)
    {
        await Task.Delay(1); // simulate I/O
        return new OrderResult { IsSuccess = true, OrderId = orderId };
    }

    // Metrics only — no span. Duration histogram with a custom name.
    [Measured("order.validation", Calls = false)]
    public bool ValidateOrder(string customerId, decimal amount)
        => !string.IsNullOrEmpty(customerId) && amount > 0;

    public static bool IsFailedResult(OrderResult r) => !r.IsSuccess;

    private static Task<string> SubmitToBackendAsync(string customerId, decimal amount)
        => Task.FromResult(Guid.NewGuid().ToString());
}
```

### 2. Register the OTel sources

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource("dotweave.Traced")
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddMeter("dotweave.Metrics")
        .AddOtlpExporter());
```

That's it. Every call site is now instrumented at compile time — no runtime reflection, no dynamic proxies.

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
- Interface methods — `[Traced]`/`[Measured]` on an interface method is resolved at all call sites, including those using a concrete-typed variable
- Override chains — attribute on a base class method is resolved correctly at all call sites

**Not supported:** Generic methods (diagnostic `OTEL001`) and ref struct parameters on async methods (diagnostic `OTEL002`). Calling an interface method via an interface-typed variable when the attribute is only on the concrete implementation is also not supported (the generator cannot pick a concrete type at compile time).

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
