using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using QH.MultiLinkShortener.Core;

namespace QH.MultiLinkShortener.Infrastructure;

public sealed class GdProvider(IHttpClientFactory clients, string domain) : IShortenerProvider
{
    private DateTimeOffset _cooldown;
    public ProviderInfo Info { get; } = new(domain, domain,
        domain == "is.gd" ? "Link ngắn gọn, không cần tài khoản" : "Có trang xem trước trước khi chuyển hướng",
        false, new($"https://{domain}/apishorteningreference.php"));

    public async Task<ProviderLink> ShortenAsync(ShorteningRequest request, CancellationToken cancellationToken)
    {
        if (request.Password is not null) throw new ProviderException("Dịch vụ này không hỗ trợ mật khẩu.");
        if (_cooldown > DateTimeOffset.UtcNow) throw new ProviderException("Dịch vụ đang giới hạn tốc độ. Hãy chờ ít nhất một phút.");
        using var client = clients.CreateClient(domain);
        using var response = await client.PostAsync("create.php", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["format"] = "json", ["url"] = request.Url.AbsoluteUri }), cancellationToken);
        if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway)
        {
            _cooldown = DateTimeOffset.UtcNow.AddMinutes(1);
            throw new ProviderException("Đã đạt giới hạn dịch vụ. Hãy chờ ít nhất một phút.");
        }
        using var json = await ProviderResponse.ReadAsync(response, cancellationToken);
        var root = json.RootElement;
        if (root.TryGetProperty("errorcode", out var code))
        {
            if (code.GetInt32() == 3) _cooldown = DateTimeOffset.UtcNow.AddMinutes(1);
            throw new ProviderException(code.GetInt32() switch
            {
                1 => "Dịch vụ từ chối URL này. Hãy kiểm tra địa chỉ.",
                3 => "Đã đạt giới hạn dịch vụ. Hãy chờ ít nhất một phút.",
                _ => "Dịch vụ chưa thể tạo link. Hãy thử lại sau."
            });
        }
        return new(ProviderResponse.Link(root, "shorturl", domain), ProtectionStatus.None);
    }
}

public sealed class SpooProvider(IHttpClientFactory clients, ISecretStore secrets) : IShortenerProvider
{
    private DateTimeOffset _cooldown;
    public ProviderInfo Info { get; } = new("spoo.me", "spoo.me", "Hỗ trợ mật khẩu · API key tùy chọn", true,
        new("https://spoo.me/docs/api-reference/url-shortening/create-shortened-url"), "spoo");

    public async Task<ProviderLink> ShortenAsync(ShorteningRequest request, CancellationToken cancellationToken)
    {
        if (_cooldown > DateTimeOffset.UtcNow)
            throw new ProviderException("Dịch vụ đang giới hạn tốc độ. Hãy chờ rồi thử lại.");
        if (request.Password is not null && !UrlValidation.IsValidPassword(request.Password))
            throw new ProviderException("Mật khẩu cần ít nhất 8 ký tự, gồm chữ, số và ký tự đặc biệt.");
        using var client = clients.CreateClient(Info.Id);
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/v1/shorten");
        var key = secrets.Read("spoo");
        if (!string.IsNullOrWhiteSpace(key)) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        message.Content = JsonContent.Create(new { long_url = request.Url.AbsoluteUri, password = request.Password });
        using var response = await client.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retry = response.Headers.RetryAfter;
            _cooldown = retry?.Date ?? DateTimeOffset.UtcNow.Add(retry?.Delta ?? TimeSpan.FromMinutes(1));
            if (_cooldown <= DateTimeOffset.UtcNow) _cooldown = DateTimeOffset.UtcNow.AddMinutes(1);
        }
        using var json = await ProviderResponse.ReadAsync(response, cancellationToken);
        // v1 does not echo protection status; never claim independently confirmed protection.
        return new(ProviderResponse.Link(json.RootElement, "short_url", "spoo.me"),
            request.Password is null ? ProtectionStatus.None : ProtectionStatus.Requested);
    }
}

internal static class ProviderResponse
{
    public static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        EnsureSuccess(response);
        try { return JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct)); }
        catch (JsonException) { throw new ProviderException("Phản hồi dịch vụ không hợp lệ. Hãy thử lại sau."); }
    }

    public static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new ProviderException(response.StatusCode switch
            {
                HttpStatusCode.TooManyRequests => "Đã đạt giới hạn dịch vụ. Hãy chờ rồi thử lại.",
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Kiểm tra API key và quyền tạo link trong Cài đặt.",
                HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => "Dịch vụ từ chối URL hoặc mật khẩu. Kiểm tra dữ liệu đã nhập.",
                _ => "Dịch vụ tạm thời không sẵn sàng. Hãy thử lại sau."
            });
    }

    public static Uri Link(JsonElement root, string field, string host)
    {
        if (!root.TryGetProperty(field, out var item) || item.ValueKind != JsonValueKind.String || !UrlValidation.TryParse(item.GetString(), out var uri)
            || uri.Scheme != "https" || !uri.IsDefaultPort || !uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase))
            throw new ProviderException("Dịch vụ trả về link không hợp lệ.");
        return uri;
    }
}
