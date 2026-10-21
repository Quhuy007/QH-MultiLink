using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using QH.MultiLinkShortener.Core;

namespace QH.MultiLinkShortener.Infrastructure;

/// <summary>Documented public HTTP endpoints return plain text, normalized to ProviderLink by this adapter.</summary>
public sealed class PublicHttpProvider(IHttpClientFactory clients, string domain) : IShortenerProvider
{
    private DateTimeOffset _cooldown;
    public ProviderInfo Info { get; } = new(domain, domain, "Dùng ngay · không cần API key", false,
        new(domain == "da.gd" ? "https://da.gd/help" : "https://clck.ru/"));

    public async Task<ProviderLink> ShortenAsync(ShorteningRequest request, CancellationToken cancellationToken)
    {
        if (request.Password is not null) throw new ProviderException("Dịch vụ này không hỗ trợ mật khẩu.");
        if (domain == "clck.ru" && request.Url.AbsoluteUri.Length > 2000)
            throw new ProviderException("clck.ru chỉ nhận URL tối đa 2.000 ký tự.");
        if (_cooldown > DateTimeOffset.UtcNow) throw new ProviderException("Dịch vụ đang giới hạn tốc độ. Hãy chờ rồi thử lại.");
        using var client = clients.CreateClient(domain);
        using var response = await client.GetAsync((domain == "da.gd" ? "s?url=" : "--?url=")
            + Uri.EscapeDataString(request.Url.AbsoluteUri), cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            _cooldown = ProviderCooldown.Until(response);
        ProviderResponse.EnsureSuccess(response);
        var text = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        if (!UrlValidation.TryParse(text, out var link) || link.Scheme != "https"
            || !link.IsDefaultPort || link.Host != domain)
            throw new ProviderException("Dịch vụ trả về link không hợp lệ.");
        return new(link, ProtectionStatus.None);
    }
}

public sealed class AccountProvider(IHttpClientFactory clients, ISecretStore secrets, string id) : IShortenerProvider
{
    private DateTimeOffset _cooldown;
    public ProviderInfo Info { get; } = id switch
    {
        "tinyurl" => new(id, "TinyURL", "Cần API token · domain tinyurl.com", false,
            new("https://tinyurl.com/app/dev"), id, true),
        "dub" => new(id, "Dub", "Cần API key · hỗ trợ mật khẩu", true,
            new("https://dub.co/docs/api-reference/links/create"), id, true),
        "shortio" => new(id, "Short.io", "Cần public API key và domain", true,
            new("https://developers.short.io/reference/post_links-public"), id, true, true),
        _ => throw new ArgumentException("Unknown provider.", nameof(id))
    };

    public bool IsConfigured
    {
        get
        {
            try { return id == "shortio" ? ServiceCredential.Decode(secrets.Read(id)) is not null
                : ServiceCredential.ValidKey(secrets.Read(id) ?? ""); }
            catch { return false; }
        }
    }

    public async Task<ProviderLink> ShortenAsync(ShorteningRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new ProviderException("Thêm API key và cấu hình dịch vụ trong Cài đặt trước khi tạo link.");
        if (request.Password is not null && !Info.SupportsPassword)
            throw new ProviderException("Dịch vụ này không hỗ trợ mật khẩu trong ứng dụng.");
        if (_cooldown > DateTimeOffset.UtcNow) throw new ProviderException("Dịch vụ đang giới hạn tốc độ. Hãy chờ rồi thử lại.");
        var stored = secrets.Read(id)!;
        var credential = id == "shortio" ? ServiceCredential.Decode(stored)! : new ServiceCredential(stored, "");
        using var client = clients.CreateClient(id);
        using var message = new HttpRequestMessage(HttpMethod.Post, id switch { "tinyurl" => "create", "dub" => "links", _ => "links/public" });
        if (id == "shortio") message.Headers.Add("Authorization", credential.ApiKey);
        else message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.ApiKey);
        var payload = new Dictionary<string, object?>();
        payload[id == "shortio" ? "originalURL" : "url"] = request.Url.AbsoluteUri;
        if (id == "tinyurl") payload["domain"] = "tinyurl.com";
        if (id == "shortio") payload["domain"] = credential.Domain;
        if (request.Password is not null) payload["password"] = request.Password;
        message.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests) _cooldown = ProviderCooldown.Until(response);
        using var json = await ProviderResponse.ReadAsync(response, cancellationToken);
        var root = json.RootElement;
        var protection = request.Password is null ? ProtectionStatus.None : ProtectionStatus.Requested;
        Uri link;
        if (id == "tinyurl")
        {
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != System.Text.Json.JsonValueKind.Object)
                throw new ProviderException("Dịch vụ trả về dữ liệu không hợp lệ.");
            link = ProviderResponse.Link(data, "tiny_url", "tinyurl.com");
        }
        else if (id == "dub")
        {
            if (!root.TryGetProperty("domain", out var domain) || domain.ValueKind != System.Text.Json.JsonValueKind.String
                || !ServiceCredential.ValidDomain(domain.GetString()!)) throw new ProviderException("Dịch vụ trả về domain không hợp lệ.");
            link = ProviderResponse.Link(root, "shortLink", domain.GetString()!);
        }
        else
        {
            link = ProviderResponse.Link(root, "secureShortURL", credential.Domain);
            if (request.Password is not null && root.TryGetProperty("hasPassword", out var hasPassword))
            {
                if (hasPassword.ValueKind != System.Text.Json.JsonValueKind.True)
                    throw new ProviderException("Dịch vụ chưa xác nhận link được bảo vệ. Không sử dụng link này cho dữ liệu riêng tư.");
                protection = ProtectionStatus.Confirmed;
            }
        }
        return new(link, protection);
    }
}

internal static class ProviderCooldown
{
    public static DateTimeOffset Until(HttpResponseMessage response)
    {
        var retry = response.Headers.RetryAfter;
        var until = retry?.Date ?? DateTimeOffset.UtcNow.Add(retry?.Delta ?? TimeSpan.FromMinutes(1));
        return until > DateTimeOffset.UtcNow ? until : DateTimeOffset.UtcNow.AddMinutes(1);
    }
}
