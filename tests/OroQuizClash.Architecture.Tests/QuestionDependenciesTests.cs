using System.Reflection;

namespace OroQuizClash.Architecture.Tests;

public sealed class QuestionDependenciesTests
{
    [Fact]
    public void Question_ShouldNotReferenceInfrastructureOrWeb()
    {
        var domainAssembly = typeof(OroQuizClash.Domain.Questions.Question).Assembly;
        var referenced = domainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        var forbidden = new[] { "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "RabbitMQ.Client", "MediatR", "MassTransit", "AutoMapper" };
        foreach (var f in forbidden)
        {
            Assert.DoesNotContain(referenced, r => r != null && r.StartsWith(f, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Question_ShouldBeAggregateRoot()
    {
        var type = typeof(OroQuizClash.Domain.Questions.Question);
        Assert.True(typeof(BuildingBlocks.Kernel.Domain.Entities.AggregateRoot<OroQuizClash.Domain.Questions.QuestionId>).IsAssignableFrom(type));
    }
}
