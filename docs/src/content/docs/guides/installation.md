---
title: Installation
description: How to add dotweave to a .NET project.
sidebar:
  order: 2
---

## NuGet package

```sh
dotnet add package dotweave
```

This single package bundles:
- `dotweave.Attributes` — the `[Traced]` and `[Measured]` attribute definitions
- `dotweave.Generator` — the Roslyn incremental source generator

The package includes MSBuild `.props` files that automatically configure `InterceptorsNamespaces` for your project. No other setup is needed.

## Register the OTel sources

dotweave emits traces on the `"dotweave.Traced"` `ActivitySource` and metrics on the `"dotweave.Metrics"` `Meter`. Register both with your OTel SDK setup:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource("dotweave.Traced")
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddMeter("dotweave.Metrics")
        .AddOtlpExporter());
```

Replace `.AddOtlpExporter()` with whichever exporter your backend requires (Jaeger, Zipkin, Console, etc.).

## Requirements

| Requirement | Version |
|---|---|
| .NET SDK | 10.0+ |
| C# language version | `preview` (set automatically by the package) |
