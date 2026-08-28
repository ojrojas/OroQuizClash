namespace OroQuizClash.Architecture.Tests;

/// <summary>
/// SPEC-017 (T035, SC-003, FR-030): the admin WASM client must talk to the BFF only.
/// (a) complements DesignSystemNoDirectDbTests for source files,
/// (b) no absolute API URLs in the client (only same-origin /bff and /hubs routes),
/// (c) no token-bearing authentication packages in the client project.
/// </summary>
public sealed class AdminBffTests
{
    private static readonly string[] SourceExtensions =
        [".cs", ".razor", ".ts", ".tsx", ".js", ".html", ".cshtml", ".json"];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("OroQuizClash.slnx").Any())
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repository root not found.");
    }

    private static List<string> SourceFiles(string relativeRoot)
    {
        var root = Path.Combine(RepoRoot(), relativeRoot.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(root))
        {
            return [];
        }
        return Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => SourceExtensions.Contains(Path.GetExtension(f)))
            .Where(f => !f.Replace('\\', '/').Split('/').Contains("bin"))
            .Where(f => !f.Replace('\\', '/').Split('/').Contains("obj"))
            .ToList();
    }

    [Fact]
    public void ClientProject_ContainsNoAbsoluteApiUrls()
    {
        var violations = new List<string>();
        foreach (var file in SourceFiles("src/Admin/QuizArena.Admin.Client"))
        {
            var content = File.ReadAllText(file);
            foreach (var marker in new[] { "oroclash-api", "quizarena-api", "http://localhost/api", "https://localhost/api" })
            {
                if (content.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{Path.GetRelativePath(RepoRoot(), file)} -> '{marker}'");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "The WASM client must call same-origin /bff/* and /hubs/game only (contracts/bff-endpoints.md §5):\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void ClientProject_ReferencesNoTokenBearingAuthPackages()
    {
        var csproj = Path.Combine(RepoRoot(), "src/Admin/QuizArena.Admin.Client/QuizArena.Admin.Client.csproj");
        var content = File.ReadAllText(csproj);

        foreach (var forbidden in new[]
                 {
                     "Microsoft.AspNetCore.Authentication.OpenIdConnect",
                     "Microsoft.IdentityModel",
                     "System.IdentityModel"
                 })
        {
            Assert.DoesNotContain(forbidden, content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ClientProject_UsesBffRelativeRoutesOnly()
    {
        var servicesDir = Path.Combine(RepoRoot(), "src/Admin/QuizArena.Admin.Client/Services");
        var violations = Directory.EnumerateFiles(servicesDir, "*.cs")
            .Where(f => File.ReadAllText(f).Contains("new Uri(\"http", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(RepoRoot(), f))
            .ToList();

        Assert.True(violations.Count == 0,
            "Client services must use relative /bff routes (base address = own origin):\n" +
            string.Join("\n", violations));
    }
}
