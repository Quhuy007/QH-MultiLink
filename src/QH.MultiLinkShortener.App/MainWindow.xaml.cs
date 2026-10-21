using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using QH.MultiLinkShortener.Core;
using QH.MultiLinkShortener.Infrastructure;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;

namespace QH.MultiLinkShortener.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel VM { get; }
    private bool _loaded;
    public event Action? QuickWindowRequested;
    public MainWindow(MainViewModel vm, BackgroundAssetStore backgroundAssets)
    {
        VM = vm;
        InitializeComponent();
        InitializeBackground(backgroundAssets);
        VM.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(VM.RequirePassword) && !VM.RequirePassword)
                LinkPassword.Password = "";
        };
        InitializeShell();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1100, 780));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            presenter.PreferredMinimumWidth = 620;
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter heightPresenter)
            heightPresenter.PreferredMinimumHeight = 480;
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        await VM.InitializeAsync();
        await LoadInitialBackgroundAsync();
        ApplyShellTheme();
        UpdateShellLayout(Root.ActualWidth);
    }
    private void Navigate_Click(object sender, RoutedEventArgs args)
    {
        var tag = (sender as FrameworkElement)?.Tag as string;
        ShowPage(tag ?? "create");
    }
    private void ShowPage(string tag)
    {
        NavigateShell(tag);
    }
    private void QuickWindow_Click(object sender, RoutedEventArgs e) => QuickWindowRequested?.Invoke();
    private async void QuickWindow_Toggled(object sender, RoutedEventArgs e)
    {
        if (!VM.IsReady || VM.IsQuickWindowSaving) return;
        VM.QuickWindowEnabled = ((ToggleSwitch)sender).IsOn;
        await VM.SaveQuickWindowPreferenceAsync();
    }
    private async void UrlInput_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || !VM.CanStart) return;
        e.Handled = true;
        await ExecuteShortenAsync();
    }
    private void UrlInput_GotFocus(object sender, RoutedEventArgs e)
    {
        if (UrlInput.FocusState == FocusState.Keyboard) UrlTip.IsOpen = true;
    }
    private void UrlInput_LostFocus(object sender, RoutedEventArgs e) => UrlTip.IsOpen = false;
    private async Task ExecuteShortenAsync()
    {
        UrlTip.IsOpen = false;
        await VM.ShortenCommand.ExecuteAsync(null);
        if (VM.UrlError.Length != 0) UrlInput.Focus(FocusState.Programmatic);
        else if (VM.PasswordError.Length != 0)
        {
            AdvancedOptions.IsExpanded = true;
            LinkPassword.Focus(FocusState.Programmatic);
        }
        else if (VM.ProviderError.Length == 0) LinkPassword.Password = "";
    }
    private void LinkPassword_Changed(object sender, RoutedEventArgs e) => VM.Password = LinkPassword.Password;
    private async void Shorten_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteShortenAsync();
    }
    private void Copy_Click(object sender, RoutedEventArgs e)
    { if ((sender as Button)?.Tag is ResultRow { CanUse: true } row) Copy(row.Link); }
    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        var links = VM.Results.Where(x => x.CanUse).Select(x => x.Link).ToArray();
        if (links.Length == 0) { VM.ShowNotice("Chưa có link thành công để sao chép."); return; }
        Copy(string.Join(Environment.NewLine, links));
    }
    private void Copy(string value)
    {
        try { var data = new DataPackage(); data.SetText(value); Clipboard.SetContent(data); VM.ShowNotice("Đã sao chép vào bộ nhớ tạm."); }
        catch { VM.ShowNotice("Không truy cập được bộ nhớ tạm. Bạn có thể chọn và sao chép link trực tiếp."); }
    }
    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ResultRow { CanUse: true } row || !UrlValidation.TryParse(row.Link, out var uri)) return;
        try { if (!await Launcher.LaunchUriAsync(uri)) VM.ShowNotice("Không mở được trình duyệt mặc định."); }
        catch { VM.ShowNotice("Không mở được link. Hãy sao chép và mở trong trình duyệt."); }
    }
    private async void Qr_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ResultRow { CanUse: true } row) return;
        try
        {
            var bytes = LocalQr.CreatePng(row.Link);
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); }
            stream.Seek(0);
            var bitmap = new BitmapImage(); await bitmap.SetSourceAsync(stream);
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(new Image { Source = bitmap, Width = 280, Height = 280 });
            panel.Children.Add(new TextBlock { Text = row.Link, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            panel.Children.Add(new TextBlock { Text = "QR được tạo trên máy. Mật khẩu không nằm trong QR.", TextWrapping = TextWrapping.Wrap });
            var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = $"Mã QR · {row.ProviderName}", Content = panel, PrimaryButtonText = "Lưu ảnh PNG", CloseButtonText = "Đóng" };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var picker = new FileSavePicker { SuggestedFileName = $"QH-{row.ProviderName}-QR", SuggestedStartLocation = PickerLocationId.PicturesLibrary };
                picker.FileTypeChoices.Add("Ảnh PNG", new List<string> { ".png" });
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
                if (await picker.PickSaveFileAsync() is { } file) { await FileIO.WriteBytesAsync(file, bytes); VM.ShowNotice("Đã lưu ảnh QR."); }
            }
        }
        catch { VM.ShowNotice("Không tạo hoặc lưu được mã QR. Hãy thử lại."); }
    }
    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        if (double.IsNaN(VM.Concurrency) || double.IsNaN(VM.TimeoutSeconds))
        { VM.ShowNotice("Nhập số hợp lệ cho số dịch vụ đồng thời và thời gian chờ."); return; }
        await VM.SaveSettingsAsync();
    }
    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "Xóa lịch sử trên máy?",
            Content = "Tất cả kết quả đã lưu sẽ bị xóa khỏi máy này. Các link đã tạo trên dịch vụ vẫn hoạt động.",
            PrimaryButtonText = "Xóa lịch sử", CloseButtonText = "Giữ lại", DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) await VM.ClearHistoryAsync();
    }
}
