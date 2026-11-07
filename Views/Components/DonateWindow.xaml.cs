using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TXATOOL.Helpers;

namespace TXATOOL.Views.Components
{
    public partial class DonateWindow : Window
    {
        public DonateWindow()
        {
            InitializeComponent();
            ApplyTranslations();
            LocalizationService.LanguageChanged += ApplyTranslations;
        }

        private void ApplyTranslations()
        {
            bool isVn = string.Equals(LocalizationService.CurrentLanguage, "vi", StringComparison.OrdinalIgnoreCase);
            if (lblDonateTitle != null) lblDonateTitle.Text = isVn ? "Ủng hộ tác giả TXA" : "Support Author TXA";
            if (lblDonateSubtitle != null) lblDonateSubtitle.Text = isVn 
                ? "Quét mã VietQR bằng ứng dụng ngân hàng hoặc ví điện tử bất kỳ" 
                : "Scan VietQR with any banking app or e-wallet";
            if (btnSaveQr != null) btnSaveQr.Content = isVn ? "Lưu ảnh QR" : "Save QR Image";
            if (btnClose != null) btnClose.Content = isVn ? "Đóng" : "Close";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnSaveQr_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    FileName = "TXA_VietQR_Donate.png",
                    Filter = "PNG Image (*.png)|*.png"
                };

                if (sfd.ShowDialog() == true)
                {
                    var uri = new Uri("pack://application:,,,/Images/qr_donate.png");
                    var streamInfo = Application.GetResourceStream(uri);
                    if (streamInfo != null)
                    {
                        using var fileStream = File.Create(sfd.FileName);
                        streamInfo.Stream.CopyTo(fileStream);
                        MessageBox.Show(this, 
                            LocalizationService.CurrentLanguage == "vi" ? "Đã lưu ảnh QR thành công!" : "QR code image saved successfully!", 
                            "TXATOOL", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

