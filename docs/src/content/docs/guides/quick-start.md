---
title: Quick Start
description: Annotate your first method and see instrumentation in action.
sidebar:
  order: 3
---

## 1. Annotate your methods

Apply `[Traced]` and/or `[Measured]` to any method you want instrumented.

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
    // Traced span + metrics. Span name defaults to "OrderService.PlaceOrderAsync".
    [Traced]
    [Measured]
    public async Task<OrderResult> PlaceOrderAsync(string customerId, decimal amount)
    {
        // Activity.Current is set — add tags from inside the method body.
        Activity.Current?.SetTag("order.customer_id", customerId);
        Activity.Current?.SetTag("order.amount", amount);

        var orderId = await SubmitToBackendAsync(customerId, amount);
        return new OrderResult { IsSuccess = true, OrderId = orderId };
    }

    // Span kind = Client, ErrorWhen marks span as Error without throwing.
    [Traced("db.query", Kind = ActivityKind.Client, ErrorWhen = nameof(IsFailedResult))]
    [Measured(ErrorWhen = nameof(IsFailedResult), InFlight = true, Tags = new[] { "db=orders" })]
    public async Task<OrderResult> GetOrderAsync(string orderId)
    {
        await Task.Delay(1);
        return new OrderResult { IsSuccess = true, OrderId = orderId };
    }

    // Metrics only — duration histogram with a custom name, no call counter.
    [Measured("order.validation", Calls = false)]
    public bool ValidateOrder(string customerId, decimal amount)
        => !string.IsNullOrEmpty(customerId) && amount > 0;

    public static bool IsFailedResult(OrderResult r) => !r.IsSuccess;

    private static Task<string> SubmitToBackendAsync(string customerId, decimal amount)
        => Task.FromResult(Guid.NewGuid().ToString());
}
```

## 2. Register OTel sources

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource("dotweave.Traced")
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddMeter("dotweave.Metrics")
        .AddOtlpExporter());
```

## 3. Build and run

That's it. Build your project — dotweave's source generator emits interceptor code at every call site. No additional runtime setup is required.

Every call to `PlaceOrderAsync`, `GetOrderAsync`, and `ValidateOrder` is now automatically traced and/or measured wherever those methods are called — not just from within the class, but from every call site in your solution.

## What the generated code looks like

For each annotated method, dotweave emits something equivalent to:

```csharp
// Generated interceptor (simplified)
[InterceptsLocation("...")]
public static async Task<OrderResult> PlaceOrderAsync_Interceptor(
    OrderService @this, string customerId, decimal amount)
{
    using var activity = ActivitySource.StartActivity("OrderService.PlaceOrderAsync");
    try
    {
        var result = await @this.PlaceOrderAsync(customerId, amount);
        return result;
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        throw;
    }
}
```

No reflection, no boxing, no proxy objects — just plain compiled C#.
