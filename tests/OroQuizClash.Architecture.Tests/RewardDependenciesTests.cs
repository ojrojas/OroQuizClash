using System.Reflection;

namespace OroQuizClash.Architecture.Tests;

public sealed class RewardDependenciesTests
{
    [Fact]
    public void Reward_ShouldNotReferenceInfrastructure()
    {
        var domainAssembly = typeof(OroQuizClash.Domain.Rewards.Reward).Assembly;
        var infraAssembly = typeof(OroQuizClash.Infrastructure.Persistence.OroQuizClashDbContext).Assembly;

        var references = domainAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, a => a.FullName == infraAssembly.FullName);
    }

    [Fact]
    public void Reward_ShouldNotExposePublicSetters()
    {
        var type = typeof(OroQuizClash.Domain.Rewards.Reward);
        var mutableProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .ToList();
        Assert.Empty(mutableProps);
    }

    [Fact]
    public void RewardRedemption_ShouldNotExposePublicSetters()
    {
        var type = typeof(OroQuizClash.Domain.Rewards.RewardRedemption);
        var mutableProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .ToList();
        Assert.Empty(mutableProps);
    }

    [Fact]
    public void RewardStatus_ShouldBeEnumeration()
    {
        var type = typeof(OroQuizClash.Domain.Rewards.RewardStatus);
        Assert.True(typeof(BuildingBlocks.Kernel.Domain.Enumerations.Enumeration<OroQuizClash.Domain.Rewards.RewardStatus>).IsAssignableFrom(type));
    }

    [Fact]
    public void RedemptionStatus_ShouldBeEnumeration()
    {
        var type = typeof(OroQuizClash.Domain.Rewards.RedemptionStatus);
        Assert.True(typeof(BuildingBlocks.Kernel.Domain.Enumerations.Enumeration<OroQuizClash.Domain.Rewards.RedemptionStatus>).IsAssignableFrom(type));
    }

    [Fact]
    public void RewardStatus_ShouldHaveTwoStates()
    {
        var all = OroQuizClash.Domain.Rewards.RewardStatus.GetAll();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void RedemptionStatus_ShouldHaveFiveStates()
    {
        var all = OroQuizClash.Domain.Rewards.RedemptionStatus.GetAll();
        Assert.Equal(5, all.Count);
    }

    [Fact]
    public void RewardSlices_ShouldImplementICommandOrIQuery()
    {
        var assembly = typeof(OroQuizClash.Application.Features.Rewards.RedeemRewardHandler).Assembly;
        var sliceTypes = assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("OroQuizClash.Application.Features.Rewards") == true)
            .ToList();

        var handlerTypes = sliceTypes.Where(t => t.Name.EndsWith("Handler")).ToList();
        foreach (var handler in handlerTypes)
        {
            var interfaces = handler.GetInterfaces();
            var implementsCqrs = interfaces.Any(i =>
                i.Name.Contains("ICommandHandler") || i.Name.Contains("IQueryHandler"));
            Assert.True(implementsCqrs, $"{handler.Name} should implement ICommandHandler or IQueryHandler");
        }
    }

    [Fact]
    public void RewardSlices_ShouldNotUseMediatR()
    {
        var assembly = typeof(OroQuizClash.Application.Features.Rewards.RedeemRewardHandler).Assembly;
        var types = assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("OroQuizClash.Application.Features.Rewards") == true);

        foreach (var type in types)
        {
            var references = type.Assembly.GetReferencedAssemblies();
            Assert.DoesNotContain(references, a => a.Name == "MediatR");
            Assert.DoesNotContain(references, a => a.Name == "AutoMapper");
        }
    }
}
