using System;
using System.Windows;
using System.Windows.Controls;
using TXATOOL.Helpers;

namespace TXATOOL.Views
{
    public partial class USBBootDescriptionView : UserControl
    {
        public USBBootDescriptionView()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdateLanguage();
            LocalizationService.LanguageChanged += UpdateLanguage;
        }

        private void UpdateLanguage()
        {
            bool isVn = LocalizationService.IsVietnamese;
            if (VietnamesePanel != null) VietnamesePanel.Visibility = isVn ? Visibility.Visible : Visibility.Collapsed;
            if (EnglishPanel != null) EnglishPanel.Visibility = isVn ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
