using System.Text.Json;

namespace QH.MultiLinkShortener.Core;

public sealed record ServiceCredential(string ApiKey, string Domain)
{
    public static bool ValidKey(string? value) => value?.Length is > 0 and <= 4096
        && value.All(c => c is >= '!' and <= '~');

    public static bool ValidDomain(string? value) => value is { Length: <= 253 } && value.Contains('.')
        && Uri.CheckHostName(value) == UriHostNameType.Dns
        && !value.EndsWith('.') && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-');

    public static ServiceCredential? Decode(string? value)
    {
        try
        {
            var credential = value is null ? null : JsonSerializer.Deserialize<ServiceCredential>(value);
            return credential is not null && ValidKey(credential.ApiKey) && ValidDomain(credential.Domain) ? credential : null;
        }
        catch (JsonException) { return null; }
        catch (ArgumentNullException) { return null; }
    }
    public string Encode() => JsonSerializer.Serialize(this);
}
