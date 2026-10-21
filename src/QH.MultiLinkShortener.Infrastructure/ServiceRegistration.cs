using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using QH.MultiLinkShortener.Core;

namespace QH.MultiLinkShortener.Infrastructure;

public static class ServiceRegistration
{
    public static IServiceCollection AddShortener(this IServiceCollection services, string dataDirectory)
    {
        services.AddSingleton<ShorteningCoordinator>();
        services.AddSingleton(new LocalStorage(dataDirectory));
        services.AddSingleton<IHistoryStore>(sp => sp.GetRequiredService<LocalStorage>());
        services.AddSingleton<ISettingsStore>(sp => sp.GetRequiredService<LocalStorage>());
        services.AddSingleton<ISecretStore>(new DpapiSecretStore(dataDirectory));
        foreach (var domain in new[] { "spoo.me", "da.gd", "clck.ru", "tinyurl", "dub", "shortio" })
        {
            services.AddHttpClient(domain, client =>
            {
                client.BaseAddress = new Uri(domain switch
                { "tinyurl" => "https://api.tinyurl.com/", "dub" => "https://api.dub.co/", "shortio" => "https://api.short.io/", _ => $"https://{domain}/" });
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.MaxResponseContentBufferSize = 64 * 1024;
                client.DefaultRequestHeaders.Accept.ParseAdd(domain is "da.gd" or "clck.ru" ? "text/plain" : "application/json");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("QH-MultiLink/0.1");
            }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false, UseCookies = false, MaxConnectionsPerServer = 4,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            }).AddStandardResilienceHandler(options =>
            {
                // Creating links has side effects, even on APIs which allow GET.
                options.Retry.DisableFor(HttpMethod.Get, HttpMethod.Post);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
                options.CircuitBreaker.MinimumThroughput = 3;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            });
        }
        services.AddSingleton<IShortenerProvider, SpooProvider>();
        foreach (var domain in new[] { "da.gd", "clck.ru" })
            services.AddSingleton<IShortenerProvider>(sp => new PublicHttpProvider(sp.GetRequiredService<IHttpClientFactory>(), domain));
        foreach (var id in new[] { "tinyurl", "dub", "shortio" })
            services.AddSingleton<IShortenerProvider>(sp => new AccountProvider(sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<ISecretStore>(), id));
        return services;
    }
}
