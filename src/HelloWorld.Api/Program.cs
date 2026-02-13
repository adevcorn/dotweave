using System.Diagnostics;
using HelloWorld.Api;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
if (!args.Any(a => a.StartsWith("--urls")) && builder.Configuration["urls"] is null)
    builder.WebHost.UseUrls("http://localhost:5199");
const string TracedSourceName = "dotweave.Traced";
const string MeasuredMeterName = "dotweave.Metrics";

var serviceName =
    builder.Configuration["OTEL_SERVICE_NAME"] ??
    builder.Environment.ApplicationName;

// In-memory telemetry store for the dashboard.
var telemetryStore = new TelemetryStore();
builder.Services.AddSingleton(telemetryStore);

// Listen for completed activities and forward to the store
var activityListener = new ActivityListener
{
    ShouldListenTo = source => source.Name == TracedSourceName ||
                                source.Name == "Microsoft.AspNetCore",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStopped = activity => telemetryStore.RecordActivity(activity)
};
ActivitySource.AddActivityListener(activityListener);

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(TracedSourceName)
            .AddAspNetCoreInstrumentation();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(MeasuredMeterName)
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(new List<Metric>(), options =>
            {
                options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 2000;
            })
            .AddReader(new PeriodicExportingMetricReader(
                new TelemetryStoreMetricExporter(telemetryStore),
                exportIntervalMilliseconds: 2000));
    });

builder.Services.AddSingleton<GreetingService>();

var app = builder.Build();

// ----- API endpoints -----

app.MapGet("/hello/{name}", (string name, GreetingService svc) =>
{
    var greeting = svc.GetGreeting(name);
    return Results.Ok(new { message = greeting });
});

app.MapGet("/hello-async/{name}", async (string name, GreetingService svc) =>
{
    var greeting = await svc.GetGreetingAsync(name);
    return Results.Ok(new { message = greeting });
});

app.MapGet("/hello-custom/{name}", (string name, GreetingService svc) =>
{
    var greeting = svc.GetFancyGreeting(name);
    return Results.Ok(new { message = greeting });
});

// ----- Telemetry API endpoints -----

app.MapGet("/telemetry/traces", (TelemetryStore store) =>
    Results.Json(store.GetTraces(), TelemetryStore.JsonOptions));

app.MapGet("/telemetry/metrics", (TelemetryStore store) =>
    Results.Json(store.GetMetrics(), TelemetryStore.JsonOptions));

// ----- Dashboard -----

app.MapGet("/dashboard", () =>
    Results.Content(DashboardHtml.Content, "text/html"));

app.Run();

// Dispose the activity listener on shutdown
activityListener.Dispose();
