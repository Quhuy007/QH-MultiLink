using Microsoft.Extensions.DependencyInjection;
using QH.MultiLinkShortener.Core;
using QH.MultiLinkShortener.Infrastructure;
using Windows.Storage;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using System.Text.Json;

if (args.Contains("--research")) { await ProviderResearch.RunAsync(); return; }

if (args.Contains("--qa-gif") || args.Contains("--qa-mp4"))
{
    var directory = Path.GetFullPath("artifacts/qa-appearance");
    var extension = args.Contains("--qa-gif") ? "gif" : "mp4";
    var store = new QH.MultiLinkShortener.App.BackgroundAssetStore(directory);
    var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath($"artifacts/qa-media/background.{extension}"));
    var name = await store.ImportAsync(file);
    await new LocalStorage(directory).SaveAsync(new AppSettings(BackgroundColor: "#DCE7F8", BackgroundMediaName: name));
    Console.WriteLine($"Isolated UI data prepared: {directory} ({extension})");
    return;
}

if (args.Contains("--media-fixtures"))
{
    var directory = Path.GetFullPath("artifacts/qa-media");
    Directory.CreateDirectory(directory);
    using (var stream = new MemoryStream())
    {
        using var writer = new BinaryWriter(stream);
        writer.Write("GIF89a"u8); writer.Write((ushort)1); writer.Write((ushort)1);
        writer.Write(new byte[] { 0x80, 0, 0, 24, 52, 110, 38, 90, 124 });
        foreach (var pixel in new byte[] { 0x44, 0x4C })
        {
            writer.Write(new byte[] { 0x21, 0xF9, 4, 0, 75, 0, 0, 0, 0x2C });
            writer.Write((ushort)0); writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
            writer.Write(new byte[] { 0, 2, 2, pixel, 0x01, 0 });
        }
        writer.Write((byte)0x3B);
        await File.WriteAllBytesAsync(Path.Combine(directory, "background.gif"), GifBackground.MakeLoop(stream.ToArray()));
    }
    var folder = await StorageFolder.GetFolderFromPathAsync(directory);
    var file = await folder.CreateFileAsync("background.mp4", CreationCollisionOption.ReplaceExisting);
    var composition = new MediaComposition();
    composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255, 24, 52, 110), TimeSpan.FromSeconds(2)));
    var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Wvga);
    profile.Video.Width = 320; profile.Video.Height = 180;
    var result = await composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, profile);
    Console.WriteLine($"Local fixtures: {directory} | {result}");
    return;
}

if (!args.Contains("--live"))
{
    Console.WriteLine("Pass --live to create public test links for https://example.com using real providers.");
    return;
}
var services = new ServiceCollection();
services.AddShortener(Path.Combine(Path.GetTempPath(), "QH-smoke"));
// Isolated anonymous tests: never read the user's stored API key or history.
services.AddSingleton<ISecretStore, EmptySecrets>();
using var sp = services.BuildServiceProvider();
var providers = sp.GetServices<IShortenerProvider>().Where(p => p.IsConfigured).ToArray();
var coordinator = sp.GetRequiredService<ShorteningCoordinator>();
if (args.Contains("--protected"))
{
    var password = "A@9" + Guid.NewGuid().ToString("N");
    var protectedLink = await providers.Single(p => p.Info.Id == "spoo.me")
        .ShortenAsync(new(new("https://example.com/"), password), default);
    Console.WriteLine($"Password API: {protectedLink.Protection} | {protectedLink.Url}");
    return;
}
int success = 0;
var completed = new List<ShorteningResult>();
await foreach (var result in coordinator.RunAsync(new(new("https://example.com/"), null), providers))
{
    if (result.Status is ShorteningStatus.Pending or ShorteningStatus.Running) continue;
    completed.Add(result);
    if (!args.Contains("--json")) Console.WriteLine($"{result.ProviderName}: {result.Status} | {result.ElapsedMs} ms | {result.ShortUrl ?? result.Message}");
    if (result.Status == ShorteningStatus.Success)
    {
        success++;
        Directory.CreateDirectory("artifacts/smoke");
        File.WriteAllBytes($"artifacts/smoke/{result.ProviderId}.png", LocalQr.CreatePng(result.ShortUrl!));
    }
}
Directory.CreateDirectory("artifacts/smoke");
var report = JsonSerializer.Serialize(new { MeasuredAt = DateTimeOffset.UtcNow, Target = "https://example.com/", Results = completed },
    new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
await File.WriteAllTextAsync("artifacts/smoke/results.json", report);
Console.WriteLine(args.Contains("--json") ? report : $"Live results: {success}/{providers.Length}. No target URL was visited.");
Environment.ExitCode = success == providers.Length ? 0 : 1;

internal sealed class EmptySecrets : ISecretStore
{
    public string? Read(string name) => null;
    public void Write(string name, string? value) => throw new NotSupportedException();
}
