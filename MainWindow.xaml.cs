using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TXATOOL.Helpers;

namespace TXATOOL;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Views.DashboardView? _dashboardView;
    private Views.GeneralView? _generalView;
    private Views.WindowsView? _windowsView;
    private Views.WindowsDescriptionView? _windowsDescriptionView;
    private Views.GameConfigsView? _gameConfigsView;
    private Views.GameConfigsDescriptionView? _gameConfigsDescriptionView;
    private bool _isJobCancelled = false;

    private Views.DefaultDescriptionView? _defaultDescriptionView;
    private Views.CleaningView? _cleaningView;
    private Views.CleaningRightView? _cleaningRightView;
    private Views.StartupAppsView? _startupAppsView;
    private Views.StartupDescriptionView? _startupDescriptionView;
    private Views.UWPAppsView? _uwpAppsView;
    private Views.UWPDescriptionView? _uwpDescriptionView;
    private Views.ExtendFeaturesView? _extendFeaturesView;
    private Views.ExtendFeaturesDescriptionView? _extendFeaturesDescriptionView;
    private Views.USBBootView? _usbBootView;
    private Views.USBBootDescriptionView? _usbBootDescriptionView;
    private Views.WSAPView? _wsapView;
    private Views.WSAPDescriptionView? _wsapDescriptionView;
    private Views.FixGameErrorsView? _fixGameErrorsView;
    private Views.FixGameErrorsDescriptionView? _fixGameErrorsDescriptionView;
    private Views.PrinterErrorsView? _printerErrorsView;
    private Views.PrinterErrorsDescriptionView? _printerErrorsDescriptionView;
    private Views.WindowsErrorsView? _windowsErrorsView;
    private Views.WindowsErrorsDescriptionView? _windowsErrorsDescriptionView;
    private Views.HardwareInfoView? _hardwareInfoView;
    private Views.HardwareInfoDescriptionView? _hardwareInfoDescriptionView;
    private Views.SettingsView? _settingsView;
    private Views.SettingsDescriptionView? _settingsDescriptionView;
    private Views.DownloadView? _downloadView;
    private Views.NetworkView? _networkView;
    private Views.NetworkDescriptionView? _networkDescriptionView;
    private Views.OfficeView? _officeView;
    private Views.ConvertSKUView? _convertSKUView;
    private Views.LicenseView? _licenseView;

    // Lazy-loaded properties for high performance and instant startup
    private Views.GeneralView GeneralView => _generalView ??= new Views.GeneralView();
    private Views.WindowsView WindowsView => _windowsView ??= new Views.WindowsView();
    private Views.WindowsDescriptionView WindowsDescriptionView => _windowsDescriptionView ??= new Views.WindowsDescriptionView();
    private Views.GameConfigsView GameConfigsView => _gameConfigsView ??= new Views.GameConfigsView();
    private Views.GameConfigsDescriptionView GameConfigsDescriptionView => _gameConfigsDescriptionView ??= new Views.GameConfigsDescriptionView();
    private Views.CleaningView CleaningView => _cleaningView ??= new Views.CleaningView();
    private Views.CleaningRightView CleaningRightView => _cleaningRightView ??= new Views.CleaningRightView();
    private Views.StartupAppsView StartupAppsView => _startupAppsView ??= new Views.StartupAppsView();
    private Views.StartupDescriptionView StartupDescriptionView => _startupDescriptionView ??= new Views.StartupDescriptionView();
    private Views.UWPAppsView UWPAppsView => _uwpAppsView ??= new Views.UWPAppsView();
    private Views.UWPDescriptionView UWPDescriptionView => _uwpDescriptionView ??= new Views.UWPDescriptionView();
    private Views.ExtendFeaturesView ExtendFeaturesView => _extendFeaturesView ??= new Views.ExtendFeaturesView();
    private Views.ExtendFeaturesDescriptionView ExtendFeaturesDescriptionView => _extendFeaturesDescriptionView ??= new Views.ExtendFeaturesDescriptionView();
    private Views.USBBootView USBBootView => _usbBootView ??= new Views.USBBootView();
    private Views.USBBootDescriptionView USBBootDescriptionView => _usbBootDescriptionView ??= new Views.USBBootDescriptionView();
    private Views.WSAPView WSAPView => _wsapView ??= new Views.WSAPView();
    private Views.WSAPDescriptionView WSAPDescriptionView => _wsapDescriptionView ??= new Views.WSAPDescriptionView();
    private Views.FixGameErrorsView FixGameErrorsView => _fixGameErrorsView ??= new Views.FixGameErrorsView();
    private Views.FixGameErrorsDescriptionView FixGameErrorsDescriptionView => _fixGameErrorsDescriptionView ??= new Views.FixGameErrorsDescriptionView();
    private Views.PrinterErrorsView PrinterErrorsView => _printerErrorsView ??= new Views.PrinterErrorsView();
    private Views.PrinterErrorsDescriptionView PrinterErrorsDescriptionView => _printerErrorsDescriptionView ??= new Views.PrinterErrorsDescriptionView();
    private Views.WindowsErrorsView WindowsErrorsView => _windowsErrorsView ??= new Views.WindowsErrorsView();
    private Views.WindowsErrorsDescriptionView WindowsErrorsDescriptionView => _windowsErrorsDescriptionView ??= new Views.WindowsErrorsDescriptionView();
    private Views.HardwareInfoView HardwareInfoView => _hardwareInfoView ??= new Views.HardwareInfoView();
    private Views.HardwareInfoDescriptionView HardwareInfoDescriptionView => _hardwareInfoDescriptionView ??= new Views.HardwareInfoDescriptionView();
    private Views.SettingsView SettingsView => _settingsView ??= new Views.SettingsView();
    private Views.SettingsDescriptionView SettingsDescriptionView => _settingsDescriptionView ??= new Views.SettingsDescriptionView();
    private Views.DownloadView DownloadView => _downloadView ??= new Views.DownloadView();
    private Views.NetworkView NetworkView => _networkView ??= new Views.NetworkView();
    private Views.NetworkDescriptionView NetworkDescriptionView => _networkDescriptionView ??= new Views.NetworkDescriptionView();
    private Views.OfficeView OfficeView => _officeView ??= new Views.OfficeView();
    private Views.ConvertSKUView ConvertSKUView => _convertSKUView ??= new Views.ConvertSKUView();
    private Views.LicenseView LicenseView
    {
        get
        {
            if (_licenseView == null)
            {
                _licenseView = new Views.LicenseView();
                _licenseView.NavigateRequested += DashboardView_NavigateRequested;
            }
            return _licenseView;
        }
    }
    private Views.DefaultDescriptionView DefaultDescriptionView => _defaultDescriptionView ??= new Views.DefaultDescriptionView();

    public MainWindow()
    {
        InitializeComponent();
        InitNavGroups();
        this.Loaded += MainWindow_Loaded;

        // Initialize localization: load saved language preference
        string savedLang = LocalizationService.LoadLanguagePreference();
        LocalizationService.LoadLanguage(savedLang);
        LocalizationService.LanguageChanged += OnLanguageChanged;

        _dashboardView = new Views.DashboardView();
        _dashboardView.NavigateRequested += DashboardView_NavigateRequested;

        UpdateView(_dashboardView, null, true);
        
        // Hide SearchContainer by default (General tab is active)
        if (SearchContainer != null) SearchContainer.Visibility = Visibility.Collapsed;

        if (GlobalLoadingOverlay != null)
        {
            GlobalLoadingOverlay.Cancelled += GlobalLoadingOverlay_Cancelled;
        }
    }
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Fix for app starting in background/minimized
        this.Activate();
        this.Topmost = true;
        this.Topmost = false;
        this.Focus();

        // Apply translations to MainWindow (navigation sidebar)
        ApplyMainWindowTranslations();

        // Dynamically scale down if the screen logical resolution is too small
        double workAreaWidth = SystemParameters.WorkArea.Width;
        double workAreaHeight = SystemParameters.WorkArea.Height;

        if (this.Height > workAreaHeight || this.Width > workAreaWidth)
        {
            double scaleY = (workAreaHeight * 0.95) / this.Height;
            double scaleX = (workAreaWidth * 0.95) / this.Width;
            double minScale = Math.Min(scaleX, Math.Min(scaleY, 1.0));

            this.Width *= minScale;
            this.Height *= minScale;

            this.Left = (workAreaWidth - this.Width) / 2 + SystemParameters.WorkArea.Left;
            this.Top = (workAreaHeight - this.Height) / 2 + SystemParameters.WorkArea.Top;
        }
    }

    private void OnLanguageChanged()
    {
        // Refresh navigation sidebar text when language changes
        Dispatcher.Invoke(() => ApplyMainWindowTranslations());
    }

    // Sidebar categories (accordion): header button Tag = panel name
    private static readonly (string Panel, string Chevron)[] NavGroups =
    {
        ("GroupDeploy", "ChvDeploy"),
        ("GroupOptimize", "ChvOptimize"),
        ("GroupSoftware", "ChvSoftware"),
        ("GroupDiagnostics", "ChvDiagnostics")
    };

    private void SetNavGroup(string panelName, bool expanded)
    {
        if (FindName(panelName) is not StackPanel panel) return;
        panel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        foreach (var g in NavGroups)
        {
            if (g.Panel == panelName && FindName(g.Chevron) is TextBlock chev)
                chev.Text = expanded ? "\uE70D" : "\uE76C";
        }
    }

    private void ExpandOnly(string panelName)
    {
        foreach (var g in NavGroups) SetNavGroup(g.Panel, g.Panel == panelName);
    }

    private void NavGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string panelName) return;
        bool open = FindName(panelName) is StackPanel p && p.Visibility == Visibility.Visible;
        if (open) SetNavGroup(panelName, false); else ExpandOnly(panelName);
    }

    private void NavItem_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Parent is StackPanel sp && sp.Name.StartsWith("Group"))
            ExpandOnly(sp.Name);
    }

    private void InitNavGroups()
    {
        foreach (var rb in new[] { NavGeneral, NavWindows, NavStartup, NavUWPApps, NavCleaning, NavHardware, NavNetwork, NavUSBBoot,
                                   NavDownload, NavOffice, NavExtendFeatures, NavWSAP, NavConvertSKU, NavLicense,
                                   NavWindowsErrors, NavPrinterErrors, NavFixGame, NavGameConfigs })
            rb.Checked += NavItem_Checked;
        ExpandOnly("GroupOptimize");
    }

    private string _searchPlaceholder = "Type here to search...";

    private void ApplyMainWindowTranslations()
    {
        string newPlaceholder = LocalizationService.Get("searchPlaceholder") ?? "Type here to search...";
        if (SearchBox != null && SearchBox.Text == _searchPlaceholder) SearchBox.Text = newPlaceholder;
        _searchPlaceholder = newPlaceholder;
        SettingsButton.ToolTip = LocalizationService.Get("winBtnSettings") ?? "Settings";
        MinimizeButton.ToolTip = LocalizationService.Get("winBtnMinimize") ?? "Minimize";
        CloseButton.ToolTip = LocalizationService.Get("winBtnClose") ?? "Close";
        System.Windows.Automation.AutomationProperties.SetName(SettingsButton, (string)SettingsButton.ToolTip);
        System.Windows.Automation.AutomationProperties.SetName(MinimizeButton, (string)MinimizeButton.ToolTip);
        System.Windows.Automation.AutomationProperties.SetName(CloseButton, (string)CloseButton.ToolTip);
        System.Windows.Automation.AutomationProperties.SetName(SearchBox, newPlaceholder);
        RestartButton.Content = LocalizationService.Get("btnRestartToApply") ?? "Restart to apply changes";

        // Direct assignments — safer than visual tree traversal for custom templates
        if (NavGroupDeploy != null) NavGroupDeploy.Text = LocalizationService.Get("NavGroupDeploy") ?? "OS Deployment";
        if (NavGroupOptimize != null) NavGroupOptimize.Text = LocalizationService.Get("NavGroupOptimize") ?? "Optimization";
        if (NavGroupSoftware != null) NavGroupSoftware.Text = LocalizationService.Get("NavGroupSoftware") ?? "Software & Tools";
        if (NavGroupDiagnostics != null) NavGroupDiagnostics.Text = LocalizationService.Get("NavGroupDiagnostics") ?? "Diagnostics & Repair";

        NavDashboard.Content = LocalizationService.Get("NavDashboard");
        NavGeneral.Content = LocalizationService.Get("NavGeneral");
        NavWindows.Content = LocalizationService.Get("NavWindows");
        NavUWPApps.Content = LocalizationService.Get("NavUWPApps");
        NavStartup.Content = LocalizationService.Get("NavStartup");
        NavDownload.Content = LocalizationService.Get("NavDownload");
        NavCleaning.Content = LocalizationService.Get("NavCleaning");
        NavNetwork.Content = LocalizationService.Get("NavNetwork");
        NavUSBBoot.Content = LocalizationService.Get("NavUSBBoot");
        NavHardware.Content = LocalizationService.Get("NavHardware");
        NavPrinterErrors.Content = LocalizationService.Get("NavPrinterErrors");
        NavWindowsErrors.Content = LocalizationService.Get("titleWindowsErrors");
        NavOffice.Content = LocalizationService.Get("NavOffice") ?? "Office";
        NavExtendFeatures.Content = LocalizationService.Get("NavExtendFeatures");
        NavGameConfigs.Content = LocalizationService.Get("NavGameConfigs");
        NavFixGame.Content = LocalizationService.Get("NavFixGame");
        NavWSAP.Content = LocalizationService.Get("NavWSAP");
        NavConvertSKU.Content = LocalizationService.Get("NavConvertSKU") ?? "Convert SKUs";
        NavLicense.Content = LocalizationService.Get("NavLicense") ?? "License";
    }

    private void GlobalLoadingOverlay_Cancelled(object sender, EventArgs e)
    {
        _isJobCancelled = true;
        HideLoading();
        // The running task will fail on next progress update or if it checks flags
    }

    private void UpdateView(UserControl mainView, UserControl? rightView, bool spansTwoColumns = true)
    {
        if (MainContentControl != null)
        {
            MainContentControl.Content = mainView;
            Grid.SetColumnSpan(MainContentControl, spansTwoColumns ? 2 : 1);
            MainContentControl.Margin = spansTwoColumns ? new Thickness(20, 20, 20, 20) : new Thickness(30, 20, 10, 20);
        }
        if (RightPanelContent != null)
        {
            RightPanelContent.Content = rightView;
        }
        if (RightPanelBorder != null)
        {
            // If spansTwoColumns is true and rightView is null (or specific logic), we might want to hide the border to avoid blocking clicks.
            // For DownloadView specifically, we want full width and NO right panel overlay.
            // A simple heuristic: if rightView is null, hide the border.
            RightPanelBorder.Visibility = (rightView == null) ? Visibility.Collapsed : Visibility.Visible;
        }
        if (SearchContainer != null)
        {
            // Show search only for Download View
            bool isDownload = mainView is Views.DownloadView;
            SearchContainer.Visibility = isDownload ? Visibility.Visible : Visibility.Collapsed;

            if (isDownload && SearchBox != null)
            {
                SearchBox.Text = _searchPlaceholder;
                SearchBox.Foreground = Helpers.ThemeBrushes.Get("TextSecondaryBrush");
            }
        }

        if (MainBackgroundBorder != null)
        {
            bool isTransparentView = (mainView == _dashboardView || mainView is Views.UWPAppsView);
            MainBackgroundBorder.Visibility = isTransparentView ? Visibility.Collapsed : Visibility.Visible;
            if (!isTransparentView)
            {
                // If rightView is null (Download View), expand background to 2 columns and adjust margin for symmetry.
                // Otherwise, keep it in column 1.
                Grid.SetColumnSpan(MainBackgroundBorder, (rightView == null) ? 2 : 1);
                MainBackgroundBorder.Margin = (rightView == null) ? new Thickness(20, 20, 20, 20) : new Thickness(20, 20, 10, 20);
            }
        }
    }

    private void Dashboard_Checked(object sender, RoutedEventArgs e) => UpdateView(_dashboardView, null, true);

    private void DashboardView_NavigateRequested(object? sender, string target)
    {
         switch (target)
         {
             case "General": NavGeneral.IsChecked = true; UpdateView(GeneralView, DefaultDescriptionView); break;
             case "Windows": NavWindows.IsChecked = true; UpdateView(WindowsView, WindowsDescriptionView); break;
             case "Download": NavDownload.IsChecked = true; UpdateView(DownloadView, null, true); break;
             case "Office": NavOffice.IsChecked = true; UpdateView(OfficeView, null, true); break;
             case "ExtendFeatures": NavExtendFeatures.IsChecked = true; UpdateView(ExtendFeaturesView, ExtendFeaturesDescriptionView, false); break;
             case "UWPApps": NavUWPApps.IsChecked = true; UpdateView(UWPAppsView, null, true); break;
             case "StartupApps": NavStartup.IsChecked = true; UpdateView(StartupAppsView, StartupDescriptionView); break;
             case "Cleaning": NavCleaning.IsChecked = true; UpdateView(CleaningView, CleaningRightView); break;
             case "Network": NavNetwork.IsChecked = true; UpdateView(NetworkView, NetworkDescriptionView, false); break;
             case "USBBoot": NavUSBBoot.IsChecked = true; UpdateView(USBBootView, USBBootDescriptionView, false); break;
             case "HardwareInfo": NavHardware.IsChecked = true; UpdateView(HardwareInfoView, HardwareInfoDescriptionView, false); break;
             case "GameConfigs": NavGameConfigs.IsChecked = true; UpdateView(GameConfigsView, null, true); break;
             case "FixGameErrors": NavFixGame.IsChecked = true; UpdateView(FixGameErrorsView, null, true); break;
             case "WSAP": NavWSAP.IsChecked = true; UpdateView(WSAPView, WSAPDescriptionView, false); break;
             case "ConvertSKU": NavConvertSKU.IsChecked = true; UpdateView(ConvertSKUView, null, true); break;
             case "License": NavLicense.IsChecked = true; UpdateView(LicenseView, null, true); break;
             case "PrinterErrors": NavPrinterErrors.IsChecked = true; UpdateView(PrinterErrorsView, PrinterErrorsDescriptionView, false); break;
             case "WindowsErrors": NavWindowsErrors.IsChecked = true; UpdateView(WindowsErrorsView, WindowsErrorsDescriptionView, false); break;
         }
    }

    private void General_Checked(object sender, RoutedEventArgs e) => UpdateView(GeneralView, DefaultDescriptionView);
    private void Windows_Checked(object sender, RoutedEventArgs e) => UpdateView(WindowsView, WindowsDescriptionView);
    private void GameConfigs_Checked(object sender, RoutedEventArgs e) 
    {
        UpdateView(GameConfigsView, null, true);
    }
    private void Cleaning_Checked(object sender, RoutedEventArgs e) => UpdateView(CleaningView, CleaningRightView);
    private void Startup_Checked(object sender, RoutedEventArgs e) => UpdateView(StartupAppsView, StartupDescriptionView);
    private void UWPApps_Checked(object sender, RoutedEventArgs e) => UpdateView(UWPAppsView, null, true); // Full width integrated Master-Detail view
    private void Download_Checked(object sender, RoutedEventArgs e) => UpdateView(DownloadView, null, true); // Full width, no description
    private void Office_Checked(object sender, RoutedEventArgs e) => UpdateView(OfficeView, null, true); // Office has its own right panel
    private void ConvertSKU_Checked(object sender, RoutedEventArgs e) => UpdateView(ConvertSKUView, null, true); // ConvertSKU has its own right panel
    private void ExtendFeatures_Checked(object sender, RoutedEventArgs e) 
    {
        UpdateView(ExtendFeaturesView, ExtendFeaturesDescriptionView, false);
    }
    private void USBBoot_Checked(object sender, RoutedEventArgs e) => UpdateView(USBBootView, USBBootDescriptionView, false);
    private void FixGameErrors_Checked(object sender, RoutedEventArgs e) 
    {
        UpdateView(FixGameErrorsView, null, true);
    }
    private void WSAP_Checked(object sender, RoutedEventArgs e) 
    {
        UpdateView(WSAPView, WSAPDescriptionView, false);
        // Refresh disk list on tab switch
        WSAPView.ReloadDisks();
    }
    private void License_Checked(object sender, RoutedEventArgs e) => UpdateView(LicenseView, null, true);
    private void HardwareInfo_Checked(object sender, RoutedEventArgs e) => UpdateView(HardwareInfoView, HardwareInfoDescriptionView, false);
    private void PrinterErrors_Checked(object sender, RoutedEventArgs e) => UpdateView(PrinterErrorsView, PrinterErrorsDescriptionView, false);
    private void WindowsErrors_Checked(object sender, RoutedEventArgs e) => UpdateView(WindowsErrorsView, WindowsErrorsDescriptionView, false);

    private void Network_Checked(object sender, RoutedEventArgs e) => UpdateView(NetworkView, NetworkDescriptionView, false);

    private void NavDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (MainContentControl?.Content == SettingsView || NavDashboard.IsChecked != true)
        {
            NavDashboard.IsChecked = true;
            UpdateView(_dashboardView, null, true);
        }
    }

    private void UncheckAllNavButtons()
    {
        var navButtons = new RadioButton?[]
        {
            NavDashboard, NavGeneral, NavWindows, NavStartup, NavUWPApps, NavCleaning, NavHardware, NavNetwork,
            NavUSBBoot, NavDownload, NavOffice, NavExtendFeatures, NavWSAP, NavConvertSKU, NavLicense,
            NavWindowsErrors, NavPrinterErrors, NavFixGame, NavGameConfigs
        };
        foreach (var rb in navButtons)
        {
            if (rb != null)
            {
                rb.IsChecked = false;
            }
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        UncheckAllNavButtons();
        UpdateView(SettingsView, SettingsDescriptionView, false);
    }

    private void GlobalRestart_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new Views.ModernConfirmationWindow(
            LocalizationService.Get("msgRestartConfirmBody") ?? "Your computer will restart immediately. Save your work first. Restart now?",
            LocalizationService.Get("msgRestartConfirmTitle") ?? "Restart your computer",
            LocalizationService.Get("btnRestartNow") ?? "Restart now",
            LocalizationService.Get("btnCancel") ?? "Cancel");
        confirm.Owner = this; 
        if (confirm.ShowDialog() == true)
        {
             try
             {
                 System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown", "/r /t 0") { CreateNoWindow = true, UseShellExecute = false });
             }
             catch (Exception ex)
             {
                 MessageBox.Show(this,
                     (LocalizationService.Get("msgRestartFailed") ?? "Could not restart the computer. Restart it manually from the Start menu.") + "\n\n" + ex.Message,
                     LocalizationService.Get("msgRestartConfirmTitle") ?? "Restart your computer",
                     MessageBoxButton.OK, MessageBoxImage.Warning);
             }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }
    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && (textBox.Text == _searchPlaceholder || textBox.Text == "Type here to search..." || textBox.Text == "Nhập để tìm kiếm..."))
        {
            textBox.Text = string.Empty;
            textBox.Foreground = Helpers.ThemeBrushes.Get("TextBrush");
        }
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && string.IsNullOrWhiteSpace(textBox.Text))
        {
            textBox.Text = _searchPlaceholder;
            textBox.Foreground = Helpers.ThemeBrushes.Get("TextSecondaryBrush");
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            string query = SearchBox?.Text?.Trim() ?? "";
            if (query == _searchPlaceholder || query == "Type here to search..." || query == "Nhập để tìm kiếm...") 
                query = string.Empty;
            DownloadView?.FilterApps(query);
        }
        catch { }
    }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            this.DragMove();
    }

    public void ShowLoading(string message = "Loading...", int step = 1, int percentage = 0)
    {
        if (GlobalLoadingOverlay != null)
        {
             _isJobCancelled = false;
             GlobalLoadingOverlay.Visibility = Visibility.Visible;
             GlobalLoadingOverlay.UpdateProgress(step, percentage, message);
        }
    }

    public void HideLoading()
    {
        if (GlobalLoadingOverlay != null)
        {
             GlobalLoadingOverlay.Visibility = Visibility.Collapsed;
             GlobalLoadingOverlay.Reset();
        }
    }

    public void UpdateLoadingProgress(int step, int percentage, string taskName)
    {
        if (GlobalLoadingOverlay != null)
        {
             if (_isJobCancelled) throw new OperationCanceledException("Operation cancelled by user.");
             GlobalLoadingOverlay.UpdateProgress(step, percentage, taskName);
        }
    }

    public void UpdateDetailedProgress(int step, int overallPercentage, int filePercentage, string taskName, string fileName, string transferSize, string speed)
    {
        if (GlobalLoadingOverlay != null)
        {
             if (_isJobCancelled) throw new OperationCanceledException("Operation cancelled by user.");
             GlobalLoadingOverlay.UpdateDetailedProgress(step, overallPercentage, filePercentage, taskName, fileName, transferSize, speed);
        }
    }
}
