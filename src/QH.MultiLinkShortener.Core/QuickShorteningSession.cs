namespace QH.MultiLinkShortener.Core;

public enum QuickStage { Collapsed, Input, Loading, Result, Error }

/// <summary>Independent quick-window workflow; shares providers and storage, never the main form's inputs.</summary>
public sealed class QuickShorteningSession(IEnumerable<IShortenerProvider> providers, ShorteningCoordinator coordinator,
    ISettingsStore settings, IHistoryStore history)
{
    private readonly IShortenerProvider[] _providers = providers.ToArray();
    private CancellationTokenSource? _cancellation;
    public QuickStage Stage { get; private set; } = QuickStage.Collapsed;
    public ShorteningResult? Result { get; private set; }
    public string Message { get; private set; } = "";
    public bool IsBusy => _cancellation is not null;
    public event Action? Changed;
    public event Action? HistoryChanged;
    public Task Completion { get; private set; } = Task.CompletedTask;

    public void Open()
    {
        if (IsBusy) return;
        Result = null; Message = ""; SetStage(QuickStage.Input);
    }
    public void Collapse()
    {
        if (IsBusy) return;
        Result = null; Message = ""; SetStage(QuickStage.Collapsed);
    }
    public void Cancel() => _cancellation?.Cancel();
    public Task SubmitAsync(string url, string? password)
    {
        if (IsBusy) return Completion;
        return Completion = ExecuteAsync(url, string.IsNullOrEmpty(password) ? null : password);
    }
    private async Task ExecuteAsync(string url, string? password)
    {
        Result = null; Message = "";
        if (!UrlValidation.TryParse(url, out var uri)) { Fail("Nhập URL http:// hoặc https:// hợp lệ."); return; }
        if (password is not null && !UrlValidation.IsValidPassword(password))
        { Fail("Mật khẩu cần 8 ký tự trở lên, gồm chữ, số và ký tự đặc biệt."); return; }
        _cancellation = new();
        SetStage(QuickStage.Loading);
        try
        {
            var options = await settings.LoadAsync();
            var preferred = _providers.FirstOrDefault(p => p.Info.Id == options.DefaultProviderId);
            if (preferred is not null && !preferred.IsConfigured)
            { Fail("Dịch vụ mặc định chưa có key. Mở Cài đặt để kết nối."); return; }
            var provider = ProviderPreference.Choose(_providers, options.DefaultProviderId, password is not null);
            if (provider is null) { Fail("Chưa có dịch vụ đã cấu hình phù hợp. Kiểm tra Cài đặt."); return; }
            await foreach (var result in coordinator.RunAsync(new(uri, password), [provider], 1,
                Math.Clamp(options.TimeoutSeconds, 5, 60), _cancellation.Token))
            {
                if (result.Status is ShorteningStatus.Pending or ShorteningStatus.Running) continue;
                Result = result;
                try
                {
                    await history.AddAsync(result with { OriginalUrl = options.SaveOriginalUrl ? uri.AbsoluteUri : null });
                    HistoryChanged?.Invoke();
                }
                catch { Message = "Đã nhận kết quả nhưng chưa lưu được lịch sử."; }
                if (result.Status == ShorteningStatus.Success) SetStage(QuickStage.Result);
                else Fail(result.Message ?? "Chưa tạo được link. Hãy thử lại.");
            }
        }
        catch { Fail("Không đọc được cài đặt hoặc kết nối dịch vụ. Hãy thử lại."); }
        finally { _cancellation.Dispose(); _cancellation = null; Changed?.Invoke(); }
    }
    public void ShowCopyError() { Message = "Chưa sao chép được. Chọn link và nhấn Ctrl+C."; Changed?.Invoke(); }
    private void Fail(string message) { Message = message; SetStage(QuickStage.Error); }
    private void SetStage(QuickStage stage) { Stage = stage; Changed?.Invoke(); }
}

public readonly record struct EdgeBounds(int X, int Y, int Width, int Height);
public static class EdgePlacement
{
    // Logical design size; scale for readable Windows DPI. Coordinates are physical work-area pixels.
    public static EdgeBounds AtRight(int left, int top, int workWidth, int workHeight,
        bool expanded, double scale = 1)
    {
        if (workWidth < 1 || workHeight < 1 || !double.IsFinite(scale) || scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        var width = Math.Min(workWidth, (int)Math.Round((expanded ? 281 : 44) * scale));
        var height = Math.Min(workHeight, (int)Math.Round((expanded ? 87 : 52) * scale));
        var y = Math.Clamp(top + (int)Math.Round(779 * scale), top, top + workHeight - height);
        return new(left + workWidth - width, y, width, height);
    }
}
