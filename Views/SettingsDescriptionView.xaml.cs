using System.Windows.Controls;
using TXATOOL.Helpers;

namespace TXATOOL.Views
{
    public partial class SettingsDescriptionView : UserControl
    {
        public SettingsDescriptionView()
        {
            InitializeComponent();
            this.Loaded += SettingsDescriptionView_Loaded;
            this.Unloaded += SettingsDescriptionView_Unloaded;
        }

        private void SettingsDescriptionView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ApplyTranslations();
            LocalizationService.LanguageChanged += ApplyTranslations;
        }

        private void SettingsDescriptionView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            LocalizationService.LanguageChanged -= ApplyTranslations;
        }

        private void ApplyTranslations()
        {
            if (lblDonationTitle != null) lblDonationTitle.Text = LocalizationService.Get("lblDonationTitle");
            if (lblDonationDesc != null) lblDonationDesc.Text = LocalizationService.Get("lblDonationDesc");
            if (lblDonationNote != null) lblDonationNote.Text = LocalizationService.Get("lblDonationNote") ?? "Nội dung chuyển khoản: DONATE UNG HO TXA";
            if (lblDonationThanks != null) lblDonationThanks.Text = LocalizationService.Get("lblDonationThanks");
            if (btnOpenDonateModal != null)
            {
                bool isVn = LocalizationService.CurrentLanguage?.ToLower().Contains("en") != true;
                btnOpenDonateModal.Content = isVn ? "Phóng to mã QR" : "Enlarge QR Code";
            }
        }

        private void QrImage_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenDonateModal();
        }

        private void OpenDonateModal_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            OpenDonateModal();
        }

        private void OpenDonateModal()
        {
            try
            {
                var win = new TXATOOL.Views.Components.DonateWindow();
                win.Owner = System.Windows.Application.Current.MainWindow;
                win.ShowDialog();
            }
            catch { }
        }
    }
}
