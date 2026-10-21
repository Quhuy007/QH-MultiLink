using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using QH.MultiLinkShortener.Core;
using QH.MultiLinkShortener.Infrastructure;
using Xunit;

namespace QH.MultiLinkShortener.Tests;

public class InfrastructureTests
{
    [Theory]
    [InlineData("is.gd")]
    [InlineData("v.gd")]
    [InlineData("spoo.me")]
    public async Task RateLimitPreventsImmediateRepeatRequests(string domain)
    {
        int calls = 0;
        var handler = new Handler(_ => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)); });
        IShortenerProvider provider = domain == "spoo.me"
            ? new SpooProvider(new Factory(handler, domain), new SecretStub())
            : new GdProvider(new Factory(handler, domain), domain);
        var request = new ShorteningRequest(new("https://example.com"), null);
        await Assert.ThrowsAsync<ProviderException>(() => provider.ShortenAsync(request, default));
        await Assert.ThrowsAsync<ProviderException>(() => provider.ShortenAsync(request, default));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("is.gd")]
    [InlineData("v.gd")]
    public async Task GdEncodesUrlAndRejectsUnexpectedHosts(string domain)
    {
        var target = new Uri("https://example.com/path?a=1&b=two#part");
        var handler = new Handler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"https://{domain}/create.php", request.RequestUri!.AbsoluteUri);
            var body = await request.Content!.ReadAsStringAsync();
            Assert.Contains(Uri.EscapeDataString(target.AbsoluteUri), body);
            return Json("{\"shorturl\":\"https://unexpected.example/steal\"}");
        });
        var provider = new GdProvider(new Factory(handler, domain), domain);
        await Assert.ThrowsAsync<ProviderException>(() => provider.ShortenAsync(new(target, null), default));
    }

    [Fact]
    public async Task SpooUsesJsonAndBearerWithoutClaimingVerifiedProtection()
    {
        var handler = new Handler(async request =>
        {
            Assert.Equal("https://spoo.me/api/v1/shorten", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer spoo_test", request.Headers.Authorization!.ToString());
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("Safe@123", json.RootElement.GetProperty("password").GetString());
            Assert.Equal("https://example.com/", json.RootElement.GetProperty("long_url").GetString());
            return Json("{\"short_url\":\"https://spoo.me/test\"}");
        });
        var provider = new SpooProvider(new Factory(handler, "spoo.me"), new SecretStub());
        var result = await provider.ShortenAsync(new(new("https://example.com"), "Safe@123"), default);
        Assert.Equal(ProtectionStatus.Requested, result.Protection);
    }

    [Fact]
    public async Task DoesNotEchoSecretBearingErrorResponses()
    {
        var handler = new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        { Content = new StringContent("secret-api-key https://private-url password=secret") }));
        var provider = new SpooProvider(new Factory(handler, "spoo.me"), new SecretStub());
        var error = await Assert.ThrowsAsync<ProviderException>(() => provider.ShortenAsync(new(new("https://example.com"), null), default));
        Assert.DoesNotContain("secret", error.Message);
    }

    [Fact]
    public async Task SqlitePersistsHistoryAndSettingsAndClearsOnlyHistory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QH-tests-" + Guid.NewGuid());
        try
        {
            var store = new LocalStorage(directory);
            var row = new ShorteningResult("id", "is.gd", "is.gd", ShorteningStatus.Success, DateTimeOffset.UtcNow, "https://is.gd/test");
            await store.AddAsync(row);
            var settings = new AppSettings(false, 3, 15);
            await store.SaveAsync(settings);
            var reload = new LocalStorage(directory);
            Assert.Equal(row, Assert.Single(await reload.ReadAsync()));
            Assert.Null(Assert.Single(await reload.ReadAsync()).OriginalUrl);
            Assert.Equal(settings, await reload.LoadAsync());
            await reload.ClearAsync();
            Assert.Empty(await reload.ReadAsync());
            Assert.Equal(settings, await reload.LoadAsync());
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }

    [Fact]
    public void SecretsAreEncryptedAndDeletable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QH-tests-" + Guid.NewGuid());
        try
        {
            var store = new DpapiSecretStore(directory);
            store.Write("spoo", "spoo_private_test_key");
            Assert.Equal("spoo_private_test_key", store.Read("spoo"));
            Assert.DoesNotContain("spoo_private_test_key", System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory, "spoo.secret"))));
            store.Write("spoo", null); Assert.Null(store.Read("spoo"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task RegisteredPipelineNeverRetriesCreationPost()
    {
        int calls = 0;
        var services = new ServiceCollection();
        services.AddShortener(Path.Combine(Path.GetTempPath(), "unused-qh"));
        services.AddHttpClient("spoo.me").ConfigurePrimaryHttpMessageHandler(() => new Handler(_ =>
        { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); }));
        using var sp = services.BuildServiceProvider();
        var provider = sp.GetServices<IShortenerProvider>().Single(x => x.Info.Id == "spoo.me");
        await Assert.ThrowsAsync<ProviderException>(() => provider.ShortenAsync(new(new("https://example.com"), null), default));
        Assert.Equal(1, calls);
    }

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value) };
    private sealed class SecretStub : ISecretStore { public string? Read(string name) => "spoo_test"; public void Write(string name, string? value) { } }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> action) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => action(request); }
    private sealed class Factory(HttpMessageHandler handler, string domain) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(handler, false) { BaseAddress = new($"https://{domain}/") }; }
}
