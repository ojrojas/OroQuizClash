using OroQuizClash.Domain.Authorization;

namespace OroQuizClash.Architecture.Tests;

public sealed class SecurityDependencyTests
{
    [Fact]
    public void Domain_ShouldNotReferenceAspNetCore()
    {
        var domainAssembly = typeof(Permission).Assembly;
        var refs = domainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.DoesNotContain(refs, r => r != null && r.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Permission_ShouldHave14Values()
    {
        Assert.Equal(14, Permission.All.Count);
    }

    [Fact]
    public void Role_ShouldHave4Values()
    {
        Assert.Equal(4, Role.All.Count);
    }

    [Fact]
    public void SecurityPolicies_ShouldDefine14Policies()
    {
        var count = OroQuizClash.Api.Authorization.SecurityPolicies.PolicyRoles.Count;
        Assert.Equal(14, count);
    }
}
