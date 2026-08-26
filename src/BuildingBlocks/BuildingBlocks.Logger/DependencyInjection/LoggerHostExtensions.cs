namespace BuildingBlocks.Logger.DependencyInjection;

public static class LoggerHostExtensions
{
    /// <summary>Adds Serilog as the host logger with sinks configured from <see cref="LoggerOptions"/>.</summary>
    public static IHostBuilder UseBuildingBlocksLogger(this IHostBuilder host, Action<LoggerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        return host.UseSerilog((context, services, cfg) =>
        {
            var options = ResolveOptions(context.Configuration, configure);
            SerilogConfigurator.Configure(cfg, options);
        });
    }

    /// <summary>Adds Serilog services for non-host scenarios (tests, libraries).</summary>
    public static IServiceCollection AddBuildingBlocksLogger(this IServiceCollection services, IConfiguration configuration, Action<LoggerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = ResolveOptions(configuration, configure);
        services.AddSingleton(options);
        services.AddSerilog((sp, cfg) => SerilogConfigurator.Configure(cfg, options));
        return services;
    }

    private static LoggerOptions ResolveOptions(IConfiguration configuration, Action<LoggerOptions>? configure)
    {
        var options = new LoggerOptions();
        configuration?.GetSection(LoggerOptions.SectionName).Bind(options);
        configure?.Invoke(options);
        return options;
    }
}