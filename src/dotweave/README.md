# dotweave

Compile-time OpenTelemetry instrumentation for .NET using C# interceptors and source generators.

## Install

```sh
dotnet add package dotweave
```

No manual `InterceptorsNamespaces` wiring is required.

## Usage

### 1. Annotate your methods

```csharp
using dotweave;
using System.Diagnostics;

public class OrderResult
{
    public bool IsSuccess { get; init; }
    public string? OrderId { get; init; }
}

public class OrderService
{
    // Span + metrics — default names: "OrderService.PlaceOrder"
    [Traced]
    [Measured]
    public async Task<OrderResult> PlaceOrderAsync(string customerId, decimal amount)
    {
        // Activity.Current is already set — add tags from inside the method
        Activity.Current?.SetTag("order.customer_id", customerId);
        Activity.Current?.SetTag("order.amount", amount);

        var id = await SubmitAsync(customerId, amount);
        return new OrderResult { IsSuccess = true, OrderId = id };
    }

    // Custom span name, Client span kind, error classification via predicate
    [Traced("db.query", Kind = ActivityKind.Client, ErrorWhen = nameof(IsFailedResult))]
    [Measured(ErrorWhen = nameof(IsFailedResult), InFlight = true, Tags = new[] { "db=orders" })]
    public async Task<OrderResult> GetOrderAsync(string orderId)
    {
        await Task.Delay(1);
        return new OrderResult { IsSuccess = true, OrderId = orderId };
    }

    // Metrics only — duration histogram, no calls counter, custom name
    [Measured("order.validation", Calls = false)]
    public bool ValidateOrder(string customerId, decimal amount)
        => !string.IsNullOrEmpty(customerId) && amount > 0;

    // Predicate: returns true when the result should be treated as an error
    public static bool IsFailedResult(OrderResult r) => !r.IsSuccess;

    private static Task<string> SubmitAsync(string c, decimal a)
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

Every call site is instrumented at compile time — no runtime reflection, no dynamic proxies.

## `[Traced]` attribute

Creates an `Activity` (OpenTelemetry span) around each call site.

- Default span name: `TypeName.MethodName`
- Pass a string to use a custom name: `[Traced("my.span")]`
- Set the span kind: `[Traced(Kind = ActivityKind.Client)]`
- Set span status to Error based on a predicate: `[Traced(ErrorWhen = nameof(IsFailure))]`
- On error: sets `ActivityStatusCode.Error` and adds an `exception` event with type, message, and stacktrace
- To add tags or events from inside the method body, use `Activity.Current` — the interceptor starts the span before the method runs, so it is always set when your code executes:

```csharp
[Traced]
public string GetOrder(int id)
{
    Activity.Current?.SetTag("order.id", id);
    // ...
}
```

| Property    | Type           | Default                   | Description                          |
|-------------|----------------|---------------------------|--------------------------------------|
| _(positional)_ | `string`    | `"TypeName.MethodName"`   | Custom span name                     |
| `Kind`      | `ActivityKind` | `ActivityKind.Internal`   | OTel span kind (Internal, Client, Server, Producer, Consumer) |
| `ErrorWhen` | `string`       | `null`                    | Name of a static bool predicate on the same class; when it returns `true` the span status is set to `Error` |

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
