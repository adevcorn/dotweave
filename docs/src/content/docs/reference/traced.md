---
title: "[Traced]"
description: Full reference for the [Traced] attribute — creating OpenTelemetry spans at call sites.
sidebar:
  order: 1
---

The `[Traced]` attribute instructs dotweave to wrap each call site of the decorated method with an OpenTelemetry `Activity` (span).

## Usage

```csharp
[Traced]                                               // span name defaults to "TypeName.MethodName"
[Traced("custom.span-name")]                           // explicit span name
[Traced(Kind = ActivityKind.Client)]                   // set the span kind
[Traced("db.query", Kind = ActivityKind.Client)]       // both
[Traced(ErrorWhen = nameof(IsFailure))]                // mark span as Error based on return value
```

## Properties

| Property | Type | Default | Description |
|---|---|---|---|
| _(positional)_ | `string` | `"TypeName.MethodName"` | Custom span name |
| `Kind` | `ActivityKind` | `ActivityKind.Internal` | OTel span kind: `Internal`, `Client`, `Server`, `Producer`, `Consumer` |
| `ErrorWhen` | `string` | `null` | Name of a `static bool` predicate on the same class. When it returns `true`, the span status is set to `Error` — without requiring an exception to be thrown. |

## Accessing Activity.Current

Because the interceptor starts the span **before** invoking the original method, `Activity.Current` is set inside the method body. You can add tags and events directly:

```csharp
[Traced]
public string GetOrder(int id)
{
    Activity.Current?.SetTag("order.id", id);
    Activity.Current?.SetTag("db.table", "orders");
    return FetchFromDb(id);
}
```

## ErrorWhen

`ErrorWhen` lets you classify a result as an error without throwing. The predicate must be a `static bool` method on the same class, accepting the method's return type:

```csharp
[Traced("order.fetch", ErrorWhen = nameof(IsFailed))]
public OrderResult GetOrder(string id)
{
    // ...
}

public static bool IsFailed(OrderResult r) => !r.IsSuccess;
```

When `IsFailed(result)` returns `true`, the span status is set to `Error` with a descriptive message.

## On exception

Regardless of `ErrorWhen`, if the method throws, the interceptor:
1. Sets span status to `Error` with the exception message
2. Records an `exception` event with type, message, and stacktrace
3. Re-throws the exception

## Combining with [Measured]

`[Traced]` and `[Measured]` can be applied to the same method:

```csharp
[Traced]
[Measured]
public async Task<OrderResult> PlaceOrderAsync(string customerId, decimal amount)
{
    // ...
}
```
