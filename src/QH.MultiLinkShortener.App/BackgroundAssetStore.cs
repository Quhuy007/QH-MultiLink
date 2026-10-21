using QH.MultiLinkShortener.Core;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;

namespace QH.MultiLinkShortener.App;

public sealed class BackgroundAssetStore(string dataDirectory)
{
    private readonly string _directory = Path.Combine(dataDirectory, "Backgrounds");
    public string? GetPath(string? name) => BackgroundPolicy.IsOwnedMediaName(name) ? Path.Combine(_directory, name!) : null;

    public async Task<string> ImportAsync(StorageFile source)
    {
        var extension = Path.GetExtension(source.Name).ToLowerInvariant();
        if (extension is not (".gif" or ".mp4")) throw new InvalidDataException("Chọn tệp GIF hoặc MP4.");
        Directory.CreateDirectory(_directory);
        var name = Guid.NewGuid().ToString("N") + extension;
        var destination = GetPath(name)!;
        try
        {
            // Copy once into owned storage and validate that exact copy. The original stays untouched.
            await using (var input = new FileStream(source.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true))
            {
                if (input.Length == 0 || input.Length > BackgroundPolicy.MaxBytes)
                    throw new InvalidDataException("Nền động phải có dữ liệu và không được lớn hơn 20 MB.");
                await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
                await input.CopyToAsync(output);
            }
            if (extension == ".gif")
            {
                var bytes = await File.ReadAllBytesAsync(destination);
                var loop = GifBackground.MakeLoop(bytes);
                if (loop.LongLength > BackgroundPolicy.MaxBytes) throw new InvalidDataException("GIF sau khi chuẩn hóa vượt quá 20 MB.");
                await File.WriteAllBytesAsync(destination, loop);
            }
            await ValidateAsync(name);
            return name;
        }
        catch { Remove(name); throw; }
    }

    public async Task ValidateAsync(string name)
    {
        var path = GetPath(name) ?? throw new InvalidDataException("Không tìm thấy nền động đã lưu.");
        var info = new FileInfo(path);
        if (!info.Exists || info.Length == 0 || info.Length > BackgroundPolicy.MaxBytes)
            throw new InvalidDataException("Nền động không tồn tại hoặc lớn hơn 20 MB.");
        var file = await StorageFile.GetFileFromPathAsync(path);
        if (name.EndsWith(".gif"))
        {
            var parsed = await Task.Run(() => GifBackground.Inspect(File.ReadAllBytes(path)));
            using var stream = await file.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.GifDecoderId, stream);
            if (decoder.FrameCount != parsed.Frames) throw new InvalidDataException("Không đọc được đầy đủ khung GIF.");
            await decoder.GetFrameAsync(decoder.FrameCount - 1);
        }
        else
        {
            var clip = await MediaClip.CreateFromFileAsync(file).AsTask().WaitAsync(TimeSpan.FromSeconds(15));
            BackgroundPolicy.ValidateDuration(clip.OriginalDuration);
            var encoding = clip.GetVideoEncodingProperties();
            BackgroundPolicy.ValidateDimensions(encoding.Width, encoding.Height);
        }
    }

    public void Remove(string? name)
    {
        if (GetPath(name) is not { } path) return;
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
