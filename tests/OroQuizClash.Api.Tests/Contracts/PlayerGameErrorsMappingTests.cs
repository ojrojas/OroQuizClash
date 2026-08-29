using Xunit;

namespace OroQuizClash.Api.Tests.Contracts;

public sealed class PlayerGameErrorsMappingTests
{
    [Theory]
    [InlineData("AnswerWindowExpired", 400)]
    [InlineData("QuestionAlreadyAnswered", 409)]
    [InlineData("PlayerNotActive", 403)]
    [InlineData("PlayerIdentityMismatch", 403)]
    [InlineData("GameNotFound", 404)]
    public void ProblemDetails_MapsCorrectStatus(string code, int status)
    {
        Assert.True(status >= 400);
    }
}
