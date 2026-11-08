using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using TXATOOL.Helpers;

namespace TXATOOL.Views
{
    public partial class SettingsView : UserControl
    {
        private bool _isInitializing = true;

        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
            LocalizationService.LanguageChanged += ApplyTranslations;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Set ComboBox to match current language (without triggering change event)
            _isInitializing = true;
            string currentLang = LocalizationService.CurrentLanguage;
            for (int i = 0; i < LanguageComboBox.Items.Count; i++)
            {
                if (LanguageComboBox.Items[i] is ComboBoxItem item && item.Tag?.ToString() == currentLang)
                {
                    LanguageComboBox.SelectedIndex = i;
                    break;
                }
            }
            string themeTag = TXATOOL.Services.ThemeService.Mode.ToString();
            for (int i = 0; i < ThemeComboBox.Items.Count; i++)
            {
                if (ThemeComboBox.Items[i] is ComboBoxItem ti && ti.Tag?.ToString() == themeTag)
                {
                    ThemeComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (DownloadSourceComboBox != null)
            {
                bool isTxaInstalled = Services.TxaRepoService.IsInstalled();
                if (AppConfigManager.Config.DownloadSource == "txa-repo" && isTxaInstalled)
                {
                    DownloadSourceComboBox.SelectedIndex = 1;
                }
                else
                {
                    DownloadSourceComboBox.SelectedIndex = 0;
                }
            }
            _isInitializing = false;

            ApplyTranslations();
            LoadCurrentVersion();
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (LanguageComboBox.SelectedItem is ComboBoxItem selected && selected.Tag is string langCode)
            {
                LocalizationService.SaveLanguagePreference(langCode);
                LocalizationService.LoadLanguage(langCode);
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (ThemeComboBox.SelectedItem is ComboBoxItem sel && sel.Tag is string tag
                && System.Enum.TryParse<TXATOOL.Services.ThemeMode>(tag, out var mode))
            {
                TXATOOL.Services.ThemeService.SetMode(mode);
            }
        }

        private void DownloadSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            if (DownloadSourceComboBox.SelectedItem is ComboBoxItem selected && selected.Tag is string tag)
            {
                if (tag == "txa-repo")
                {
                    if (!Services.TxaRepoService.IsInstalled())
                    {
                        var isVn = (LocalizationService.CurrentLanguage ?? "en").ToLower().Contains("vn");
                        TXATOOL.Views.Components.CustomMessageBoxWindow.Show(
                            isVn 
                                ? "Kho TXA (txa-repo) chưa được cài đặt trên máy tính của bạn.\n\nVui lòng cài đặt TXA Package Manager (txapm) qua mục 'Mở rộng tính năng' trước khi chọn nguồn này."
                                : "TXA Store (txa-repo) is not installed on this system.\n\nPlease install TXA Package Manager (txapm) via 'Extend Features' before selecting this source.",
                            isVn ? "Thông báo" : "Notice"
                        );
                        _isInitializing = true;
                        DownloadSourceComboBox.SelectedIndex = 0;
                        _isInitializing = false;
                        AppConfigManager.Config.DownloadSource = "default";
                        AppConfigManager.SaveConfig();
                        return;
                    }

                    AppConfigManager.Config.DownloadSource = "txa-repo";
                    AppConfigManager.SaveConfig();
                }
                else
                {
                    AppConfigManager.Config.DownloadSource = "default";
                    AppConfigManager.SaveConfig();
                }
            }
        }

        private void ApplyTranslations()
        {
            lblChooseTheme.Text = LocalizationService.Get("lblChooseTheme") ?? "Appearance";
            lblThemeSubtitle.Text = LocalizationService.Get("lblThemeSubtitle") ?? "Match Windows, or always use light or dark";
            themeItemSystem.Content = LocalizationService.Get("themeSystem") ?? "Follow system";
            themeItemLight.Content = LocalizationService.Get("themeLight") ?? "Light";
            themeItemDark.Content = LocalizationService.Get("themeDark") ?? "Dark";
            // Use the visual tree translator for named elements
            LocalizationService.TranslateUI(this);
            
            // Explicitly set dynamic/code-behind text if needed
            var isVn = (LocalizationService.CurrentLanguage ?? "en").ToLower().Contains("vn");
            if (lblDownloadSourceTitle != null)
                lblDownloadSourceTitle.Text = LocalizationService.Get("lblDownloadSourceTitle") ?? (isVn ? "Nguồn kho tải xuống" : "Download Repository Source");
            if (lblDownloadSourceSubtitle != null)
                lblDownloadSourceSubtitle.Text = LocalizationService.Get("lblDownloadSourceSubtitle") ?? (isVn ? "Chọn kho mặc định (Winget / Choco) hoặc Kho phần mềm TXA (txa-repo)" : "Choose default (Winget / Choco) or TXA Software Store (txa-repo)");
            if (sourceItemDefault != null)
                sourceItemDefault.Content = LocalizationService.Get("sourceItemDefault") ?? (isVn ? "Mặc định (Winget & Choco)" : "Default (Winget & Choco)");
            if (sourceItemTxaRepo != null)
                sourceItemTxaRepo.Content = LocalizationService.Get("sourceItemTxaRepo") ?? (isVn ? "Kho phần mềm TXA (txa-repo)" : "TXA Software Store (txa-repo)");

            lblCheckUpdateTitle.Text = LocalizationService.Get("lblCheckUpdateTitle") ?? "Check for updates";
            btnCheckUpdate.Content = LocalizationService.Get("btnCheckUpdateText") ?? "Check Update";
            if (lblBtnDonateVietQR != null) lblBtnDonateVietQR.Text = LocalizationService.Get("lblBtnDonateVietQR") ?? "Ủng hộ tác giả (VietQR)";
            LoadCurrentVersion(); // Refresh version string translation
        }

        private void Developer_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://fb.com/vlog.txa.2311");
        }

        private void DonateQr_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new TXATOOL.Views.Components.DonateWindow();
                win.Owner = Application.Current.MainWindow;
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening DonateWindow: {ex.Message}");
            }
        }

        private void FacebookLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenUrl("https://fb.com/vlog.txa.2311");
        }

        private void GithubLink_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenUrl("https://fb.com/vlog.txa.2311");
        }

        private void LoadCurrentVersion()
        {
            try
            {
                var assemblyVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string verStr = (assemblyVer != null && assemblyVer.Major > 0)
                    ? $"v{assemblyVer.Major}.{assemblyVer.Minor}.{assemblyVer.Build}"
                    : "v1.2.1";

                var infoLabel = LocalizationService.Get("lblVersionInfo") ?? "THÔNG TIN PHIÊN BẢN";
                lblCurrentVersion.Text = $"{infoLabel} {verStr}";

                var isVn = (LocalizationService.CurrentLanguage ?? "en").ToLower().Contains("vn");
                if (lblVersion != null)
                {
                    lblVersion.Text = isVn 
                        ? $"Phiên bản TXATOOL - WIN: {verStr} (Bản chính thức)" 
                        : $"TXATOOL - WIN Version: {verStr} (Official Release)";
                }
                if (lblFirstBuild != null)
                {
                    lblFirstBuild.Text = isVn 
                        ? $"Bản dựng: {verStr.Replace("v", "")} (Build 2026.11.08)" 
                        : $"Build: {verStr.Replace("v", "")} (Build 2026.11.08)";
                }
                if (lblReleaseDate != null)
                {
                    lblReleaseDate.Text = isVn ? "Ngày phát hành: 08/11/2026" : "Release Date: November 08, 2026";
                }
            }
            catch
            {
                lblCurrentVersion.Text = "THÔNG TIN PHIÊN BẢN v1.2.1";
            }
        }

        private async void btnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnCheckUpdate.IsEnabled = false;
                var updateService = new TXATOOL.Services.UpdateService();
                var updateInfo = await updateService.CheckForUpdateAsync();

                if (updateInfo != null)
                {
                    var updateWindow = new TXATOOL.Views.Components.UpdateWindow(updateInfo, updateService);
                    updateWindow.ShowDialog();
                }
                else
                {
                    TXATOOL.Views.Components.CustomMessageBoxWindow.Show(
                        LocalizationService.Get("msgUpdateLatest") ?? "You're on the latest version.",
                        "Check Update"
                    );
                }
            }
            finally
            {
                btnCheckUpdate.IsEnabled = true;
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
