---
title: Diagnostics
description: Compile-time diagnostic codes emitted by dotweave.
sidebar:
  order: 4
---

dotweave reports problems at **compile time** via standard Roslyn diagnostics. These appear in your IDE and build output just like any other C# diagnostic.

## Diagnostic codes

| Code | Severity | Description |
|---|---|---|
| `OTEL001` | Error | **Generic method** — C# interceptors cannot intercept open generic methods. Remove `[Traced]`/`[Measured]`, or make the method non-generic. |
| `OTEL002` | Error | **Ref struct on async method** — async state machines cannot capture `ref struct` parameters. Remove the attribute or change the parameter type. |
| `OTEL003` | Error | **Invalid `ErrorWhen` predicate** — the named method was not found, is not `static`, does not return `bool`, or does not accept the method's return type as its sole parameter. |

## OTEL001 — Generic method

```csharp
// ERROR: OTEL001
[Traced]
public T GetItem<T>(int id) { ... }
```

**Fix:** Remove the attribute from generic methods, or extract a non-generic wrapper.

## OTEL002 — Ref struct on async

```csharp
// ERROR: OTEL002
[Traced]
public async Task ProcessAsync(ReadOnlySpan<byte> data) { ... }
```

**Fix:** Use `ReadOnlyMemory<byte>` or another non-ref-struct type instead of `Span`/`ReadOnlySpan` on async methods.

## OTEL003 — Invalid ErrorWhen predicate

```csharp
// ERROR: OTEL003 — "IsFailure" not found or signature mismatch
[Traced(ErrorWhen = nameof(IsFailure))]
public OrderResult GetOrder(string id) { ... }
```

The predicate must satisfy all of:

1. **Same class** as the annotated method
2. **`static`**
3. **Returns `bool`**
4. **Accepts exactly one parameter** whose type matches the method's return type

```csharp
// Correct predicate
public static bool IsFailure(OrderResult r) => !r.IsSuccess;
```
