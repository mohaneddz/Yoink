using Yoink.Infrastructure.Ytdlp;

namespace Yoink.Tests;

public class PreparationRetryTests
{
    [Theory]
    [InlineData("Error reading response: curl: (56) Connection closed abruptly", true)]
    [InlineData("curl: (28) Operation timed out", true)]
    [InlineData("Remote end closed connection without response", true)]
    [InlineData("HTTP Error 403: browser verification; curl: (56)", false)]
    [InlineData("Requested format is not available", false)]
    [InlineData("No space left on device", false)]
    public void Only_transient_transport_errors_retry(string error, bool expected) =>
        Assert.Equal(expected, PreparationRetry.IsTransient(error));

    [Fact]
    public async Task Transient_failures_stop_after_three_attempts()
    {
        var attempts = 0;
        var delays = new List<TimeSpan>();
        var notices = new List<int>();
        var result = await PreparationRetry.RunAsync(() => Task.FromResult(++attempts), _ => true,
            CancellationToken.None, notices.Add, (delay, _) => { delays.Add(delay); return Task.CompletedTask; });
        Assert.Equal(3, result);
        Assert.Equal(new[] { 2, 3 }, notices);
        Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, delays);
    }

    [Fact]
    public async Task Success_or_permanent_failure_returns_immediately()
    {
        var attempts = 0;
        await PreparationRetry.RunAsync(() => Task.FromResult(++attempts), _ => false, CancellationToken.None);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Cancellation_during_backoff_prevents_another_process()
    {
        using var cancellation = new CancellationTokenSource();
        var attempts = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PreparationRetry.RunAsync(
            () => Task.FromResult(++attempts), _ => true, cancellation.Token,
            _ => cancellation.Cancel()));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Retry_can_recover_without_using_all_attempts()
    {
        var attempts = 0;
        var result = await PreparationRetry.RunAsync(() => Task.FromResult(++attempts), value => value < 2,
            CancellationToken.None, delay: (_, _) => Task.CompletedTask);
        Assert.Equal(2, result);
    }
}
