using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using QH.MultiLinkShortener.Infrastructure;
using QH.MultiLinkShortener.Core;
using Microsoft.UI.Windowing;

namespace QH.MultiLinkShortener.App;

public partial class App : Application
{
    private IHost? _host;
    private MainWindow? _window;
    private QuickWindow? _quickWindow;
    private QuickShorteningSession? _quickSession;
    private bool _exiting;
    public App() { InitializeComponent(); }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // No default HTTP/console logging: destination URLs and tokens must stay private.
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Logging.ClearProviders();
        var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QH", "MultiLink");
#if DEBUG
        if (Environment.GetEnvironmentVariable("QH_TEST_DATA_DIRECTORY") is { Length: > 0 } testDirectory)
            dataDirectory = Path.GetFullPath(testDirectory);
#endif
        builder.Services.AddShortener(dataDirectory);
        builder.Services.AddSingleton(new BackgroundAssetStore(dataDirectory));
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddSingleton<QuickShorteningSession>();
        _host = builder.Build();
        await _host.StartAsync();
        _window = new MainWindow(_host.Services.GetRequiredService<MainViewModel>(), _host.Services.GetRequiredService<BackgroundAssetStore>());
        _quickSession = _host.Services.GetRequiredService<QuickShorteningSession>();
        _quickWindow = new QuickWindow(_quickSession, ShowMain, ExitAsync);
        _window.QuickWindowRequested += _quickWindow.OpenInput;
        _window.VM.QuickWindowPreferenceChanged += _quickWindow.SetEnabled;
        _window.VM.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MainViewModel.IsReady) && _window.VM.IsReady)
                _quickWindow.SetEnabled(_window.VM.QuickWindowEnabled);
        };
        _window.AppWindow.Closing += (_, eventArgs) =>
        {
            if (_exiting) return;
            eventArgs.Cancel = true;
            if (_window.VM.QuickWindowEnabled) _window.AppWindow.Hide();
            else _window.DispatcherQueue.TryEnqueue(async () => await ExitAsync());
        };
        _quickSession.HistoryChanged += async () =>
        {
            try { await _window.VM.RefreshHistoryAsync(); }
            catch { _window.VM.ShowNotice("Chưa cập nhật được lịch sử. Hãy mở lại ứng dụng để tải lại."); }
        };
        _window.Activate();
    }
    private void ShowMain()
    {
        if (_window is null) return;
        if (_window.AppWindow.Presenter is OverlappedPresenter presenter && presenter.State == OverlappedPresenterState.Minimized)
            presenter.Restore();
        _window.AppWindow.Show(); _window.Activate();
    }
    private async Task ExitAsync()
    {
        if (_exiting) return;
        _exiting = true;
        _quickSession?.Cancel(); _window?.VM.Cancel();
        if (_quickSession is not null) await _quickSession.Completion;
        if (_window is not null) await _window.VM.WaitForCompletionAsync();
        if (_host is not null) { await _host.StopAsync(); _host.Dispose(); }
        _quickWindow?.CloseForExit(); _window?.Close();
        Exit();
    }
}
