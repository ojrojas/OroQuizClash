using System.Reflection;

namespace OroQuizClash.Architecture.Tests;

public sealed class DomainDependenciesTests
{
    [Fact]
    public void Domain_ShouldNotReferenceForbiddenAssemblies()
    {
        var domainAssembly = typeof(OroQuizClash.Domain.Games.Game).Assembly;
        var referenced = domainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        var forbidden = new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "RabbitMQ.Client", "MediatR", "MassTransit", "AutoMapper" };
        foreach (var f in forbidden)
        {
            Assert.DoesNotContain(referenced, r => r != null && r.StartsWith(f, StringComparison.OrdinalIgnoreCase));
        }
    }
}