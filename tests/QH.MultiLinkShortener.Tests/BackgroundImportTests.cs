using QH.MultiLinkShortener.App;
using QH.MultiLinkShortener.Core;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Xunit;

namespace QH.MultiLinkShortener.Tests;

public sealed class BackgroundImportTests
{
    [Fact]
    public async Task NativeGifImportCopiesAndLoopsWithoutModifyingOriginal()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QH-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var original = Path.Combine(directory, "original.gif");
            var bytes = PreferencesTests.MakeGif(25, 25);
            await File.WriteAllBytesAsync(original, bytes);
            var store = new BackgroundAssetStore(directory);
            var name = await store.ImportAsync(await StorageFile.GetFileFromPathAsync(original));
            Assert.True(BackgroundPolicy.IsOwnedMediaName(name));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(original));
            Assert.Equal(TimeSpan.FromMilliseconds(500), GifBackground.Inspect(await File.ReadAllBytesAsync(store.GetPath(name)!)).Duration);
            await store.ValidateAsync(name);
            store.Remove(name);
            Assert.False(File.Exists(store.GetPath(name)));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task NativeMp4ImportRejectsLongTimelineAndRetainsPreviousAsset()
    {
        var directory = Path.Combine(Path.GetTempPath(), "QH-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(directory);
            var store = new BackgroundAssetStore(directory);
            var valid = await RenderAsync(folder, "short.mp4", TimeSpan.FromSeconds(1));
            var current = await store.ImportAsync(valid);
            await store.ValidateAsync(current);
            var tooLong = await RenderAsync(folder, "long.mp4", TimeSpan.FromSeconds(16));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.ImportAsync(tooLong));
            Assert.True(File.Exists(store.GetPath(current)));
            Assert.Single(Directory.GetFiles(Path.Combine(directory, "Backgrounds")));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task<StorageFile> RenderAsync(StorageFolder folder, string name, TimeSpan duration)
    {
        var file = await folder.CreateFileAsync(name, CreationCollisionOption.FailIfExists);
        var composition = new MediaComposition();
        composition.Clips.Add(MediaClip.CreateFromColor(Windows.UI.Color.FromArgb(255, 24, 52, 110), duration));
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Wvga);
        profile.Video.Width = 320; profile.Video.Height = 180;
        profile.Video.FrameRate.Numerator = 10; profile.Video.FrameRate.Denominator = 1;
        var result = await composition.RenderToFileAsync(file, MediaTrimmingPreference.Precise, profile);
        Assert.Equal(Windows.Media.Transcoding.TranscodeFailureReason.None, result);
        return file;
    }
}
