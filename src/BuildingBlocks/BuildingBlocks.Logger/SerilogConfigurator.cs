namespace BuildingBlocks.Logger;

public static class SerilogConfigurator
{
    public static LoggerConfiguration Configure(LoggerConfiguration cfg, LoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(options);

        var min = Enum.TryParse<LogEventLevel>(options.MinimumLevel, true, out var level) ? level : LogEventLevel.Information;

        cfg.MinimumLevel.Is(min)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", options.ApplicationName)
            .Enrich.WithProperty("Environment", options.Environment)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning);

        if (options.Console.Enabled)
            cfg.WriteTo.Console(outputTemplate: options.Console.OutputTemplate);

        if (options.File.Enabled)
        {
            var interval = Enum.TryParse<RollingInterval>(options.File.RollingInterval, true, out var ri) ? ri : RollingInterval.Day;
            cfg.WriteTo.File(
                path: options.File.Path,
                rollingInterval: interval,
                retainedFileCountLimit: options.File.RetainedFileCountLimit,
                outputTemplate: options.File.OutputTemplate);
        }

        if (options.Loki.Enabled)
        {
            var labels = options.Loki.Labels
                .Select(kv => new LokiLabel { Key = kv.Key, Value = kv.Value })
                .Concat(new[]
                {
                    new LokiLabel { Key = "app", Value = options.ApplicationName },
                    new LokiLabel { Key = "env", Value = options.Environment }
                })
                .ToArray();

            LokiCredentials? creds = options.Loki.Username is { Length: > 0 }
                ? new LokiCredentials { Login = options.Loki.Username, Password = options.Loki.Password ?? string.Empty }
                : null;

            cfg.WriteTo.GrafanaLoki(
                uri: options.Loki.Url,
                labels: labels,
                credentials: creds,
                tenant: options.Loki.Tenant);
        }

        if (options.Seq.Enabled)
            cfg.WriteTo.Seq(serverUrl: options.Seq.ServerUrl, apiKey: options.Seq.ApiKey);

        return cfg;
    }
}