using QuizArena.Admin.Client.Models.GameConfiguration;

namespace QuizArena.Admin.Tests;

public sealed class GameConfigurationTests
{
    private static GameConfigurationForm ValidForm() => new(
        Name: "Quiz Noche Estrellada",
        Description: "Trivia de astronomía",
        CategoryId: Guid.NewGuid(),
        NumberOfRounds: 7,
        MaxPlayers: 50,
        TimePerQuestion: 30,
        InitialDifficulty: 3,
        DifficultyProgression: DifficultyStrategy.Adaptive,
        Scoring: ScoringSystem.ProgressiveBonus,
        PointsPerRound: 100,
        SecuredPoints: SecuredPointsPolicy.KeepCheckpoint,
        WithdrawalPolicy: WithdrawalPolicy.KeepSecuredScore,
        FinishPolicy: LossPolicy.FallbackToCheckpoint,
        FinalRewardId: null,
        ConsolationRewardId: null,
        ScheduledAt: DateTimeOffset.UtcNow.AddMinutes(10));

    [Fact]
    public void Validate_NameTooShort_Fails()
    {
        var form = ValidForm() with { Name = "ab" };
        Assert.True(form.Validate().ContainsKey(nameof(GameConfigurationForm.Name)));
    }

    [Fact]
    public void Validate_RoundsBelow5_Fails()
    {
        var form = ValidForm() with { NumberOfRounds = 4 };
        Assert.True(form.Validate().ContainsKey(nameof(GameConfigurationForm.NumberOfRounds)));
    }

    [Fact]
    public void Validate_RoundsAbove10_Fails()
    {
        var form = ValidForm() with { NumberOfRounds = 11 };
        Assert.True(form.Validate().ContainsKey(nameof(GameConfigurationForm.NumberOfRounds)));
    }

    [Fact]
    public void Validate_MaxPlayersBelow2_Fails()
    {
        var form = ValidForm() with { MaxPlayers = 1 };
        Assert.True(form.Validate().ContainsKey(nameof(GameConfigurationForm.MaxPlayers)));
    }

    [Fact]
    public void Validate_TimePerQuestionOutOfRange_Fails()
    {
        var form = ValidForm() with { TimePerQuestion = 4 };
        Assert.True(form.Validate().ContainsKey(nameof(GameConfigurationForm.TimePerQuestion)));
        var form2 = ValidForm() with { TimePerQuestion = 301 };
        Assert.True(form2.Validate().ContainsKey(nameof(GameConfigurationForm.TimePerQuestion)));
    }

    [Fact]
    public void Validate_ScheduledAtPast_Fails()
    {
        var form = ValidForm() with { ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        Assert.True(form.Validate().ContainsKey(nameof(GameConfigurationForm.ScheduledAt)));
    }

    [Fact]
    public void Validate_ScheduledAtLessThan5Min_Fails()
    {
        var form = ValidForm() with { ScheduledAt = DateTimeOffset.UtcNow.AddMinutes(4) };
        Assert.True(form.Validate().ContainsKey(nameof(GameConfigurationForm.ScheduledAt)));
    }

    [Fact]
    public void Validate_SameRewards_Fails()
    {
        var g = Guid.NewGuid();
        var form = ValidForm() with { FinalRewardId = g, ConsolationRewardId = g };
        Assert.True(form.Validate().ContainsKey(nameof(GameConfigurationForm.ConsolationRewardId)));
    }

    [Fact]
    public void Validate_ValidForm_NoErrors() => Assert.Empty(ValidForm().Validate());

    [Fact]
    public void IsImmutable_AfterReadyOrRunning()
    {
        Assert.True(GameStateViewMap.IsImmutable(GameStateView.Ready));
        Assert.True(GameStateViewMap.IsImmutable(GameStateView.Running));
        Assert.True(GameStateViewMap.IsImmutable(GameStateView.Paused));
        Assert.False(GameStateViewMap.IsImmutable(GameStateView.Draft));
        Assert.False(GameStateViewMap.IsImmutable(GameStateView.Configured));
    }
}
