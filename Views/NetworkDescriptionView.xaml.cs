using System;
using System.Windows;
using System.Windows.Controls;
using TXATOOL.Helpers;

namespace TXATOOL.Views
{
    public partial class NetworkDescriptionView : UserControl
    {
        public NetworkDescriptionView()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdateLanguage();
            LocalizationService.LanguageChanged += UpdateLanguage;
        }

        private void UpdateLanguage()
        {
            bool isVn = string.Equals(LocalizationService.CurrentLanguage, "vi", StringComparison.OrdinalIgnoreCase);
            if (VietnamesePanel != null) VietnamesePanel.Visibility = isVn ? Visibility.Visible : Visibility.Collapsed;
            if (EnglishPanel != null) EnglishPanel.Visibility = isVn ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
