using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using QH.MultiLinkShortener.Core;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace QH.MultiLinkShortener.App;

public sealed partial class MainWindow
{
    private BackgroundAssetStore _backgroundAssets = null!;
    private readonly UISettings _uiSettings = new();
    private readonly AccessibilitySettings _accessibility = new();
    private MediaPlayer? _backgroundPlayer;
    private BitmapImage? _backgroundBitmap;
    private bool _backgroundReady, _windowActive = true, _closing;
    private int _backgroundGeneration;
    private Task _backgroundLoad = Task.CompletedTask;
    private readonly DispatcherTimer _appearanceTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool? _previousAnimations, _previousHighContrast;
    private bool AnimationsEnabled
    {
        get
        {
#if DEBUG
            // Exercise playback in the isolated QA profile without changing any Windows preference.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("QH_TEST_DATA_DIRECTORY"))
                && Environment.GetEnvironmentVariable("QH_TEST_ENABLE_MOTION") == "1") return true;
#endif
            return _uiSettings.AnimationsEnabled;
        }
    }

    private void InitializeBackground(BackgroundAssetStore store)
    {
        _backgroundAssets = store;
        VM.PropertyChanged += OnAppearanceChanged;
        Activated += (_, args) => { _windowActive = args.WindowActivationState != WindowActivationState.Deactivated; UpdateBackgroundPlayback(); };
        // Poll these desktop preferences: UWP change-event registration can block an unpackaged window's startup.
        _appearanceTimer.Tick += (_, _) =>
        {
            if (_closing) return;
            var animations = AnimationsEnabled;
            var contrast = _accessibility.HighContrast;
            if (animations == _previousAnimations && contrast == _previousHighContrast) return;
            _previousAnimations = animations; _previousHighContrast = contrast;
            ApplyShellTheme(); UpdateBackgroundPlayback();
            if (!animations || contrast) CompleteNavigationImmediately();
        };
        _appearanceTimer.Start();
        Closed += (_, _) =>
        {
            _closing = true; _backgroundGeneration++;
            VM.PropertyChanged -= OnAppearanceChanged;
            _appearanceTimer.Stop();
            ReleaseBackground();
        };
    }

    private async Task LoadInitialBackgroundAsync()
    {
        _backgroundReady = true;
        ApplyBackgroundColor();
        _backgroundLoad = ApplyBackgroundAsync();
        await _backgroundLoad;
    }

    private void OnAppearanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_backgroundReady || _closing) return;
        if (e.PropertyName == nameof(VM.BackgroundColor)) ApplyBackgroundColor();
        if (e.PropertyName == nameof(VM.BackgroundMediaName)) _backgroundLoad = ApplyBackgroundAsync();
        if (e.PropertyName == nameof(VM.BackgroundMotionEnabled)) UpdateBackgroundPlayback();
    }
    private void ApplyBackgroundColor()
    {
        if (_closing) return;
        var hex = BackgroundPolicy.NormalizeColor(VM.BackgroundColor);
        var color = Color.FromArgb(255, Convert.ToByte(hex[1..3], 16), Convert.ToByte(hex[3..5], 16), Convert.ToByte(hex[5..7], 16));
        ColorPreview.Background = new SolidColorBrush(color);
        if (hex == "#F5F7FB" && VM.DarkThemeEnabled) color = Color.FromArgb(255, 12, 14, 23);
        Root.Background = new SolidColorBrush(_accessibility.HighContrast ? _uiSettings.GetColorValue(UIColorType.Background) : color);
    }

    private void ReleaseBackground()
    {
        BackgroundVideo.SetMediaPlayer(null);
        _backgroundPlayer?.Dispose(); _backgroundPlayer = null;
        _backgroundBitmap?.Stop(); _backgroundBitmap = null;
        BackgroundGif.Source = null;
        BackgroundVideo.Visibility = BackgroundGif.Visibility = MediaLayer.Visibility = Visibility.Collapsed;
    }

    private async Task ApplyBackgroundAsync()
    {
        var generation = ++_backgroundGeneration;
        ReleaseBackground();
        var name = VM.BackgroundMediaName;
        if (name is null || _closing) { UpdateBackgroundPlayback(); return; }
        try
        {
            await _backgroundAssets.ValidateAsync(name);
            if (generation != _backgroundGeneration || _closing) return;
            var file = await StorageFile.GetFileFromPathAsync(_backgroundAssets.GetPath(name)!);
            if (generation != _backgroundGeneration || _closing) return;
            if (name.EndsWith(".gif"))
            {
                var bitmap = new BitmapImage { AutoPlay = false, DecodePixelWidth = 1280 };
                bitmap.ImageFailed += (_, _) => BackgroundPlaybackFailed(generation);
                using var stream = await file.OpenReadAsync();
                await bitmap.SetSourceAsync(stream);
                if (generation != _backgroundGeneration || _closing) return;
                _backgroundBitmap = bitmap;
                BackgroundGif.Source = bitmap;
                BackgroundGif.Visibility = Visibility.Visible;
            }
            else
            {
                _backgroundPlayer = new MediaPlayer { IsMuted = true, IsLoopingEnabled = true, AutoPlay = false };
                _backgroundPlayer.CommandManager.IsEnabled = false;
                _backgroundPlayer.MediaFailed += (_, _) => DispatcherQueue.TryEnqueue(() => BackgroundPlaybackFailed(generation));
                BackgroundVideo.SetMediaPlayer(_backgroundPlayer);
                _backgroundPlayer.Source = MediaSource.CreateFromStorageFile(file);
                BackgroundVideo.Visibility = Visibility.Visible;
            }
            UpdateBackgroundPlayback();
        }
        catch
        {
            if (generation == _backgroundGeneration && !_closing)
            { ReleaseBackground(); VM.ShowNotice("Không phát được nền động đã chọn. Hãy chọn lại GIF/MP4 tối đa 15 giây."); }
        }
    }

    private void BackgroundPlaybackFailed(int generation)
    {
        if (generation != _backgroundGeneration || _closing) return;
        _backgroundGeneration++;
        ReleaseBackground(); VM.ShowNotice("Windows không phát được tệp nền này. Thử GIF khác hoặc MP4 dùng H.264.");
    }

    private void UpdateBackgroundPlayback()
    {
        if (_closing) return;
        var reduced = !AnimationsEnabled || _accessibility.HighContrast;
        VM.SystemMotionSuppressed = reduced;
        var available = _backgroundBitmap is not null || _backgroundPlayer is not null;
        MediaLayer.Visibility = available && !reduced ? Visibility.Visible : Visibility.Collapsed;
        var play = available && VM.BackgroundMotionEnabled && _windowActive && !reduced;
        MotionGlyph.Glyph = VM.BackgroundMotionEnabled ? "\uE769" : "\uE768";
        if (play) { _backgroundPlayer?.Play(); _backgroundBitmap?.Play(); }
        else { _backgroundPlayer?.Pause(); _backgroundBitmap?.Stop(); }
        MotionStatus.Text = reduced ? "Đang dùng màu nền tĩnh theo cài đặt trợ năng của Windows." : "";
        MotionStatus.Visibility = reduced && VM.BackgroundMediaName is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void ChooseColor_Click(object sender, RoutedEventArgs e)
    {
        var color = (ColorPreview.Background as SolidColorBrush)?.Color ?? Microsoft.UI.Colors.White;
        var picker = new ColorPicker { Color = color, IsAlphaEnabled = false, IsAlphaSliderVisible = false,
            IsAlphaTextInputVisible = false, IsColorSliderVisible = false, MinWidth = 280, MaxWidth = 340 };
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "Màu nền của bạn", Content = picker,
            PrimaryButtonText = "Dùng màu này", CloseButtonText = "Hủy", DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        VM.BackgroundColor = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}";
        await VM.SaveAppearanceAsync();
    }

    private async void ImportBackground_Click(object sender, RoutedEventArgs e)
    {
        ImportBackgroundButton.IsEnabled = false;
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary };
            picker.FileTypeFilter.Add(".gif"); picker.FileTypeFilter.Add(".mp4");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            if (await picker.PickSingleFileAsync() is not { } file) return;
            var name = await _backgroundAssets.ImportAsync(file);
            var previous = VM.BackgroundMediaName;
            VM.BackgroundMediaName = name;
            VM.BackgroundMotionEnabled = true;
            if (!await VM.SaveAppearanceAsync())
            { VM.BackgroundMediaName = previous; await _backgroundLoad; _backgroundAssets.Remove(name); return; }
            await _backgroundLoad;
            _backgroundAssets.Remove(previous);
        }
        catch (InvalidDataException ex) { VM.ShowNotice(ex.Message); }
        catch { VM.ShowNotice("Không nhập được nền động. Chọn GIF/MP4 tối đa 15 giây, 20 MB và Full HD."); }
        finally { ImportBackgroundButton.IsEnabled = true; }
    }

    private async void RemoveBackground_Click(object sender, RoutedEventArgs e)
    {
        var previous = VM.BackgroundMediaName;
        VM.BackgroundMediaName = null;
        if (await VM.SaveAppearanceAsync()) _backgroundAssets.Remove(previous);
        else VM.BackgroundMediaName = previous;
    }

    private async void ToggleMotion_Click(object sender, RoutedEventArgs e)
    {
        VM.BackgroundMotionEnabled = !VM.BackgroundMotionEnabled;
        await VM.SaveAppearanceAsync();
    }
}
