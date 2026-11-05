using System.Windows;

namespace TXATOOL.Views
{
    public partial class USBBootProgressWindow : Window
    {
        public USBBootProgressWindow()
        {
            InitializeComponent();
        }

        public void UpdateStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = status;
            });
        }
    }
}
