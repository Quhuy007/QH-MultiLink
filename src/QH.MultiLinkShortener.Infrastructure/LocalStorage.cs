using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using QH.MultiLinkShortener.Core;
using QRCoder;

namespace QH.MultiLinkShortener.Infrastructure;

public sealed class LocalStorage(string directory) : IHistoryStore, ISettingsStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = Path.Combine(directory, "history.db") }.ToString());
        try
        {
            await connection.OpenAsync(ct);
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS history (id TEXT PRIMARY KEY, created TEXT NOT NULL, payload TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS settings (id INTEGER PRIMARY KEY CHECK(id=1), payload TEXT NOT NULL);
                """;
            await command.ExecuteNonQueryAsync(ct);
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task AddAsync(ShorteningResult result, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO history VALUES ($id, $created, $payload)";
            command.Parameters.AddWithValue("$id", result.Id);
            command.Parameters.AddWithValue("$created", result.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(result));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<ShorteningResult>> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT payload FROM history ORDER BY created DESC LIMIT 500";
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<ShorteningResult>();
            while (await reader.ReadAsync(cancellationToken))
                if (JsonSerializer.Deserialize<ShorteningResult>(reader.GetString(0)) is { } item) results.Add(item);
            return results;
        }
        finally { _gate.Release(); }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            using var command = connection.CreateCommand();
            // Overwrite deleted SQLite pages; do not leave old URLs in free pages.
            command.CommandText = "PRAGMA secure_delete=ON; DELETE FROM history; VACUUM;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task<AppSettings> LoadAsync()
    {
        await using var connection = await OpenAsync(CancellationToken.None);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM settings WHERE id=1";
        var data = await command.ExecuteScalarAsync();
        return data is string json ? JsonSerializer.Deserialize<AppSettings>(json) ?? new() : new();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await _gate.WaitAsync();
        try
        {
            await using var connection = await OpenAsync(CancellationToken.None);
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO settings VALUES (1, $payload)";
            command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(settings));
            await command.ExecuteNonQueryAsync();
        }
        finally { _gate.Release(); }
    }
}

public sealed class DpapiSecretStore(string directory) : ISecretStore
{
    private string GetPath(string name) => name is "spoo" or "tinyurl" or "dub" or "shortio" ? Path.Combine(directory, name + ".secret")
        : throw new ArgumentException("Unknown secret name.", nameof(name));
    public string? Read(string name)
    {
        var path = GetPath(name);
        return !File.Exists(path) ? null : Encoding.UTF8.GetString(ProtectedData.Unprotect(
            File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser));
    }
    public void Write(string name, string? value)
    {
        var path = GetPath(name);
        if (string.IsNullOrWhiteSpace(value)) { File.Delete(path); return; }
        Directory.CreateDirectory(directory);
        var bytes = Encoding.UTF8.GetBytes(value);
        try { File.WriteAllBytes(path, ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}

public static class LocalQr
{
    public static byte[] CreatePng(string url)
    {
        if (!UrlValidation.TryParse(url, out _)) throw new ArgumentException("Invalid URL.");
        using var data = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(10);
    }
}
