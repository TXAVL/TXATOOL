using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using TXATOOL.Helpers;

namespace TXATOOL.Services
{
    public enum ThemeMode { System, Light, Dark }

    /// <summary>
    /// Swaps the active theme dictionary (Themes/Light.xaml or Themes/Dark.xaml) at runtime.
    /// All theme brushes are consumed through DynamicResource, so the UI updates live.
    /// </summary>
    public static class ThemeService
    {
        private const string ThemeDictionaryPrefix = "Themes/";
        private static ThemeMode _mode = ThemeMode.System;

        public static ThemeMode Mode => _mode;
        public static bool IsDark { get; private set; }
        public static event Action? ThemeChanged;

        public static void Initialize()
        {
            _mode = Parse(AppConfigManager.Config.Theme);
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (_mode == ThemeMode.System && e.Category == UserPreferenceCategory.General)
                    Application.Current?.Dispatcher.Invoke(Apply);
            };
            Apply();
        }

        public static void SetMode(ThemeMode mode)
        {
            _mode = mode;
            AppConfigManager.Config.Theme = mode.ToString();
            AppConfigManager.SaveConfig();
            Apply();
        }

        public static bool SystemUsesDark()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
            }
            catch { return false; }
        }

        private static ThemeMode Parse(string? s) =>
            Enum.TryParse<ThemeMode>(s, true, out var m) ? m : ThemeMode.System;

        private static void Apply()
        {
            var app = Application.Current;
            if (app == null) return;
            IsDark = _mode == ThemeMode.Dark || (_mode == ThemeMode.System && SystemUsesDark());

            var dicts = app.Resources.MergedDictionaries;
            var existing = dicts.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.StartsWith(ThemeDictionaryPrefix, StringComparison.OrdinalIgnoreCase));
            var next = new ResourceDictionary { Source = new Uri(ThemeDictionaryPrefix + (IsDark ? "Dark.xaml" : "Light.xaml"), UriKind.Relative) };
            if (existing != null) dicts.Remove(existing);
            dicts.Insert(0, next);
            ThemeChanged?.Invoke();
        }

        /// <summary>Resolve a theme colour for code-behind use. Returns a fallback when the key is missing.</summary>
        public static System.Windows.Media.Color GetColor(string key, System.Windows.Media.Color fallback)
        {
            if (Application.Current?.TryFindResource(key) is System.Windows.Media.Color c) return c;
            if (Application.Current?.TryFindResource(key) is System.Windows.Media.SolidColorBrush b) return b.Color;
            return fallback;
        }
    }
}
