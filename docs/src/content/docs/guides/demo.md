---
title: Demo App
description: Run the HelloWorld.Api demo to see dotweave in action with a built-in telemetry dashboard.
sidebar:
  order: 5
---

The `HelloWorld.Api` project is a working ASP.NET Core app with four instrumented endpoints and a **built-in telemetry dashboard** — no external OTel backend required.

## Running the demo

```bash
cd src/HelloWorld.Api
dotnet run
```

Then open:

| URL | Description |
|---|---|
| `http://localhost:5199/dashboard` | Live telemetry dashboard |
| `http://localhost:5199/hello/world` | Sync method, default instrumentation |
| `http://localhost:5199/hello-async/world` | Async method with `InFlight` tracking |
| `http://localhost:5199/hello-custom/world` | Custom span and metric names |
| `http://localhost:5199/hello-static/world` | Static method interception |

## The dashboard

The dashboard is a self-contained single-page app served directly by the API at `/dashboard`. It:

- Auto-polls `/telemetry/traces` and `/telemetry/metrics` every 2 seconds
- Shows a **metrics card grid** with call counts, durations, and error rates
- Shows a **traces table** with expandable tag and event detail rows
- Shows a **waterfall view** — span timelines grouped per trace
- Has a "Send Requests" button to generate sample traffic
- Requires zero external dependencies — all HTML, CSS, and JS is inline

This makes it easy to verify dotweave instrumentation works end-to-end without configuring Jaeger, Prometheus, or any other backend.
