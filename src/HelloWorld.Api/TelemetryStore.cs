using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTelemetry.Metrics;

namespace HelloWorld.Api;

/// <summary>
/// Collects Activity (trace) snapshots and metric snapshots in-memory
/// so we can serve them from JSON API endpoints for the dashboard.
/// </summary>
public sealed class TelemetryStore
{
    private readonly ConcurrentQueue<TraceRecord> _traces = new();
    private readonly ConcurrentDictionary<string, MetricSnapshot> _metrics = new();
    private const int MaxTraces = 500;

    // ---------- Traces ----------

    public void RecordActivity(Activity activity)
    {
        var record = new TraceRecord
        {
            TraceId = activity.TraceId.ToString(),
            SpanId = activity.SpanId.ToString(),
            ParentSpanId = activity.ParentSpanId == default ? null : activity.ParentSpanId.ToString(),
            OperationName = activity.OperationName,
            DisplayName = activity.DisplayName,
            Source = activity.Source.Name,
            StartTime = activity.StartTimeUtc,
            DurationMs = activity.Duration.TotalMilliseconds,
            Status = activity.Status.ToString(),
            StatusDescription = activity.StatusDescription,
            Tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value ?? ""),
            Events = activity.Events.Select(e => new TraceEvent
            {
                Name = e.Name,
                Timestamp = e.Timestamp.UtcDateTime,
                Tags = e.Tags.ToDictionary(t => t.Key, t => t.Value?.ToString() ?? "")
            }).ToList()
        };

        _traces.Enqueue(record);

        // Trim to max size
        while (_traces.Count > MaxTraces)
            _traces.TryDequeue(out _);
    }

    public List<TraceRecord> GetTraces() => _traces.ToList();

    // ---------- Metrics ----------

    public void RecordMetrics(IReadOnlyList<Metric> metrics)
    {
        foreach (var metric in metrics)
        {
            var snapshot = new MetricSnapshot
            {
                Name = metric.Name,
                Unit = metric.Unit,
                Description = metric.Description,
                MeterName = metric.MeterName,
                MetricType = metric.MetricType.ToString(),
                Timestamp = DateTime.UtcNow,
                DataPoints = new List<MetricDataPoint>()
            };

            foreach (ref readonly var point in metric.GetMetricPoints())
            {
                var dp = new MetricDataPoint
                {
                    Tags = new Dictionary<string, string>(),
                    StartTime = point.StartTime.UtcDateTime,
                    EndTime = point.EndTime.UtcDateTime
                };

                foreach (var tag in point.Tags)
                    dp.Tags[tag.Key] = tag.Value?.ToString() ?? "";

                switch (metric.MetricType)
                {
                    case MetricType.LongSum:
                        dp.Value = point.GetSumLong();
                        break;
                    case MetricType.DoubleSum:
                        dp.Value = point.GetSumDouble();
                        break;
                    case MetricType.LongGauge:
                        dp.Value = point.GetGaugeLastValueLong();
                        break;
                    case MetricType.DoubleGauge:
                        dp.Value = point.GetGaugeLastValueDouble();
                        break;
                    case MetricType.Histogram:
                        dp.Value = point.GetHistogramSum();
                        dp.Count = point.GetHistogramCount();
                        break;
                }

                snapshot.DataPoints.Add(dp);
            }

            _metrics[metric.Name] = snapshot;
        }
    }

    public List<MetricSnapshot> GetMetrics() => _metrics.Values.ToList();

    // ---------- JSON options ----------

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}

// ---------- DTOs ----------

public record TraceRecord
{
    public string TraceId { get; init; } = "";
    public string SpanId { get; init; } = "";
    public string? ParentSpanId { get; init; }
    public string OperationName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Source { get; init; } = "";
    public DateTime StartTime { get; init; }
    public double DurationMs { get; init; }
    public string Status { get; init; } = "";
    public string? StatusDescription { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new();
    public List<TraceEvent> Events { get; init; } = new();
}

public record TraceEvent
{
    public string Name { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new();
}

public record MetricSnapshot
{
    public string Name { get; init; } = "";
    public string? Unit { get; init; }
    public string? Description { get; init; }
    public string MeterName { get; init; } = "";
    public string MetricType { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public List<MetricDataPoint> DataPoints { get; init; } = new();
}

public record MetricDataPoint
{
    public Dictionary<string, string> Tags { get; init; } = new();
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public double Value { get; set; }
    public long? Count { get; set; }
}
