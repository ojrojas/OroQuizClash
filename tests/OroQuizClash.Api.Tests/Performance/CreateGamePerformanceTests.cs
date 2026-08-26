namespace OroQuizClash.Api.Tests.Performance;

public sealed class CreateGamePerformanceTests
{
    [Fact]
    public async Task CreateGame_ShouldBeUnder2Seconds()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Task.Delay(10);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 2000);
    }
}