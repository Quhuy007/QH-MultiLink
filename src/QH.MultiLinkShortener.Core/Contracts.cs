namespace QH.MultiLinkShortener.Core;

public sealed record ProviderInfo(string Id, string Name, string Description, bool SupportsPassword, Uri Documentation,
    string? SecretName = null, bool RequiresKey = false, bool RequiresDomain = false);
public sealed record ShorteningRequest(Uri Url, string? Password);
public enum ProtectionStatus { None, Requested, Confirmed }
public enum ShorteningStatus { Pending, Running, Success, Failed, Cancelled }
public sealed record ProviderLink(Uri Url, ProtectionStatus Protection);
public sealed record ShorteningResult(string Id, string ProviderId, string ProviderName,
    ShorteningStatus Status, DateTimeOffset CreatedAt, string? ShortUrl = null,
    ProtectionStatus Protection = ProtectionStatus.None, string? Message = null, long ElapsedMs = 0,
    string? OriginalUrl = null);

public interface IShortenerProvider
{
    ProviderInfo Info { get; }
    bool IsConfigured => true;
    Task<ProviderLink> ShortenAsync(ShorteningRequest request, CancellationToken cancellationToken);
}

// Only application-owned, sanitized messages may cross this boundary.
public sealed class ProviderException(string message) : Exception(message);

public interface IHistoryStore
{
    Task AddAsync(ShorteningResult result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShorteningResult>> ReadAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed record AppSettings(bool SaveOriginalUrl = false, int Concurrency = 4, int TimeoutSeconds = 20,
    string DefaultProviderId = "spoo.me", string BackgroundColor = "#F5F7FB",
    string? BackgroundMediaName = null, bool BackgroundMotionEnabled = true, bool QuickWindowEnabled = true,
    bool DarkThemeEnabled = false);
public interface ISettingsStore
{
    Task<AppSettings> LoadAsync();
    Task SaveAsync(AppSettings settings);
}
public interface ISecretStore
{
    string? Read(string name);
    void Write(string name, string? value);
}

public static class UrlValidation
{
    public static bool TryParse(string? value, out Uri uri)
    {
        uri = null!;
        return value?.Length <= 8192 && Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri!)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
    }

    public static bool IsValidPassword(string password) => password.Length >= 8
        && password.Any(char.IsLetter) && password.Any(char.IsDigit)
        && password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));
}
