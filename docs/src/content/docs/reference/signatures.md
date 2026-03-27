---
title: Supported Signatures
description: Which method signatures dotweave can and cannot instrument.
sidebar:
  order: 3
---

dotweave's generator handles a wide range of method signatures. Here is a complete breakdown.

## Supported

| Signature type | Notes |
|---|---|
| Sync `void` | Fully supported |
| Sync non-`void` (any return type) | Fully supported |
| `Task` | Awaited; exception captured on failure |
| `Task<T>` | Awaited; `ErrorWhen` receives the `T` result |
| `ValueTask` | Awaited; synchronous fast-path avoids state machine allocation when the task completes synchronously |
| `ValueTask<T>` | Same fast-path optimization; `ErrorWhen` receives the `T` result |
| Instance methods | Fully supported |
| Static methods | Fully supported |
| Extension methods | Fully supported |
| `ref` parameters | Fully supported |
| `out` parameters | Fully supported |
| `in` parameters | Fully supported |
| Interface methods | Attribute on the interface is resolved at all call sites |
| Override chains | Attribute on a base or concrete type is resolved correctly |

## Not supported

| Signature type | Diagnostic | Notes |
|---|---|---|
| Generic methods (`void Foo<T>(...)`) | `OTEL001` | C# interceptors cannot intercept open generic methods |
| `ref struct` parameters on async methods | `OTEL002` | The async state machine cannot capture `ref struct`s |

Both cases emit a **compile-time diagnostic** (warning/error) — the build does not silently produce broken instrumentation.

## ValueTask fast-path

For `ValueTask` and `ValueTask<T>`, dotweave checks `task.IsCompletedSuccessfully` before awaiting. If the task already completed synchronously (the common hot path for many value-returning operations), the interceptor avoids allocating a state machine entirely:

```csharp
// Simplified generated code for ValueTask<T>
var task = @this.GetValueAsync(id);
if (task.IsCompletedSuccessfully)
{
    var result = task.Result;
    // record metrics, check ErrorWhen
    return new ValueTask<T>(result);
}
// fall through to async path
return SlowPath(task, activity, ...);
```
