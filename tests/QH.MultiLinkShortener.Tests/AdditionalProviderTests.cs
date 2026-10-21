using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using QH.MultiLinkShortener.Core;
using QH.MultiLinkShortener.Infrastructure;
using Xunit;

namespace QH.MultiLinkShortener.Tests;

public class AdditionalProviderTests
{
    private static readonly ShorteningRequest PublicRequest = new(new("https://example.com/path?a=1&b=two#fragment"), null);

    [Theory]
    [InlineData("da.gd", "s")]
    [InlineData("clck.ru", "--")]
    public async Task PublicAdaptersEncodeFullDestinationAndNormalizeText(string domain, string path)
    {
        var provider = new PublicHttpProvider(new Factory(domain, request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal($"https://{domain}/{path}?url=" + Uri.EscapeDataString(PublicRequest.Url.AbsoluteUri), request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            return Ok($"https://{domain}/abc\n");
        }), domain);
        Assert.Equal($"https://{domain}/abc", (await provider.ShortenAsync(PublicRequest, default)).Url.AbsoluteUri);
    }

    [Theory]
    [InlineData("<html>challenge</html>")]
    [InlineData("https://other.example/abc")]
    [InlineData("https://da.gd:8443/abc")]
    [InlineData("http://da.gd/abc")]
    public async Task PlainTextMustBeSecureLinkOnExpectedHost(string text)
    {
        var provider = new PublicHttpProvider(new Factory("da.gd", _ => Ok(text)), "da.gd");
        await Assert.ThrowsAsync<ProviderException>(() => provider.ShortenAsync(PublicRequest, default));
    }

    [Theory]
    [InlineData("tinyurl", "api.tinyurl.com", "create", "{\"data\":{\"tiny_url\":\"https://tinyurl.com/abc\"}}", "https://tinyurl.com/abc")]
    [InlineData("dub", "api.dub.co", "links", "{\"domain\":\"custom.example\",\"shortLink\":\"https://custom.example/abc\"}", "https://custom.example/abc")]
    [InlineData("shortio", "api.short.io", "links/public", "{\"secureShortURL\":\"https://custom.example/abc\",\"hasPassword\":true}", "https://custom.example/abc")]
    public async Task AccountAdaptersKeepTokenInHeaderAndParseOfficialResponse(string id, string host, string path, string reply, string expected)
    {
        var token = "test-token";
        var secret = id == "shortio" ? new ServiceCredential(token, "custom.example").Encode() : token;
        var factory = new Factory(host, request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"https://{host}/{path}", request.RequestUri!.AbsoluteUri);
            Assert.Equal(id == "shortio" ? token : "Bearer " + token, request.Headers.GetValues("Authorization").Single());
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.DoesNotContain(token, body);
            using var json = JsonDocument.Parse(body);
            Assert.Equal(PublicRequest.Url.AbsoluteUri, json.RootElement.GetProperty(id == "shortio" ? "originalURL" : "url").GetString());
            if (id == "shortio") Assert.Equal("custom.example", json.RootElement.GetProperty("domain").GetString());
            return Ok(reply);
        });
        var provider = new AccountProvider(factory, new Secrets(secret), id);
        Assert.True(provider.IsConfigured);
        var result = await provider.ShortenAsync(PublicRequest with { Password = id == "tinyurl" ? null : "Safe@123" }, default);
        Assert.Equal(expected, result.Url.AbsoluteUri);
        Assert.Equal(id == "tinyurl" ? ProtectionStatus.None : id == "shortio" ? ProtectionStatus.Confirmed : ProtectionStatus.Requested, result.Protection);
    }

    [Theory]
    [InlineData("tinyurl")]
    [InlineData("dub")]
    [InlineData("shortio")]
    public async Task MissingCredentialsNeverMakeNetworkRequest(string id)
    {
        var factory = new Factory("unused.example", _ => throw new InvalidOperationException("Must not send"));
        IShortenerProvider provider = new AccountProvider(factory, new Secrets(null), id);
        Assert.False(provider.IsConfigured);
        await Assert.ThrowsAsync<ProviderException>(() => provider.ShortenAsync(PublicRequest, default));
        Assert.Null(ProviderPreference.Choose([provider], id, false));
    }

    [Fact]
    public async Task ShortIoDoesNotReportSuccessWhenProtectionRejected()
    {
        var provider = new AccountProvider(new Factory("api.short.io", _ => Ok("{\"secureShortURL\":\"https://custom.example/a\",\"hasPassword\":false}")),
            new Secrets(new ServiceCredential("key", "custom.example").Encode()), "shortio");
        await Assert.ThrowsAsync<ProviderException>(() => provider.ShortenAsync(PublicRequest with { Password = "Safe@123" }, default));
    }

    [Theory]
    [InlineData("https://custom.example")]
    [InlineData("custom.example/abc")]
    [InlineData("custom.example:443")]
    [InlineData("127.0.0.1")]
    [InlineData("custom.example.")]
    public void ShortIoDomainCannotContainUrlComponents(string domain) => Assert.False(ServiceCredential.ValidDomain(domain));

    [Fact]
    public void CatalogRetiresBrokenServicesAndExposesThreeAnonymousOptions()
    {
        var services = new ServiceCollection().AddShortener(Path.Combine(Path.GetTempPath(), "unused-qh"));
        services.AddSingleton<ISecretStore>(new Secrets(null));
        using var sp = services.BuildServiceProvider();
        var providers = sp.GetServices<IShortenerProvider>().ToArray();
        Assert.Equal(6, providers.Length);
        Assert.DoesNotContain(providers, p => p.Info.Id is "is.gd" or "v.gd");
        Assert.Equal(new[] { "spoo.me", "da.gd", "clck.ru" }, providers.Where(p => p.IsConfigured).Select(p => p.Info.Id));
        Assert.Equal("spoo.me", ProviderPreference.Choose(providers, "is.gd", false)!.Info.Id);
    }

    [Theory]
    [InlineData("da.gd")]
    [InlineData("clck.ru")]
    public async Task PublicRateLimitHonorsCooldown(string domain)
    {
        int calls = 0;
        var provider = new PublicHttpProvider(new Factory(domain, _ => { calls++; return new(HttpStatusCode.TooManyRequests); }), domain);
        await Assert.ThrowsAsync<ProviderException>(() => provider.ShortenAsync(PublicRequest, default));
        await Assert.ThrowsAsync<ProviderException>(() => provider.ShortenAsync(PublicRequest, default));
        Assert.Equal(1, calls);
    }

    private static HttpResponseMessage Ok(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text) };
    private sealed class Secrets(string? value) : ISecretStore
    { public string? Read(string name) => value; public void Write(string name, string? value) => throw new NotSupportedException(); }
    private sealed class Factory(string host, Func<HttpRequestMessage, HttpResponseMessage> action) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(new Handler(action)) { BaseAddress = new("https://" + host + "/") }; }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> action) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(action(request)); }
}
