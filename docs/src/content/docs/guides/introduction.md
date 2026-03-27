---
title: Introduction
description: What dotweave is and why you might want to use it.
sidebar:
  order: 1
---

dotweave is a compile-time OpenTelemetry (OTel) instrumentation library for .NET.

Mark your methods with `[Traced]` and `[Measured]`. dotweave's Roslyn source generator finds every call site that invokes those methods and emits **interceptor** code that wraps them with OpenTelemetry spans and metrics. The result is compiled C# — no reflection, no dynamic proxies, no runtime overhead.

## Key features

- **`[Traced]`** — wraps each call site with an `ActivitySource.StartActivity()` span. Configurable name, `ActivityKind`, and `ErrorWhen` predicate.
- **`[Measured]`** — emits a `.calls` counter, `.duration` histogram, and optional `.inflight` up/down counter at each call site. Supports custom tags and `ErrorWhen`.
- **Zero runtime overhead** — all instrumentation is compiled code.
- **AOT-compatible** — nothing dynamic, works with Native AOT.
- **Broad signature support** — sync, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`, `ref`/`out`/`in` parameters, instance and static methods.
- **Interface-aware** — applying attributes on interface methods or concrete implementations is resolved correctly regardless of the call-site reference type.

## Requirements

- .NET 10.0+ SDK (C# interceptors require `LangVersion=preview`)
- An OpenTelemetry-compatible backend, or the built-in demo dashboard for local development

## License

[MIT](https://github.com/adevcorn/dotweave/blob/main/LICENSE)
