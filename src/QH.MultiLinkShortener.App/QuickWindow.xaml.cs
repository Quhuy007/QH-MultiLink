using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Automation;
using QH.MultiLinkShortener.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.System;

namespace QH.MultiLinkShortener.App;

public sealed partial class QuickWindow : Window
{
    private readonly QuickShorteningSession _session;
    private readonly Action _showMain;
    private readonly Func<Task> _exit;
    private bool _closing;
    private bool _enabled;
    private QuickStage? _lastStage;
    private readonly DispatcherTimer _placementTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    public QuickWindow(QuickShorteningSession session, Action showMain, Func<Task> exit)
    {
        _session = session; _showMain = showMain; _exit = exit;
        InitializeComponent();
        QuickRoot.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(Quick_KeyDown), true);
        AppWindow.IsShownInSwitchers = false;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.PreferredMinimumWidth = 1;
            presenter.PreferredMinimumHeight = 1;
        }
        AppWindow.Closing += (_, args) =>
        {
            if (_closing) return;
            args.Cancel = true;
            if (_session.IsBusy) _session.Cancel(); else HideToTab();
        };
        _session.Changed += Render;
        _placementTimer.Tick += (_, _) => PlaceWindow();
        Closed += (_, _) => { _placementTimer.Stop(); _session.Changed -= Render; };
        Render();
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
        {
            if (!_session.IsBusy) HideToTab();
            PlaceWindow(); AppWindow.Show(false); _placementTimer.Start();
        }
        else
        {
            _placementTimer.Stop(); _session.Cancel(); AppWindow.Hide();
            if (!_session.IsBusy) HideToTab();
        }
    }
    public void CloseForExit() { _closing = true; Close(); }
    private void Quick_Loaded(object sender, RoutedEventArgs e)
    {
        QuickRoot.XamlRoot.Changed += (_, _) => PlaceWindow();
        PlaceWindow(); if (_enabled) _placementTimer.Start();
    }
    private void PlaceWindow()
    {
        if (!_enabled) return;
        var display = DisplayArea.Primary;
        var work = display.WorkArea;
        var outer = display.OuterBounds;
        var bounds = EdgePlacement.AtRight(outer.X + work.X, outer.Y + work.Y, work.Width, work.Height,
            _session.Stage is not (QuickStage.Collapsed or QuickStage.Loading), QuickRoot.XamlRoot?.RasterizationScale ?? 1);
        if (AppWindow.Position.X != bounds.X || AppWindow.Position.Y != bounds.Y || AppWindow.Size.Width != bounds.Width || AppWindow.Size.Height != bounds.Height)
            AppWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }
    public void OpenInput()
    {
        if (!_enabled || _session.IsBusy) return;
        _session.Open();
        AppWindow.Show(); Activate();
        QuickUrl.Focus(FocusState.Programmatic);
    }
    private void Render()
    {
        var stage = _session.Stage;
        EdgeTab.Visibility = stage == QuickStage.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        BusyTab.Visibility = stage == QuickStage.Loading ? Visibility.Visible : Visibility.Collapsed;
        BusyRing.IsActive = stage == QuickStage.Loading;
        InputPanel.Visibility = stage == QuickStage.Input ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility = stage == QuickStage.Result ? Visibility.Visible : Visibility.Collapsed;
        ErrorPanel.Visibility = stage == QuickStage.Error ? Visibility.Visible : Visibility.Collapsed;
        ShortLink.Text = _session.Result?.ShortUrl ?? "";
        var detail = _session.Result is { } result ? $"{result.ProviderName} · {result.ElapsedMs:N0} ms · {result.Protection switch
        { ProtectionStatus.Confirmed => "Đã xác nhận bảo vệ", ProtectionStatus.Requested => "Đã gửi mật khẩu theo API", _ => "Link công khai" }}" : "";
        ToolTipService.SetToolTip(ShortLink, string.IsNullOrEmpty(_session.Message) ? detail : _session.Message);
        AutomationProperties.SetHelpText(ShortLink, string.IsNullOrEmpty(_session.Message) ? detail : _session.Message);
        ErrorText.Text = _session.Message;
        ToolTipService.SetToolTip(ErrorText, _session.Message);
        PlaceWindow();
        if (_lastStage != stage)
        {
            if (stage == QuickStage.Loading) QuickPassword.Password = "";
            if (stage == QuickStage.Result) { CopyButton.Content = "Sao chép"; CopyButton.Focus(FocusState.Programmatic); }
            _lastStage = stage;
        }
    }
    private void Url_Changed(object sender, TextChangedEventArgs e)
    {
        if (QuickPassword is null) return;
        var valid = UrlValidation.TryParse(QuickUrl.Text, out _);
        QuickPassword.Visibility = valid ? Visibility.Visible : Visibility.Collapsed;
        if (!valid) QuickPassword.Password = "";
    }
    private async void Quick_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            if (_session.IsBusy) _session.Cancel(); else HideToTab();
        }
        else if (e.Key == VirtualKey.Enter && _session.Stage == QuickStage.Input)
        { e.Handled = true; await SubmitAsync(); }
    }
    private async Task SubmitAsync()
    {
        var password = QuickPassword.Password;
        var operation = _session.SubmitAsync(QuickUrl.Text, password);
        QuickPassword.Password = "";
        await operation;
    }
    private void HideToTab()
    {
        if (_session.IsBusy) return;
        QuickUrl.Text = ""; QuickPassword.Password = ""; _session.Collapse();
    }
    private void Open_Click(object sender, RoutedEventArgs e) => OpenInput();
    private void Hide_Click(object sender, RoutedEventArgs e) => HideToTab();
    private void Retry_Click(object sender, RoutedEventArgs e) => OpenInput();
    private void Cancel_Click(object sender, RoutedEventArgs e) => _session.Cancel();
    private void Main_Click(object sender, RoutedEventArgs e) => _showMain();
    private async void Exit_Click(object sender, RoutedEventArgs e) => await _exit();
    private async void Create_Click(object sender, RoutedEventArgs e) => await SubmitAsync();
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_session.Result is not { Status: ShorteningStatus.Success, ShortUrl: { } link }) return;
        try
        {
            var data = new DataPackage(); data.SetText(link); Clipboard.SetContent(data);
            CopyButton.Content = "Đã sao chép";
        }
        catch { CopyButton.Content = "Thử chép lại"; _session.ShowCopyError(); }
    }
}
