using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using QH.MultiLinkShortener.Core;

namespace QH.MultiLinkShortener.App;

public partial class ProviderChoice(IShortenerProvider provider) : ObservableObject
{
    public IShortenerProvider Provider { get; } = provider;
    public string Name => Provider.Info.Name;
    public string Description => Provider.Info.Description;
    public bool IsConfigured => Provider.IsConfigured;
    public string Capability => !IsConfigured ? "Cần cấu hình API key" : Provider.Info.SupportsPassword ? "Hỗ trợ mật khẩu" : "Sẵn sàng · link công khai";
    public string Display => $"{Name} · {Capability}";
    public void Refresh() { OnPropertyChanged(nameof(IsConfigured)); OnPropertyChanged(nameof(Capability)); OnPropertyChanged(nameof(Display)); }
    [ObservableProperty] public partial bool IsSelected { get; set; } = true;
    [ObservableProperty] public partial Visibility Visibility { get; set; } = Visibility.Visible;
}

public partial class ResultRow : ObservableObject
{
    [ObservableProperty] public partial ShorteningResult Result { get; set; }
    public ResultRow(ShorteningResult result) { Result = result; }
    public string ProviderName => Result.ProviderName;
    public string Link => Result.ShortUrl ?? "";
    public bool CanUse => Result.Status == ShorteningStatus.Success;
    public string StatusLabel => Result.Status switch
    {
        ShorteningStatus.Pending => "Đang chờ", ShorteningStatus.Running => "Đang tạo…",
        ShorteningStatus.Success => "Thành công", ShorteningStatus.Cancelled => "Đã hủy", _ => "Chưa thành công"
    };
    public string Detail => Result.Message ?? (CanUse ? $"{Result.ElapsedMs:N0} ms · {ProtectionLabel}" : "Đang xử lý yêu cầu của bạn");
    public string ProtectionLabel => Result.Protection switch
    { ProtectionStatus.Requested => "Đã gửi mật khẩu theo API", ProtectionStatus.Confirmed => "Đã xác nhận bảo vệ", _ => "Link công khai" };
    public string Time => Result.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy · HH:mm");
    partial void OnResultChanged(ShorteningResult value)
    { foreach (var name in new[] { nameof(ProviderName), nameof(Link), nameof(CanUse), nameof(StatusLabel), nameof(Detail), nameof(Time) }) OnPropertyChanged(name); }
}

public partial class MainViewModel : ObservableObject
{
    private readonly ShorteningCoordinator _coordinator;
    private readonly IHistoryStore _history;
    private readonly ISettingsStore _settings;
    private readonly ConnectionDrafts _connectionDrafts;
    private bool _loadingConnection;
    private CancellationTokenSource? _cancellation;
    public ObservableCollection<ProviderChoice> Providers { get; }
    public ObservableCollection<ProviderChoice> Connections { get; }
    public ObservableCollection<ResultRow> Results { get; } = [];
    public ObservableCollection<ResultRow> History { get; } = [];

    [ObservableProperty] public partial string Url { get; set; } = "";
    [ObservableProperty] public partial bool RequirePassword { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool IsReady { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ThemeToggleLabel))]
    public partial bool DarkThemeEnabled { get; set; }
    [ObservableProperty] public partial bool IsThemeSaving { get; set; }
    public string ThemeToggleLabel => DarkThemeEnabled ? "Giao diện sáng" : "Giao diện tối";
    partial void OnIsThemeSavingChanged(bool value) { OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanStart)); }
    [ObservableProperty] public partial bool QuickWindowEnabled { get; set; } = true;
    [ObservableProperty] public partial bool IsQuickWindowSaving { get; set; }
    public event Action<bool>? QuickWindowPreferenceChanged;
    partial void OnIsQuickWindowSavingChanged(bool value) { OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanStart)); }
    [ObservableProperty] public partial string Notice { get; set; } = "";
    [ObservableProperty] public partial bool HasNotice { get; set; }
    [ObservableProperty] public partial bool SaveOriginalUrl { get; set; }
    [ObservableProperty] public partial double Concurrency { get; set; } = 4;
    [ObservableProperty] public partial double TimeoutSeconds { get; set; } = 20;
    [ObservableProperty] public partial string ApiKeyStatus { get; set; } = "Chưa lưu API key · có thể dùng ẩn danh";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DomainVisibility)), NotifyPropertyChangedFor(nameof(ConnectionDocumentation))]
    public partial int ConnectionIndex { get; set; }
    [ObservableProperty] public partial string ConnectionDomain { get; set; } = "";
    [ObservableProperty] public partial string ConnectionKey { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ConnectionErrorVisibility))]
    public partial string ConnectionError { get; set; } = "";
    public Visibility ConnectionErrorVisibility => ConnectionError.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    public Visibility DomainVisibility => SelectedConnection?.Provider.Info.RequiresDomain == true ? Visibility.Visible : Visibility.Collapsed;
    public Uri ConnectionDocumentation => SelectedConnection?.Provider.Info.Documentation ?? new("https://spoo.me/docs/api-keys");
    private ProviderChoice? SelectedConnection => Connections.ElementAtOrDefault(ConnectionIndex);
    partial void OnConnectionIndexChanged(int value) => LoadConnection();
    partial void OnConnectionKeyChanged(string value) => RememberConnectionDraft();
    partial void OnConnectionDomainChanged(string value) => RememberConnectionDraft();
    private void RememberConnectionDraft()
    {
        if (_loadingConnection || SelectedConnection is null) return;
        _connectionDrafts.Set(SelectedConnection.Provider.Info.SecretName!, new(ConnectionKey, ConnectionDomain));
        ConnectionError = "";
        UpdateKeyStatus();
    }
    private void LoadConnection()
    {
        if (SelectedConnection is null) return;
        _loadingConnection = true;
        try
        {
            var draft = _connectionDrafts.Get(SelectedConnection.Provider.Info.SecretName!);
            ConnectionKey = draft.Key;
            ConnectionDomain = draft.Domain;
            ConnectionError = "";
            UpdateKeyStatus();
        }
        finally { _loadingConnection = false; }
    }
    [ObservableProperty] public partial string ProgressText { get; set; } = "Sẵn sàng tạo link";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PreferredProviderDisplay))]
    public partial int DefaultProviderIndex { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MultiProviderVisibility)), NotifyPropertyChangedFor(nameof(PreferredProviderDisplay))]
    public partial bool UseMultipleProviders { get; set; }
    [ObservableProperty] public partial string BackgroundColor { get; set; } = "#F5F7FB";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(BackgroundMediaVisibility)), NotifyPropertyChangedFor(nameof(BackgroundLabel))]
    public partial string? BackgroundMediaName { get; set; }
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MotionButtonLabel)), NotifyPropertyChangedFor(nameof(MotionTooltip))]
    public partial bool BackgroundMotionEnabled { get; set; } = true;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(MotionButtonLabel)), NotifyPropertyChangedFor(nameof(MotionTooltip)), NotifyPropertyChangedFor(nameof(CanControlBackground))]
    public partial bool SystemMotionSuppressed { get; set; }
    public Visibility MultiProviderVisibility => UseMultipleProviders ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BackgroundMediaVisibility => BackgroundMediaName is null ? Visibility.Collapsed : Visibility.Visible;
    public string BackgroundLabel => BackgroundMediaName is null ? "Chưa chọn nền động" : $"Nền {Path.GetExtension(BackgroundMediaName).TrimStart('.').ToUpperInvariant()} · tối đa 15 giây";
    public bool CanControlBackground => !SystemMotionSuppressed;
    public string MotionButtonLabel => SystemMotionSuppressed ? "Nền tĩnh" : BackgroundMotionEnabled ? "Dừng nền" : "Phát nền";
    public string MotionTooltip => SystemMotionSuppressed ? "Windows đang tắt chuyển động hoặc bật tương phản cao. Nền tĩnh được sử dụng." : MotionButtonLabel;
    public string PreferredProviderDisplay => UseMultipleProviders ? "Nhiều dịch vụ" : $"Dịch vụ: {PreferredProvider?.Info.Name ?? "Chưa có"}";
    private IShortenerProvider? PreferredProvider => Providers.ElementAtOrDefault(DefaultProviderIndex)?.IsConfigured != true ? null : ProviderPreference.Choose(Providers.Select(p => p.Provider),
        Providers.ElementAtOrDefault(DefaultProviderIndex)?.Provider.Info.Id, RequirePassword);
    [ObservableProperty, NotifyPropertyChangedFor(nameof(UrlErrorVisibility))]
    public partial string UrlError { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PasswordErrorVisibility))]
    public partial string PasswordError { get; set; } = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ProviderErrorVisibility))]
    public partial string ProviderError { get; set; } = "";
    public Visibility UrlErrorVisibility => UrlError.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    public Visibility PasswordErrorVisibility => PasswordError.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ProviderErrorVisibility => ProviderError.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    public string Password { private get; set; } = "";
    public bool CanEdit => IsReady && !IsBusy && !IsQuickWindowSaving && !IsThemeSaving;
    public bool CanStart => CanEdit;
    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PasswordVisibility => RequirePassword ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyVisibility => Results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HistoryEmptyVisibility => History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public string ResultSummary => $"{Results.Count(x => x.CanUse)} / {Results.Count} thành công";
    public string HistorySummary => $"{History.Count} kết quả gần nhất · lưu trên máy của bạn";

    public MainViewModel(IEnumerable<IShortenerProvider> providers, ShorteningCoordinator coordinator,
        IHistoryStore history, ISettingsStore settings, ISecretStore secrets)
    {
        _coordinator = coordinator; _history = history; _settings = settings;
        Providers = new(providers.Select(x => new ProviderChoice(x)));
        Connections = new(Providers.Where(x => x.Provider.Info.SecretName is not null));
        _connectionDrafts = new(secrets, Connections.Select(x => x.Provider.Info));
    }

    public async Task InitializeAsync()
    {
        try
        {
            var options = await _settings.LoadAsync();
            SaveOriginalUrl = options.SaveOriginalUrl;
            Concurrency = Math.Clamp(options.Concurrency, 1, 6);
            TimeoutSeconds = Math.Clamp(options.TimeoutSeconds, 5, 60);
            DefaultProviderIndex = Math.Max(0, Providers.ToList().FindIndex(p => p.Provider.Info.Id == options.DefaultProviderId));
            if (options.DefaultProviderId is "is.gd" or "v.gd")
            {
                await _settings.SaveAsync(options with { DefaultProviderId = "spoo.me" });
                ShowNotice("is.gd và v.gd đã được tạm gỡ khỏi danh sách. Dịch vụ mặc định hiện là spoo.me; lịch sử cũ vẫn được giữ.");
            }
            BackgroundColor = BackgroundPolicy.NormalizeColor(options.BackgroundColor);
            BackgroundMediaName = BackgroundPolicy.IsOwnedMediaName(options.BackgroundMediaName) ? options.BackgroundMediaName : null;
            BackgroundMotionEnabled = options.BackgroundMotionEnabled;
            DarkThemeEnabled = options.DarkThemeEnabled;
            QuickWindowEnabled = options.QuickWindowEnabled;
            await RefreshHistoryAsync();
            LoadConnection();
        }
        catch { ShowNotice("Không đọc được dữ liệu cục bộ. Kiểm tra quyền truy cập thư mục dữ liệu."); }
        finally { IsReady = true; }
    }

    partial void OnRequirePasswordChanged(bool value)
    {
        foreach (var item in Providers)
            item.Visibility = !value || item.Provider.Info.SupportsPassword ? Visibility.Visible : Visibility.Collapsed;
        OnPropertyChanged(nameof(PasswordVisibility));
        OnPropertyChanged(nameof(PreferredProviderDisplay));
        if (!value) Password = "";
    }
    partial void OnIsReadyChanged(bool value) { OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanStart)); }
    partial void OnIsBusyChanged(bool value) { OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanStart)); OnPropertyChanged(nameof(BusyVisibility)); }
    public void ShowNotice(string message) { Notice = message; HasNotice = true; }

    [RelayCommand]
    private async Task ShortenAsync()
    {
        if (!CanStart) return;
        HasNotice = false;
        UrlError = PasswordError = ProviderError = "";
        if (!UrlValidation.TryParse(Url, out var uri))
        { UrlError = "Nhập URL đầy đủ bắt đầu bằng https:// hoặc http://, không chứa thông tin đăng nhập."; return; }
        if (RequirePassword && !UrlValidation.IsValidPassword(Password))
        { PasswordError = "Mật khẩu cần ít nhất 8 ký tự, gồm chữ, số và ký tự đặc biệt."; return; }
        var selected = UseMultipleProviders
            ? Providers.Where(p => p.IsSelected && p.IsConfigured && (!RequirePassword || p.Provider.Info.SupportsPassword)).Select(x => x.Provider).ToArray()
            : PreferredProvider is { } preferred ? [preferred] : Array.Empty<IShortenerProvider>();
        if (selected.Length == 0) { ProviderError = "Chọn dịch vụ đã sẵn sàng hoặc thêm API key trong Cài đặt."; return; }
        IsBusy = true;
        Results.Clear();
        OnPropertyChanged(nameof(EmptyVisibility));
        _cancellation = new CancellationTokenSource();
        var remember = SaveOriginalUrl;
        try
        {
            await foreach (var result in _coordinator.RunAsync(new(uri, RequirePassword ? Password : null), selected,
                Math.Clamp((int)Concurrency, 1, 6), Math.Clamp((int)TimeoutSeconds, 5, 60), _cancellation.Token))
            {
                var existing = Results.FirstOrDefault(x => x.Result.Id == result.Id);
                if (existing is null) Results.Add(new(result)); else existing.Result = result;
                OnPropertyChanged(nameof(EmptyVisibility));
                OnPropertyChanged(nameof(ResultSummary));
                var finished = Results.Count(x => x.Result.Status is ShorteningStatus.Success or ShorteningStatus.Failed or ShorteningStatus.Cancelled);
                ProgressText = $"Đã xử lý {finished}/{selected.Length} dịch vụ";
                if (result.Status is ShorteningStatus.Success or ShorteningStatus.Failed or ShorteningStatus.Cancelled)
                {
                    try { await _history.AddAsync(result with { OriginalUrl = remember ? uri.AbsoluteUri : null }); }
                    catch { ShowNotice("Đã nhận kết quả nhưng không lưu được lịch sử. Bạn vẫn có thể sao chép link."); }
                }
            }
            await RefreshHistoryAsync();
        }
        catch { ShowNotice("Không thể hoàn tất tác vụ. Các kết quả đã tạo vẫn được giữ lại."); }
        finally
        {
            _cancellation.Dispose(); _cancellation = null; Password = ""; IsBusy = false;
            ProgressText = $"Hoàn tất · {ResultSummary}";
        }
    }

    [RelayCommand] public void Cancel() => _cancellation?.Cancel();
    public async Task WaitForCompletionAsync() { if (ShortenCommand.ExecutionTask is { } task) await task; }

    public async Task SaveSettingsAsync()
    {
        try
        {
            var count = _connectionDrafts.SaveAll();
            LoadConnection(); RefreshConnections();
            await _settings.SaveAsync(CurrentSettings());
            ShowNotice($"Đã lưu cài đặt{(count > 0 ? $" và key của {count} dịch vụ" : "")}. Key được mã hóa trên máy này.");
        }
        catch (ArgumentException ex) { ConnectionError = ex.Message; ShowNotice(ex.Message); }
        catch { ConnectionError = "Chưa lưu được tất cả thay đổi. Dữ liệu chưa lưu vẫn được giữ; hãy thử lại."; ShowNotice(ConnectionError); }
    }

    [RelayCommand]
    private void SaveConnection()
    {
        if (!CanEdit || SelectedConnection is null) return;
        try
        {
            var name = SelectedConnection.Provider.Info.SecretName!;
            var saved = _connectionDrafts.Save(name);
            LoadConnection(); RefreshConnections();
            ShowNotice(saved ? $"Đã lưu key {SelectedConnection.Name} trên máy." : "Không có thay đổi cần lưu cho dịch vụ này.");
        }
        catch (ArgumentException ex) { ConnectionError = ex.Message; }
        catch { ConnectionError = "Chưa lưu được key. Phần nhập vẫn được giữ, hãy thử lại."; }
    }

    private AppSettings CurrentSettings() => new(SaveOriginalUrl, Math.Clamp((int)Concurrency, 1, 6), Math.Clamp((int)TimeoutSeconds, 5, 60),
        Providers.ElementAtOrDefault(DefaultProviderIndex)?.Provider.Info.Id ?? "spoo.me",
        BackgroundPolicy.NormalizeColor(BackgroundColor), BackgroundMediaName, BackgroundMotionEnabled, QuickWindowEnabled, DarkThemeEnabled);

    public async Task SaveThemePreferenceAsync()
    {
        if (!IsReady || IsThemeSaving) return;
        IsThemeSaving = true;
        var requested = DarkThemeEnabled;
        try
        {
            var saved = await _settings.LoadAsync();
            await _settings.SaveAsync(saved with { DarkThemeEnabled = requested });
        }
        catch { DarkThemeEnabled = !requested; ShowNotice("Chưa lưu được giao diện sáng/tối. Hãy thử lại."); }
        finally { IsThemeSaving = false; }
    }

    public async Task SaveQuickWindowPreferenceAsync()
    {
        if (!IsReady || IsQuickWindowSaving) return;
        IsQuickWindowSaving = true;
        var requested = QuickWindowEnabled;
        try
        {
            var saved = await _settings.LoadAsync();
            await _settings.SaveAsync(saved with { QuickWindowEnabled = requested });
            QuickWindowPreferenceChanged?.Invoke(requested);
        }
        catch
        {
            QuickWindowEnabled = !requested;
            ShowNotice("Chưa lưu được lựa chọn cửa sổ nổi. Hãy thử lại.");
        }
        finally { IsQuickWindowSaving = false; }
    }

    public async Task<bool> SaveAppearanceAsync()
    {
        try
        {
            var saved = await _settings.LoadAsync();
            await _settings.SaveAsync(saved with { BackgroundColor = BackgroundPolicy.NormalizeColor(BackgroundColor),
                BackgroundMediaName = BackgroundMediaName, BackgroundMotionEnabled = BackgroundMotionEnabled });
            return true;
        }
        catch { ShowNotice("Chưa lưu được giao diện. Hãy thử lại."); return false; }
    }

    [RelayCommand]
    private void RemoveApiKey()
    {
        try { _connectionDrafts.Remove(SelectedConnection!.Provider.Info.SecretName!); LoadConnection(); RefreshConnections(); ShowNotice("Đã xóa API key của dịch vụ đang chọn khỏi máy này."); }
        catch { ShowNotice("Không xóa được API key. Kiểm tra quyền truy cập thư mục dữ liệu."); }
    }
    private void UpdateKeyStatus()
    {
        var connection = SelectedConnection;
        if (connection is null) return;
        var info = connection.Provider.Info;
        var saved = _connectionDrafts.HasSavedKey(info.SecretName!);
        ApiKeyStatus = _connectionDrafts.HasChanges(info.SecretName!)
            ? $"{info.Name} · Chưa lưu thay đổi{(saved ? " · key cũ vẫn được giữ" : "")}"
            : saved ? $"{info.Name} · Đã lưu key mã hóa · ô trống để giữ key hiện tại"
            : $"{info.Name} · Chưa lưu key{(!info.RequiresKey ? " · có thể dùng ẩn danh" : "")}";
    }
    private void RefreshConnections()
    {
        foreach (var provider in Providers) provider.Refresh();
        OnPropertyChanged(nameof(PreferredProviderDisplay));
    }

    public async Task ClearHistoryAsync()
    {
        if (IsBusy) return;
        try { await _history.ClearAsync(); await RefreshHistoryAsync(); ShowNotice("Đã xóa lịch sử trên máy."); }
        catch { ShowNotice("Không xóa được lịch sử. Hãy thử lại sau."); }
    }
    public async Task RefreshHistoryAsync()
    {
        var rows = await _history.ReadAsync();
        History.Clear(); foreach (var result in rows) History.Add(new(result));
        OnPropertyChanged(nameof(HistorySummary)); OnPropertyChanged(nameof(HistoryEmptyVisibility));
    }
}
