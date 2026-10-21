using System.Text;

namespace QH.MultiLinkShortener.Core;

public static class ProviderPreference
{
    public static IShortenerProvider? Choose(IEnumerable<IShortenerProvider> providers, string? preferredId, bool requirePassword)
    {
        var eligible = providers.Where(p => p.IsConfigured && (!requirePassword || p.Info.SupportsPassword)).ToArray();
        return eligible.FirstOrDefault(p => p.Info.Id == preferredId) ?? eligible.FirstOrDefault();
    }
}

public static class BackgroundPolicy
{
    public const int MaxSeconds = 15;
    public const long MaxBytes = 20 * 1024 * 1024;
    public const int MaxFrames = 450;
    public static void ValidateDuration(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromSeconds(MaxSeconds))
            throw new InvalidDataException("Nền động phải có thời lượng lớn hơn 0 và không quá 15 giây.");
    }
    public static void ValidateDimensions(uint width, uint height)
    {
        if (width == 0 || height == 0 || width > 1920 || height > 1920 || (ulong)width * height > 1920UL * 1080)
            throw new InvalidDataException("Nền động chỉ hỗ trợ độ phân giải tối đa Full HD (1920 × 1080 hoặc dọc tương đương).");
    }
    public static string NormalizeColor(string? value) => value?.Length == 7 && value[0] == '#'
        && value.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0 ? value.ToUpperInvariant() : "#F5F7FB";
    public static bool IsOwnedMediaName(string? name) => name is not null && name.Length == 36
        && Guid.TryParseExact(name[..32], "N", out _) && (name.EndsWith(".gif") || name.EndsWith(".mp4"));
}

public sealed record GifInfo(TimeSpan Duration, int Frames, uint Width, uint Height);

/// <summary>Checks a GIF's complete timeline and writes an infinite loop without touching its image data.</summary>
public static class GifBackground
{
    public static GifInfo Inspect(byte[] bytes) => Analyze(bytes).Info;

    public static byte[] MakeLoop(byte[] bytes)
    {
        var parsed = Analyze(bytes);
        using var output = new MemoryStream();
        output.Write(bytes.AsSpan(0, parsed.InsertAt));
        output.Write(new byte[] { 0x21, 0xFF, 11 });
        output.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
        output.Write(new byte[] { 3, 1, 0, 0, 0 });
        var position = parsed.InsertAt;
        foreach (var (start, end) in parsed.Loops)
        {
            output.Write(bytes.AsSpan(position, start - position)); position = end;
        }
        output.Write(bytes.AsSpan(position));
        return output.ToArray();
    }

    private static (GifInfo Info, int InsertAt, List<(int Start, int End)> Loops) Analyze(byte[] bytes)
    {
        if (bytes.Length > BackgroundPolicy.MaxBytes) throw new InvalidDataException("Nền động không được lớn hơn 20 MB.");
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(stream);
            var signature = Encoding.ASCII.GetString(reader.ReadBytes(6));
            if (signature is not ("GIF87a" or "GIF89a")) throw new InvalidDataException("Tệp không phải GIF hợp lệ.");
            var width = reader.ReadUInt16(); var height = reader.ReadUInt16();
            BackgroundPolicy.ValidateDimensions(width, height);
            var packed = reader.ReadByte(); Skip(2);
            if ((packed & 0x80) != 0) Skip(3 * (1 << ((packed & 7) + 1)));
            var insertAt = (int)stream.Position;
            var loops = new List<(int, int)>();
            int frames = 0, delay = 10;
            long centiseconds = 0;
            while (true)
            {
                var start = (int)stream.Position;
                var marker = reader.ReadByte();
                if (marker == 0x3B)
                {
                    if (frames == 0 || stream.Position != stream.Length) throw new InvalidDataException("GIF bị thiếu hoặc có dữ liệu thừa.");
                    var duration = TimeSpan.FromMilliseconds(centiseconds * 10);
                    BackgroundPolicy.ValidateDuration(duration);
                    return (new(duration, frames, width, height), insertAt, loops);
                }
                if (marker == 0x21)
                {
                    var label = reader.ReadByte();
                    if (label == 0xF9)
                    {
                        if (reader.ReadByte() != 4) throw new InvalidDataException("GIF bị hỏng.");
                        reader.ReadByte(); delay = reader.ReadUInt16(); reader.ReadByte();
                        if (reader.ReadByte() != 0) throw new InvalidDataException("GIF bị hỏng.");
                    }
                    else if (label == 0xFF)
                    {
                        var count = reader.ReadByte();
                        var app = Encoding.ASCII.GetString(reader.ReadBytes(count));
                        if (count != 11) throw new InvalidDataException("GIF bị hỏng.");
                        SkipBlocks();
                        if (app is "NETSCAPE2.0" or "ANIMEXTS1.0") loops.Add((start, (int)stream.Position));
                    }
                    else if (label is 0xFE or 0x01) SkipBlocks();
                    else throw new InvalidDataException("GIF chứa phần mở rộng không được hỗ trợ.");
                }
                else if (marker == 0x2C)
                {
                    var x = reader.ReadUInt16(); var y = reader.ReadUInt16();
                    var w = reader.ReadUInt16(); var h = reader.ReadUInt16();
                    if (w == 0 || h == 0 || x + w > width || y + h > height) throw new InvalidDataException("Kích thước khung GIF không hợp lệ.");
                    var flags = reader.ReadByte();
                    if ((flags & 0x80) != 0) Skip(3 * (1 << ((flags & 7) + 1)));
                    if (reader.ReadByte() is < 2 or > 8) throw new InvalidDataException("GIF bị hỏng.");
                    SkipBlocks();
                    frames++;
                    // Windows displays very short/unspecified frame delays at about 100 ms.
                    centiseconds += delay <= 1 ? 10 : delay;
                    delay = 10;
                    if (frames > BackgroundPolicy.MaxFrames) throw new InvalidDataException("GIF chỉ được chứa tối đa 450 khung hình.");
                    BackgroundPolicy.ValidateDuration(TimeSpan.FromMilliseconds(centiseconds * 10));
                }
                else throw new InvalidDataException("GIF bị hỏng.");
            }
            void Skip(int count)
            {
                if (stream.Length - stream.Position < count) throw new EndOfStreamException();
                stream.Position += count;
            }
            void SkipBlocks() { int count; while ((count = reader.ReadByte()) != 0) Skip(count); }
        }
        catch (EndOfStreamException) { throw new InvalidDataException("Tệp GIF chưa hoàn chỉnh."); }
    }
}
