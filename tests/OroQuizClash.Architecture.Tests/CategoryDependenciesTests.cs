using System.Reflection;

namespace OroQuizClash.Architecture.Tests;

public sealed class CategoryDependenciesTests
{
    [Fact]
    public void Category_ShouldNotReferenceInfrastructureOrWeb()
    {
        var domainAssembly = typeof(OroQuizClash.Domain.Categories.Category).Assembly;
        var referenced = domainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        var forbidden = new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "RabbitMQ.Client", "MediatR", "MassTransit", "AutoMapper" };
        foreach (var f in forbidden)
        {
            Assert.DoesNotContain(referenced, r => r != null && r.StartsWith(f, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Category_ShouldBeAggregateRoot()
    {
        var type = typeof(OroQuizClash.Domain.Categories.Category);
        Assert.True(typeof(BuildingBlocks.Kernel.Domain.Entities.AggregateRoot<OroQuizClash.Domain.Categories.CategoryId>).IsAssignableFrom(type));
    }
}