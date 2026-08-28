namespace OroQuizClash.Architecture.Tests;

/// <summary>
/// SPEC-016 (FR-023, SC-011): the Administration (Blazor) and Player (Angular) apps must
/// NEVER access the database directly. All persistence flows through QuizArena.Api.
/// This test scans the src/Admin and src/Player trees for forbidden data-access markers.
/// It is a filesystem scan (not reflection) because those apps may not yet be compiled
/// assemblies, and it must keep guarding them as they are implemented (SPEC-017 / SPEC-027).
/// </summary>
public sealed class DesignSystemNoDirectDbTests
{
    private static readonly string[] ForbiddenPatterns =
    {
        // EF Core
        "Microsoft.EntityFrameworkCore",
        "DbContext",
        "DbSet<",
        "IDbContextFactory",
        // raw ADO.NET / providers
        "System.Data.SqlClient",
        "Microsoft.Data.SqlClient",
        "SqlConnection",
        "Npgsql",
        "NpgsqlConnection",
        // ORMs / micro-ORMs
        "Dapper",
        // Connection strings embedded in the client apps
        "ConnectionStrings"
    };

    [Theory]
    [InlineData("src/Admin")]
    [InlineData("src/Player")]
    public void ClientApps_ShouldNotReferenceDataAccessDirectly(string relativePath)
    {
        var root = FindRepoRoot();
        var target = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        // Placeholder stage: directory may not exist yet — nothing to violate.
        if (!Directory.Exists(target))
        {
            return;
        }

        var sourceFiles = Directory.EnumerateFiles(target, "*.*", SearchOption.AllDirectories)
            .Where(f => IsSourceFile(f) && !IsBuildArtifact(f))
            .ToList();

        var violations = new List<string>();
        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            foreach (var pattern in ForbiddenPatterns)
            {
                if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{Relative(root, file)} -> '{pattern}'");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "Client apps must call QuizArena.Api, never the database directly (SPEC-016 FR-023). Violations:\n" +
            string.Join("\n", violations));
    }

    private static bool IsSourceFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext is ".cs" or ".razor" or ".ts" or ".tsx" or ".js" or ".html" or ".cshtml" or ".json";
    }

    // bin/obj contain restore/build artifacts (deps.json, dgspec.json) that list the
    // transitive closure of shared building blocks (e.g. ServiceDefaults → EF Core for
    // OTel). They are not source and cannot introduce direct DB access.
    private static bool IsBuildArtifact(string path)
    {
        var segments = path.Replace('\\', '/').Split('/');
        return segments.Contains("bin") || segments.Contains("obj");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("OroQuizClash.slnx").Any() ||
                dir.EnumerateDirectories(".git").Any())
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);
    }

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file);
}
