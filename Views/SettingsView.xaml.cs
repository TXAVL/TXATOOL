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
            lblCheckUpdateTitle.Text = LocalizationService.Get("lblCheckUpdateTitle") ?? "Check for updates";
            btnCheckUpdate.Content = LocalizationService.Get("btnCheckUpdateText") ?? "Check Update";
            LoadCurrentVersion(); // Refresh version string translation
        }

        private void Developer_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://fb.com/vlog.txa.2311");
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
                var lang = (LocalizationService.CurrentLanguage ?? "en").ToLower();
                string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Changelog", $"{lang}.json");
                
                // Fallback to en.json if language file doesn't exist
                if (!System.IO.File.Exists(path))
                {
                    path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Changelog", "en.json");
                }

                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    var allVersions = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, DashboardView.ChangelogData>>(json);
                    if (allVersions != null && allVersions.Count > 0)
                    {
                        var sortedKeys = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderByDescending(allVersions.Keys, k => k));
                        var latestData = allVersions[sortedKeys[0]];
                        lblCurrentVersion.Text = $"{LocalizationService.Get("lblVersionInfo") ?? "Current Version:"} {latestData.version_value}";
                    }
                }
            }
            catch { }
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
