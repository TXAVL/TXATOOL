using System.Windows;
using System.Windows.Media;

namespace TXATOOL.Helpers
{
    /// <summary>Resolves theme brushes (Themes/*.xaml) for code-behind at call time.</summary>
    public static class ThemeBrushes
    {
        public static Brush Get(string key, Brush? fallback = null) =>
            Application.Current?.TryFindResource(key) as Brush ?? fallback ?? Brushes.Black;
    }
}
