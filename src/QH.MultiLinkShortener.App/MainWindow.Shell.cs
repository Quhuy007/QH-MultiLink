using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace QH.MultiLinkShortener.App;

public sealed partial class MainWindow
{
    private int _selectedSection;
    private int _navigationVersion;
    private Storyboard? _navMotion, _pageMotion;
    private FrameworkElement[] ShellPages => [CreatePage, HistoryPage, SettingsPage];
    private Microsoft.UI.Xaml.Controls.Primitives.ToggleButton[] ShellButtons => [CreateNavButton, HistoryNavButton, SettingsNavButton];

    private void InitializeShell()
    {
        VM.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(VM.DarkThemeEnabled)) ApplyShellTheme();
        };
        foreach (var page in ShellPages) page.RenderTransform = new TranslateTransform();
        Closed += (_, _) => { _navMotion?.Stop(); _pageMotion?.Stop(); };
        ApplyShellTheme();
    }

    private void Root_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateShellLayout(e.NewSize.Width);
    private void UpdateShellLayout(double width)
    {
        if (width <= 0) return;
        var full = width >= 760;
        SidebarColumn.Width = new GridLength(full ? 188 : 72);
        foreach (var label in new FrameworkElement[] { BrandLabel, CreateNavLabel, HistoryNavLabel, SettingsNavLabel, QuickNavLabel, ThemeLabel })
            label.Visibility = full ? Visibility.Visible : Visibility.Collapsed;
        MainContent.Padding = new Thickness(full ? 24 : 18);
        var split = width >= 1040;
        ResultsColumn.Width = split ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(ResultsPane, split ? 1 : 0); Grid.SetRow(ResultsPane, split ? 0 : 1);
    }

    private void ApplyShellTheme()
    {
        Root.RequestedTheme = VM.DarkThemeEnabled ? ElementTheme.Dark : ElementTheme.Light;
        var dark = VM.DarkThemeEnabled;
        if (Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
        {
            var background = dark ? Color.FromArgb(255, 12, 14, 23) : Color.FromArgb(255, 245, 247, 251);
            var ink = dark ? Microsoft.UI.Colors.White : Color.FromArgb(255, 25, 29, 40);
            if (_accessibility.HighContrast)
            { background = _uiSettings.GetColorValue(UIColorType.Background); ink = _uiSettings.GetColorValue(UIColorType.Foreground); }
            AppWindow.TitleBar.BackgroundColor = background; AppWindow.TitleBar.ForegroundColor = ink;
            AppWindow.TitleBar.ButtonBackgroundColor = background; AppWindow.TitleBar.ButtonForegroundColor = ink;
            AppWindow.TitleBar.InactiveBackgroundColor = background; AppWindow.TitleBar.ButtonInactiveBackgroundColor = background;
        }
        ApplyBackgroundColor(); UpdateNavigationInk();
    }

    private async void Theme_Toggled(object sender, RoutedEventArgs e)
    {
        if (!VM.IsReady || VM.IsThemeSaving) return;
        VM.DarkThemeEnabled = ((ToggleSwitch)sender).IsOn;
        await VM.SaveThemePreferenceAsync();
    }

    private void UpdateNavigationInk()
    {
        var active = _accessibility.HighContrast ? _uiSettings.UIElementColor(UIElementType.HighlightText)
            : VM.DarkThemeEnabled ? Color.FromArgb(255, 218, 211, 255) : Color.FromArgb(255, 37, 87, 191);
        var inactive = _accessibility.HighContrast ? _uiSettings.GetColorValue(UIColorType.Foreground)
            : VM.DarkThemeEnabled ? Color.FromArgb(255, 181, 182, 194) : Color.FromArgb(255, 83, 90, 107);
        var buttons = ShellButtons;
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].IsChecked = i == _selectedSection;
            buttons[i].Foreground = new SolidColorBrush(i == _selectedSection ? active : inactive);
            AutomationProperties.SetItemStatus(buttons[i], i == _selectedSection ? "Đang chọn" : "");
        }
    }

    private void NavigateShell(string tag)
    {
        var next = tag switch { "history" => 1, "settings" => 2, _ => 0 };
        if (next == _selectedSection) { UpdateNavigationInk(); return; }
        var previous = _selectedSection;
        var version = ++_navigationVersion;
        _selectedSection = next; UpdateNavigationInk();
        var currentY = NavTranslation.Y;
        _navMotion?.Stop(); _pageMotion?.Stop();
        NavTranslation.Y = currentY;
        foreach (var page in ShellPages)
        {
            page.Visibility = Visibility.Collapsed; page.Opacity = 1; page.IsHitTestVisible = true;
            ((TranslateTransform)page.RenderTransform).X = 0;
        }
        var incoming = ShellPages[next]; incoming.Visibility = Visibility.Visible;
        if (!AnimationsEnabled || _accessibility.HighContrast)
        { NavTranslation.Y = next * 50; return; }
        _navMotion = new Storyboard();
        AddMotion(_navMotion, NavTranslation, "Y", currentY, next * 50, 240); _navMotion.Begin();
        var outgoing = ShellPages[previous];
        outgoing.Visibility = Visibility.Visible; outgoing.IsHitTestVisible = false;
        Canvas.SetZIndex(incoming, 1); Canvas.SetZIndex(outgoing, 0);
        var direction = next > previous ? 1 : -1;
        _pageMotion = new Storyboard();
        AddMotion(_pageMotion, incoming, "Opacity", 0, 1, 220);
        AddMotion(_pageMotion, incoming.RenderTransform, "X", direction * 18, 0, 240);
        AddMotion(_pageMotion, outgoing, "Opacity", 1, 0, 140);
        AddMotion(_pageMotion, outgoing.RenderTransform, "X", 0, -direction * 12, 180);
        _pageMotion.Completed += (_, _) =>
        {
            if (version != _navigationVersion) return;
            outgoing.Visibility = Visibility.Collapsed; outgoing.IsHitTestVisible = true;
        };
        _pageMotion.Begin();
    }

    private void CompleteNavigationImmediately()
    {
        ++_navigationVersion; _navMotion?.Stop(); _pageMotion?.Stop();
        NavTranslation.Y = _selectedSection * 50;
        var pages = ShellPages;
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i].Visibility = i == _selectedSection ? Visibility.Visible : Visibility.Collapsed;
            pages[i].Opacity = 1; pages[i].IsHitTestVisible = true;
            ((TranslateTransform)pages[i].RenderTransform).X = 0;
        }
    }

    private static void AddMotion(Storyboard storyboard, DependencyObject target, string property, double from, double to, int milliseconds)
    {
        var animation = new DoubleAnimation { From = from, To = to, Duration = new Duration(TimeSpan.FromMilliseconds(milliseconds)),
            EnableDependentAnimation = true, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(animation, target); Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }
}
