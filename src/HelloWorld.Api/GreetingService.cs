using dotweave;

public class GreetingService
{
    private readonly ILogger<GreetingService> _logger;

    public GreetingService(ILogger<GreetingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Simple sync method — default [Measured] emits both calls + duration.
    /// </summary>
    [Traced]
    [Measured]
    public string GetGreeting(string name)
    {
        _logger.LogInformation("Generating greeting for {Name}", name);
        return $"Hello, {name}! Welcome to the OTel-traced API.";
    }

    /// <summary>
    /// Async method — uses InFlight to track concurrent executions,
    /// plus custom tags for dashboard filtering.
    /// </summary>
    [Traced]
    [Measured(InFlight = true, Tags = new[] { "endpoint=hello-async", "tier=standard" })]
    public async Task<string> GetGreetingAsync(string name)
    {
        _logger.LogInformation("Starting async greeting for {Name}", name);
        await Task.Delay(100); // simulate async work
        _logger.LogDebug("Async work completed for {Name}", name);
        return $"Hello (async), {name}! This was traced with OpenTelemetry.";
    }

    /// <summary>
    /// Uses custom span/metric names, duration-only (no calls counter),
    /// with custom tags.
    /// </summary>
    [Traced("custom.fancy-greeting")]
    [Measured("custom.fancy-greeting", Calls = false, Tags = new[] { "style=fancy" })]
    public string GetFancyGreeting(string name)
    {
        _logger.LogInformation("Generating fancy greeting for {Name} with style={Style}", name, "fancy");
        return $"Greetings and salutations, {name}!";
    }
}
