using System.Collections.Concurrent;
using QH.MultiLinkShortener.Core;
using Xunit;

namespace QH.MultiLinkShortener.Tests;

public class CoordinatorTests
{
    private static readonly ShorteningRequest Request = new(new("https://example.com/path?a=1&b=2"), null);
    [Theory]
    [InlineData("https://example.com/a?x=1&y=2", true)]
    [InlineData("http://example.com/#fragment", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/secret", false)]
    [InlineData("https://user:pass@example.com", false)]
    [InlineData("example.com", false)]
    [InlineData("", false)]
    public void ValidatesUrl(string value, bool expected) => Assert.Equal(expected, UrlValidation.TryParse(value, out _));

    [Fact]
    public async Task StreamsFastProviderBeforeSlowOneAndIsolatesFailure()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = new Stub("slow", async (_, ct) => { await release.Task.WaitAsync(ct); return Link; });
        var fast = new Stub("fast", (_, _) => Task.FromResult(Link));
        var failed = new Stub("failed", (_, _) => throw new Exception("secret-token-and-url"));
        var events = new List<ShorteningResult>();
        await foreach (var item in new ShorteningCoordinator().RunAsync(Request, [slow, fast, failed], 3))
        {
            events.Add(item);
            if (item.ProviderId == "fast" && item.Status == ShorteningStatus.Success)
            {
                Assert.DoesNotContain(events, e => e.ProviderId == "slow" && e.Status == ShorteningStatus.Success);
                release.SetResult();
            }
        }
        Assert.Equal(2, events.Count(e => e.Status == ShorteningStatus.Success));
        Assert.DoesNotContain("secret-token", Assert.Single(events, e => e.Status == ShorteningStatus.Failed).Message);
    }

    [Fact]
    public async Task LimitsConcurrencyAndEmitsCompleteStateTransitions()
    {
        int active = 0, peak = 0;
        var gate = new object();
        var providers = Enumerable.Range(0, 8).Select(i => (IShortenerProvider)new Stub($"p{i}", async (_, ct) =>
        {
            lock (gate) { active++; peak = Math.Max(peak, active); }
            await Task.Delay(25, ct);
            lock (gate) active--;
            return Link;
        })).ToArray();
        var events = new List<ShorteningResult>();
        await foreach (var item in new ShorteningCoordinator().RunAsync(Request, providers, 2)) events.Add(item);
        Assert.Equal(2, peak);
        foreach (var group in events.GroupBy(e => e.ProviderId))
            Assert.Equal(new[] { ShorteningStatus.Pending, ShorteningStatus.Running, ShorteningStatus.Success }, group.Select(e => e.Status));
    }

    [Fact]
    public async Task CancellationFinishesRunningAndQueuedProviders()
    {
        using var cts = new CancellationTokenSource();
        var provider = new Stub("a", async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Link; });
        var queued = new Stub("b", (_, _) => throw new Exception("Must not execute queued provider"));
        var events = new List<ShorteningResult>();
        await foreach (var item in new ShorteningCoordinator().RunAsync(Request, [provider, queued], 1, cancellationToken: cts.Token))
        { events.Add(item); if (item.Status == ShorteningStatus.Running) cts.Cancel(); }
        Assert.Equal(2, events.Count(e => e.Status == ShorteningStatus.Cancelled));
        Assert.DoesNotContain(events, e => e.Status == ShorteningStatus.Failed);
    }

    [Fact]
    public async Task PasswordNeverReachesUnsupportedProvider()
    {
        bool called = false;
        var provider = new Stub("unsupported", (_, _) => { called = true; return Task.FromResult(Link); });
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in new ShorteningCoordinator().RunAsync(Request with { Password = "Safe@123" }, [provider])) { }
        });
        Assert.False(called);
    }

    [Fact]
    public async Task TimeoutDoesNotCancelOtherProviders()
    {
        var slow = new Stub("slow", async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Link; });
        var fast = new Stub("fast", (_, _) => Task.FromResult(Link));
        var events = new List<ShorteningResult>();
        await foreach (var item in new ShorteningCoordinator().RunAsync(Request, [slow, fast], timeoutSeconds: 1)) events.Add(item);
        Assert.Single(events, e => e.Status == ShorteningStatus.Success);
        Assert.Single(events, e => e.Status == ShorteningStatus.Failed);
    }
    internal static ProviderLink Link => new(new("https://is.gd/test"), ProtectionStatus.None);
    internal sealed class Stub(string id, Func<ShorteningRequest, CancellationToken, Task<ProviderLink>> action) : IShortenerProvider
    {
        public ProviderInfo Info { get; } = new(id, id, "", false, new("https://example.com"));
        public Task<ProviderLink> ShortenAsync(ShorteningRequest request, CancellationToken cancellationToken) => action(request, cancellationToken);
    }
}
