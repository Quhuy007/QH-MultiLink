using System.Text.Json;
using QH.MultiLinkShortener.Core;
using QH.MultiLinkShortener.Infrastructure;
using Xunit;

namespace QH.MultiLinkShortener.Tests;

public class PreferencesTests
{
    [Fact]
    public void PreferenceUsesExactlyOneChosenServiceAndRespectsPasswordCapability()
    {
        IShortenerProvider[] providers = [new Stub("is.gd", false), new Stub("v.gd", false), new Stub("spoo.me", true)];
        Assert.Equal("v.gd", ProviderPreference.Choose(providers, "v.gd", false)!.Info.Id);
        Assert.Equal("spoo.me", ProviderPreference.Choose(providers, "v.gd", true)!.Info.Id);
        Assert.Equal("is.gd", ProviderPreference.Choose(providers, "removed-service", false)!.Info.Id);
        Assert.Null(ProviderPreference.Choose(providers.Take(2), "is.gd", true));
    }

    [Fact]
    public void OldSettingsMigrateWithSafeDefaults()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"SaveOriginalUrl":false,"Concurrency":4,"TimeoutSeconds":20}""")!;
        Assert.Equal("spoo.me", settings.DefaultProviderId);
        Assert.Equal("#F5F7FB", settings.BackgroundColor);
        Assert.Null(settings.BackgroundMediaName);
        Assert.True(settings.BackgroundMotionEnabled);
        Assert.True(settings.QuickWindowEnabled);
        Assert.False(settings.DarkThemeEnabled);
    }

    [Fact]
    public async Task DisabledQuickWindowSurvivesRestartAndOtherSettingsUpdates()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QH-quick-preference-" + Guid.NewGuid());
        try
        {
            var store = new LocalStorage(directory);
            await store.SaveAsync(new AppSettings(QuickWindowEnabled: false, DarkThemeEnabled: true));
            var restored = await new LocalStorage(directory).LoadAsync();
            Assert.False(restored.QuickWindowEnabled);
            Assert.True(restored.DarkThemeEnabled);
            await store.SaveAsync(restored with { BackgroundColor = "#123456" });
            Assert.False((await store.LoadAsync()).QuickWindowEnabled);
            await store.SaveAsync((await store.LoadAsync()) with { QuickWindowEnabled = true });
            Assert.True((await new LocalStorage(directory).LoadAsync()).QuickWindowEnabled);
            Assert.True((await new LocalStorage(directory).LoadAsync()).DarkThemeEnabled);
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(15, true)]
    [InlineData(15.001, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void ValidatesExactDurationBoundary(double seconds, bool valid)
    {
        if (valid) BackgroundPolicy.ValidateDuration(TimeSpan.FromSeconds(seconds));
        else Assert.Throws<InvalidDataException>(() => BackgroundPolicy.ValidateDuration(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void ColorAndAssetNamesCannotBecomeUntrustedPaths()
    {
        Assert.Equal("#ABCDEF", BackgroundPolicy.NormalizeColor("#abcdef"));
        Assert.Equal("#F5F7FB", BackgroundPolicy.NormalizeColor("transparent"));
        Assert.False(BackgroundPolicy.IsOwnedMediaName("../../secret.mp4"));
        Assert.False(BackgroundPolicy.IsOwnedMediaName("https://example.com/background.mp4"));
        Assert.True(BackgroundPolicy.IsOwnedMediaName(Guid.NewGuid().ToString("N") + ".gif"));
    }

    [Fact]
    public async Task PersistsDefaultProviderAndAppearanceAcrossInstances()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QH-preferences-" + Guid.NewGuid());
        try
        {
            var store = new LocalStorage(directory);
            var settings = new AppSettings(DefaultProviderId: "v.gd", BackgroundColor: "#112233",
                BackgroundMediaName: Guid.NewGuid().ToString("N") + ".mp4", BackgroundMotionEnabled: false);
            await store.SaveAsync(settings);
            Assert.Equal(settings, await new LocalStorage(directory).LoadAsync());
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }

    [Fact]
    public void GifChecksFullTimelineInsteadOfFirstFrameAndIgnoresLoopCount()
    {
        var gif = MakeGif(750, 750);
        Assert.Equal(TimeSpan.FromSeconds(15), GifBackground.Inspect(gif).Duration);
        Assert.Throws<InvalidDataException>(() => GifBackground.Inspect(MakeGif(750, 751)));
        var loop = GifBackground.MakeLoop(MakeGif(10, 20));
        Assert.Contains("NETSCAPE2.0", System.Text.Encoding.ASCII.GetString(loop));
        Assert.Equal(TimeSpan.FromMilliseconds(300), GifBackground.Inspect(loop).Duration);
    }

    [Fact]
    public void GifRejectsTruncationNonGifAndOversizedResolution()
    {
        var valid = MakeGif(10, 10);
        Assert.Throws<InvalidDataException>(() => GifBackground.Inspect(valid[..^1]));
        Assert.Throws<InvalidDataException>(() => GifBackground.Inspect("not a GIF"u8.ToArray()));
        valid[6] = 0xFF; valid[7] = 0xFF;
        Assert.Throws<InvalidDataException>(() => GifBackground.Inspect(valid));
    }

    [Fact]
    public void GifNormalizationReplacesAnExistingFiniteLoop()
    {
        var original = MakeGif(10, 20);
        var once = GifBackground.MakeLoop(original);
        var appIndex = Find(once, "NETSCAPE2.0"u8.ToArray());
        once[appIndex + 13] = 7; // finite loop count low byte
        var normalized = GifBackground.MakeLoop(once);
        var index = Find(normalized, "NETSCAPE2.0"u8.ToArray());
        Assert.Equal(0, normalized[index + 13]);
        Assert.Equal(normalized.Length, once.Length);
        Assert.Equal(GifBackground.Inspect(original), GifBackground.Inspect(normalized));
    }

    internal static byte[] MakeGif(params ushort[] delays)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("GIF89a"u8); writer.Write((ushort)1); writer.Write((ushort)1);
        writer.Write(new byte[] { 0x80, 0, 0, 0, 0, 0, 255, 255, 255 });
        foreach (var delay in delays)
        {
            writer.Write(new byte[] { 0x21, 0xF9, 4, 0 }); writer.Write(delay); writer.Write(new byte[] { 0, 0, 0x2C });
            writer.Write((ushort)0); writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
            writer.Write(new byte[] { 0, 2, 2, 0x44, 0x01, 0 });
        }
        writer.Write((byte)0x3B); return stream.ToArray();
    }
    private static int Find(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle);
    private sealed class Stub(string id, bool password) : IShortenerProvider
    {
        public ProviderInfo Info { get; } = new(id, id, "", password, new("https://example.com"));
        public Task<ProviderLink> ShortenAsync(ShorteningRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
