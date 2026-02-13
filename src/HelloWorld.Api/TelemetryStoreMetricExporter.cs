using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace HelloWorld.Api;

/// <summary>
/// A metric exporter that pushes snapshots into our TelemetryStore.
/// </summary>
public sealed class TelemetryStoreMetricExporter : BaseExporter<Metric>
{
    private readonly TelemetryStore _store;

    public TelemetryStoreMetricExporter(TelemetryStore store)
    {
        _store = store;
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        var metrics = new List<Metric>();
        foreach (var metric in batch)
        {
            metrics.Add(metric);
        }
        _store.RecordMetrics(metrics);
        return ExportResult.Success;
    }
}
