---
title: "[Measured]"
description: Full reference for the [Measured] attribute — emitting OpenTelemetry metrics at call sites.
sidebar:
  order: 2
---

The `[Measured]` attribute instructs dotweave to record OpenTelemetry metrics at each call site of the decorated method.

## Usage

```csharp
[Measured]                                                      // counter + histogram
[Measured("custom.metric-name")]                                // explicit metric base name
[Measured(Calls = false)]                                       // duration histogram only
[Measured(InFlight = true)]                                     // add concurrency counter
[Measured(Tags = new[] { "endpoint=api", "tier=free" })]        // custom tags
[Measured(ErrorWhen = nameof(IsFailure))]                       // classify errors by return value
```

## Properties

| Property | Type | Default | Description |
|---|---|---|---|
| _(positional)_ | `string` | `"TypeName.MethodName"` | Custom metric base name |
| `Calls` | `bool` | `true` | Emit `{name}.calls` counter |
| `Duration` | `bool` | `true` | Emit `{name}.duration` histogram (milliseconds) |
| `InFlight` | `bool` | `false` | Emit `{name}.inflight` up/down counter for concurrency tracking |
| `Tags` | `string[]` | `null` | Custom `key=value` tags added to all recordings |
| `ErrorWhen` | `string` | `null` | Name of a `static bool` predicate on the same class. When it returns `true`, recordings are tagged `status="error"`. |

## Emitted instruments

Given `[Measured]` on `OrderService.PlaceOrderAsync`, dotweave emits up to three instruments on the `"dotweave.Metrics"` meter:

| Instrument | Type | Description |
|---|---|---|
| `OrderService.PlaceOrderAsync.calls` | Counter | Incremented once per call. Tagged `status="ok"` or `status="error"`. |
| `OrderService.PlaceOrderAsync.duration` | Histogram | Elapsed milliseconds per call. Same status tag. |
| `OrderService.PlaceOrderAsync.inflight` | UpDownCounter | +1 on entry, -1 on exit. Only emitted when `InFlight = true`. |

## Custom metric names

```csharp
[Measured("order.placement")]
public async Task<OrderResult> PlaceOrderAsync(...) { ... }
```

Emits `order.placement.calls`, `order.placement.duration`, and (if enabled) `order.placement.inflight`.

## Tags

Tags are `key=value` strings applied to all recordings:

```csharp
[Measured(Tags = new[] { "db=orders", "tier=premium" })]
public async Task<OrderResult> GetOrderAsync(string id) { ... }
```

Tags are added alongside the automatic `status` tag.

## ErrorWhen

```csharp
[Measured(ErrorWhen = nameof(IsFailed))]
public OrderResult GetOrder(string id) { ... }

public static bool IsFailed(OrderResult r) => !r.IsSuccess;
```

When `IsFailed(result)` returns `true`, the `status` tag is set to `"error"` instead of `"ok"`.

If the method throws an exception, `status` is also set to `"error"` regardless of `ErrorWhen`.

## Combining with [Traced]

`[Measured]` and `[Traced]` can be used together on the same method to get both spans and metrics from a single annotation.
