using BuildingBlocks.Kernel.Domain.Repositories;

using NSubstitute;

using OroQuizClash.Application.Features.Games;
using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;

namespace OroQuizClash.Application.Tests.Features.Games;

public sealed class CreateGameHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidCommand_Succeeds()
    {
        var repo = Substitute.For<IRepository<Game, GameId>>();
        var validator = Substitute.For<ICategoryValidator>();
        validator.ExistsAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>()).Returns(true);
        validator.IsPublishedAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>()).Returns(true);
        var uow = Substitute.For<IUnitOfWork>();
        var handler = new CreateGameHandler(repo, validator, uow);
        var cmd = new CreateGameCommand("Quiz", Guid.NewGuid(), 5, 10, 1, "Linear", 30, "Standard", "LOSE_ALL", "KEEP_CURRENT_SCORE", "None", "Points", 500, 2, 10);
        var result = await handler.HandleAsync(cmd, CancellationToken.None);
        Assert.True(result.IsSuccess);
        await repo.Received(1).AddAsync(Arg.Any<Game>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithNonExistingCategory_Fails()
    {
        var repo = Substitute.For<IRepository<Game, GameId>>();
        var validator = Substitute.For<ICategoryValidator>();
        validator.ExistsAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>()).Returns(false);
        validator.IsPublishedAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>()).Returns(false);
        var uow = Substitute.For<IUnitOfWork>();
        var handler = new CreateGameHandler(repo, validator, uow);
        var cmd = new CreateGameCommand("Quiz", Guid.NewGuid(), 5, 10, 1, "Linear", 30, "Standard", "LOSE_ALL", "KEEP_CURRENT_SCORE", "None", "Points", 500, 2, 10);
        var result = await handler.HandleAsync(cmd, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal("CategoryNotFound", result.Error.Code);
    }
}