using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

internal static class ProviderResearch
{
    public static async Task RunAsync()
    {
        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, MaxConnectionsPerServer = 4 };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 65536 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("QH-MultiLink/0.2");
        var url = "https://example.com/";
        using var slots = new SemaphoreSlim(4);
        var targets = new[]
        {
            ("spoo.me", "https://spoo.me/api/v1/shorten", "json"),
            ("is.gd", "https://is.gd/create.php", "gd"),
            ("v.gd", "https://v.gd/create.php", "gd"),
            ("cleanuri.com", "https://cleanuri.com/api/v1/shorten", "form"),
            ("ulvis.net", "https://ulvis.net/api/write/post", "ulvis"),
            ("da.gd", "https://da.gd/s?url=" + Uri.EscapeDataString(url), "get"),
            ("clck.ru", "https://clck.ru/--?url=" + Uri.EscapeDataString(url), "get")
        };
        var results = await Task.WhenAll(targets.Select(async target =>
        {
            await slots.WaitAsync();
            var clock = Stopwatch.StartNew();
            try
            {
                using var request = new HttpRequestMessage(target.Item3 == "get" ? HttpMethod.Get : HttpMethod.Post, target.Item2);
                if (target.Item3 == "json") request.Content = JsonContent.Create(new { long_url = url });
                else if (target.Item3 != "get")
                {
                    var fields = new Dictionary<string, string> { ["url"] = url };
                    if (target.Item3 == "gd") fields["format"] = "json";
                    if (target.Item3 == "ulvis") { fields["type"] = "json"; fields["private"] = "1"; }
                    request.Content = new FormUrlEncodedContent(fields);
                }
                using var response = await client.SendAsync(request);
                var body = (await response.Content.ReadAsStringAsync()).Trim();
                var row = new ProbeResult(target.Item1, (int)response.StatusCode, clock.ElapsedMilliseconds,
                    body.Length > 300 ? body[..300] : body, response.Headers.Location?.ToString());
                Console.WriteLine(JsonSerializer.Serialize(row));
                return row;
            }
            catch (Exception error)
            {
                var row = new ProbeResult(target.Item1, 0, clock.ElapsedMilliseconds, error.GetType().Name, null);
                Console.WriteLine(JsonSerializer.Serialize(row)); return row;
            }
            finally { slots.Release(); }
        }));
        Directory.CreateDirectory("artifacts/research");
        await File.WriteAllTextAsync("artifacts/research/providers.json", JsonSerializer.Serialize(new
        { MeasuredAt = DateTimeOffset.UtcNow, Target = url, Results = results }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private sealed record ProbeResult(string Provider, int HttpStatus, long ElapsedMs, string Body, string? Redirect);
}
