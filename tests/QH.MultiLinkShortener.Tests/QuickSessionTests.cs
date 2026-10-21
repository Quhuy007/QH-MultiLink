using QH.MultiLinkShortener.Core;
using Xunit;

namespace QH.MultiLinkShortener.Tests;

public class QuickSessionTests
{
    [Fact]
    public async Task FullWorkflowUsesSavedPreferenceAndKeepsOriginalUrlPrivate()
    {
        int otherCalls = 0;
        var history = new History();
        var first = new Provider("first", false, (_, _) => { otherCalls++; return Task.FromResult(Link); });
        var preferred = new Provider("preferred", true, (_, _) => Task.FromResult(Link));
        var session = Session([first, preferred], history, new(DefaultProviderId: "preferred"));
        var stages = new List<QuickStage>();
        session.Changed += () => stages.Add(session.Stage);
        Assert.Equal(QuickStage.Collapsed, session.Stage);
        session.Open();
        await session.SubmitAsync("https://example.com/", "");
        Assert.Equal(0, otherCalls);
        Assert.Equal(QuickStage.Result, session.Stage);
        Assert.Equal("preferred", session.Result!.ProviderId);
        Assert.Null(Assert.Single(history.Rows).OriginalUrl);
        Assert.Contains(QuickStage.Loading, stages);
        session.Collapse();
        Assert.Null(session.Result);
        Assert.Equal(QuickStage.Collapsed, session.Stage);
    }

    [Fact]
    public async Task PasswordUsesOnlyCompatibleProvider()
    {
        var plain = new Provider("plain", false, (_, _) => throw new Exception("Must not receive password"));
        string? sent = null;
        var protectedService = new Provider("protected", true, (request, _) => { sent = request.Password; return Task.FromResult(Link); });
        var session = Session([plain, protectedService], new(), new(DefaultProviderId: "plain"));
        await session.SubmitAsync("https://example.com/", "Safe@123");
        Assert.Equal("Safe@123", sent);
        Assert.Equal("protected", session.Result!.ProviderId);
    }

    [Fact]
    public async Task MissingKeyDoesNotSilentlySendToAnotherProvider()
    {
        var missing = new Provider("missing", false, (_, _) => throw new Exception("No request")) { IsConfigured = false };
        var other = new Provider("other", false, (_, _) => throw new Exception("No failover"));
        var session = Session([missing, other], new(), new(DefaultProviderId: "missing"));
        await session.SubmitAsync("https://example.com/", null);
        Assert.Equal(QuickStage.Error, session.Stage);
        Assert.Contains("key", session.Message);
    }

    [Theory]
    [InlineData("not a URL", null)]
    [InlineData("https://example.com/", "short")]
    public async Task ValidationStaysInSmallErrorViewWithoutNetwork(string url, string? password)
    {
        var provider = new Provider("one", true, (_, _) => throw new Exception("No request"));
        var session = Session([provider], new(), new(DefaultProviderId: "one"));
        await session.SubmitAsync(url, password);
        Assert.Equal(QuickStage.Error, session.Stage);
        Assert.False(session.IsBusy);
        session.Open();
        Assert.Equal("", session.Message);
    }

    [Fact]
    public async Task RepeatedEnterMakesOneRequestAndCancellationFinishes()
    {
        int calls = 0;
        var provider = new Provider("one", false, async (_, ct) => { calls++; await Task.Delay(Timeout.Infinite, ct); return Link; });
        var session = Session([provider], new(), new(DefaultProviderId: "one"));
        var operation = session.SubmitAsync("https://example.com/", null);
        var repeated = session.SubmitAsync("https://other.example/", null);
        session.Collapse();
        Assert.Equal(QuickStage.Loading, session.Stage);
        Assert.Same(operation, repeated);
        session.Cancel();
        await operation;
        Assert.Equal(1, calls);
        Assert.False(session.IsBusy);
        Assert.Equal(ShorteningStatus.Cancelled, session.Result!.Status);
        Assert.Equal(QuickStage.Error, session.Stage);
    }

    [Fact]
    public async Task HistoryFailureKeepsUsableResultAndSettingsReloadBetweenRequests()
    {
        var history = new History { Fail = true };
        var options = new Settings(new(DefaultProviderId: "one"));
        var session = new QuickShorteningSession([
            new Provider("one", false, (_, _) => Task.FromResult(Link)),
            new Provider("two", false, (_, _) => Task.FromResult(Link))], new(), options, history);
        await session.SubmitAsync("https://example.com/", null);
        Assert.Equal(QuickStage.Result, session.Stage);
        Assert.NotEmpty(session.Message);
        options.Value = new(DefaultProviderId: "two");
        await session.SubmitAsync("https://example.com/", null);
        Assert.Equal("two", session.Result!.ProviderId);
    }

    [Theory]
    [InlineData(1920, 1040, 1)]
    [InlineData(1366, 728, 1)]
    [InlineData(2560, 1400, 1.5)]
    [InlineData(1920, 1040, 2)]
    public void BothWindowSizesStayOnRightWorkAreaAndDoNotOverlapTaskbar(int width, int height, double scale)
    {
        foreach (var expanded in new[] { true, false })
        {
            var bounds = EdgePlacement.AtRight(-width, 40, width, height, expanded, scale);
            Assert.Equal(0, bounds.X + bounds.Width);
            Assert.InRange(bounds.Y, 40, 40 + height - bounds.Height);
            Assert.Equal((int)Math.Round((expanded ? 281 : 44) * scale), bounds.Width);
        }
    }

    private static ProviderLink Link => new(new("https://spoo.me/test"), ProtectionStatus.None);
    private static QuickShorteningSession Session(IShortenerProvider[] providers, History history, AppSettings options) => new(providers, new(), new Settings(options), history);
    private sealed class Provider(string id, bool supportsPassword, Func<ShorteningRequest, CancellationToken, Task<ProviderLink>> send) : IShortenerProvider
    {
        public ProviderInfo Info { get; } = new(id, id, "", supportsPassword, new("https://example.com/"));
        public bool IsConfigured { get; init; } = true;
        public Task<ProviderLink> ShortenAsync(ShorteningRequest request, CancellationToken ct) => send(request, ct);
    }
    private sealed class Settings(AppSettings value) : ISettingsStore
    {
        public AppSettings Value { get; set; } = value;
        public Task<AppSettings> LoadAsync() => Task.FromResult(Value);
        public Task SaveAsync(AppSettings value) { Value = value; return Task.CompletedTask; }
    }
    private sealed class History : IHistoryStore
    {
        public List<ShorteningResult> Rows { get; } = [];
        public bool Fail { get; init; }
        public Task AddAsync(ShorteningResult row, CancellationToken ct = default)
        { if (Fail) throw new IOException(); Rows.Add(row); return Task.CompletedTask; }
        public Task<IReadOnlyList<ShorteningResult>> ReadAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ShorteningResult>>(Rows);
        public Task ClearAsync(CancellationToken ct = default) { Rows.Clear(); return Task.CompletedTask; }
    }
}
