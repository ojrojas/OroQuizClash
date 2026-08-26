namespace BuildingBlocks.Logger.Options;

public sealed class LoggerOptions
{
    public const string SectionName = "Logging:BuildingBlocks";

    public string ApplicationName { get; set; } = "BuildingBlocks.App";
    public string Environment { get; set; } = "Development";
    public string MinimumLevel { get; set; } = "Information";

    public ConsoleSinkOptions Console { get; set; } = new();
    public FileSinkOptions File { get; set; } = new();
    public LokiSinkOptions Loki { get; set; } = new();
    public SeqSinkOptions Seq { get; set; } = new();
}

public sealed class ConsoleSinkOptions
{
    public bool Enabled { get; set; } = true;
    public string OutputTemplate { get; set; } =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}";
}

public sealed class FileSinkOptions
{
    public bool Enabled { get; set; }
    public string Path { get; set; } = "logs/app-.log";
    public string RollingInterval { get; set; } = "Day";
    public int RetainedFileCountLimit { get; set; } = 14;
    public string OutputTemplate { get; set; } =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";
}

public sealed class LokiSinkOptions
{
    public bool Enabled { get; set; }
    public string Url { get; set; } = "http://localhost:3100";
    public Dictionary<string, string> Labels { get; set; } = new();
    public string? Tenant { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class SeqSinkOptions
{
    public bool Enabled { get; set; }
    public string ServerUrl { get; set; } = "http://localhost:5341";
    public string? ApiKey { get; set; }
}
