---
title: How It Works
description: The architecture behind dotweave's compile-time instrumentation.
sidebar:
  order: 4
---

dotweave is a **Roslyn incremental source generator**. It runs as part of the C# compilation pipeline and emits interceptor code before your binary is produced.

## The three-step process

### 1. Find annotated methods

The generator scans the compilation for methods decorated with `[Traced]` or `[Measured]`. It reads the attribute arguments (span name, `Kind`, `ErrorWhen`, metric options, etc.) and builds an internal model of each instrumented method.

### 2. Find every call site

Using Roslyn's semantic model, the generator locates every invocation of those methods across the entire compilation. It records the exact source file path and line/column so it can issue `[InterceptsLocation]` attributes.

### 3. Emit interceptors

For each call site, the generator emits a `static` method decorated with `[InterceptsLocation("file", line, column)]`. The C# compiler routes the call to the interceptor instead of the original method. The interceptor:

- Starts an `Activity` (for `[Traced]`)
- Increments an `.inflight` counter (if configured)
- Calls the original method
- Records duration and call count (for `[Measured]`)
- Sets span status on exception or based on `ErrorWhen`
- Disposes the activity

## C# interceptors

Interceptors are an experimental C# language feature (stable in .NET 8+ but still gated behind `LangVersion=preview`). They allow a static method to redirect calls at a specific source location. dotweave's MSBuild props enable this feature automatically.

```xml
<!-- Set automatically by the dotweave NuGet package -->
<InterceptorsNamespaces>$(InterceptorsNamespaces);dotweave.Generated</InterceptorsNamespaces>
```

## Why incremental?

Roslyn's incremental generator API means the generator only re-runs when relevant syntax nodes change. On subsequent builds, unchanged call sites are not re-processed, keeping build times fast.

## Project structure

```
src/
  dotweave/                  # NuGet meta-package (ships attributes + generator)
  dotweave.Attributes/       # [Traced] and [Measured] attribute definitions
  dotweave.Generator/        # Roslyn IIncrementalGenerator + code emitter
  HelloWorld.Api/            # Demo ASP.NET Core app with built-in OTel dashboard
```
