using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TXATOOL.Helpers;
using TXATOOL.Views.Components;

namespace TXATOOL.Views
{
    public partial class GameConfigsView : UserControl
    {
        private bool _isLoaded = false;

        public GameConfigsView()
        {
            InitializeComponent();
            Loaded += GameConfigsView_Loaded;
        }

        private void GameConfigsView_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            UpdatePresetDetails();
            AutoDetectCurrentGamePath();
        }

        private void AppendLog(string message)
        {
            if (TxtLogConsole == null || LogScrollViewer == null) return;
            Dispatcher.Invoke(() =>
            {
                if (TxtLogConsole == null || LogScrollViewer == null) return;
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                TxtLogConsole.AppendText($"[{timestamp}] {message}\n");
                LogScrollViewer.ScrollToEnd();
            });
        }

        private string GetSelectedGameName()
        {
            if (GameSelectorCombo.SelectedItem is ComboBoxItem item)
            {
                string text = item.Content.ToString() ?? "";
                if (text.Contains("Delta Force")) return "Delta Force";
                if (text.Contains("Counter-Strike 2") || text.Contains("CS2")) return "CS2";
                if (text.Contains("PUBG")) return "PUBG";
                if (text.Contains("Battlefield 2042")) return "Battlefield 2042";
                if (text.Contains("Battlefield V")) return "Battlefield V";
                if (text.Contains("Battlefield 1")) return "Battlefield 1";
            }
            return "Delta Force";
        }

        private void UpdatePresetDetails()
        {
            if (TxtPresetDetails == null) return;
            string game = GetSelectedGameName();
            switch (game)
            {
                case "Delta Force":
                    TxtPresetDetails.Text =
                        "• Tối ưu Engine.ini: Vô hiệu hóa Motion Blur, Bloom, Depth of Field, khử mờ viền hạt (SceneColorFringe).\n" +
                        "• Tối ưu hóa Streaming.PoolSize lên 3072 MB, giới hạn theo dung lượng VRAM thực tế của card màn hình.\n" +
                        "• Thiết lập OneFrameThreadLag = 1 và Shaders.Optimize = 1 để triệt tiêu hiện tượng sụt drop FPS khi giao tranh.\n" +
                        "• Tự động đăng ký High Performance GPU cho file thực thi 'DeltaForceClient-Win64-Shipping.exe'.";
                    break;

                case "CS2":
                    TxtPresetDetails.Text =
                        "• Tạo file autoexec.cfg chuẩn thi đấu: rate 1000000, fps_max 0, cl_updaterate 128.\n" +
                        "• Bật engine_low_latency_sleep_after_client_tick để đồng bộ input chuột chính xác nhất sau mỗi tick mạng.\n" +
                        "• Tối ưu âm thanh tiếng bước chân rõ nét (snd_spatialize_lerp 1) và hiển thị radar bao quát toàn map.";
                    break;

                case "PUBG":
                    TxtPresetDetails.Text =
                        "• Tối ưu GameUserSettings.ini: Giảm độ trễ khung hình, tối ưu hóa foliage và khoảng cách hiển thị cỏ cây.\n" +
                        "• Khóa tốc độ khung hình phù hợp với tần số quét màn hình, tránh nóng card và lag đột ngột.";
                    break;

                case "Battlefield 2042":
                case "Battlefield V":
                case "Battlefield 1":
                    int cores = Environment.ProcessorCount;
                    TxtPresetDetails.Text =
                        $"• Tạo user.cfg tự động gán {cores} luồng xử lý CPU (Thread.ProcessorCount {cores}) tránh nghẽn CPU 100%.\n" +
                        "• Tắt hiệu ứng làm mờ vũ khí (WeaponDOF = 0), tắt Motion Blur và bật hiển thị FPS gốc trong game.";
                    break;
            }
        }

        private void AutoDetectCurrentGamePath()
        {
            if (GameLocationTextBox == null) return;
            string game = GetSelectedGameName();
            string detected = GameDoctorHelper.DetectGameConfigPath(game);
            if (!string.IsNullOrEmpty(detected))
            {
                GameLocationTextBox.Text = detected;
                AppendLog($"Tự động phát hiện đường dẫn cho {game}: {detected}");
            }
            else
            {
                AppendLog($"Chưa phát hiện đường dẫn mặc định cho {game}. Bạn có thể bấm 'Duyệt...' để chọn.");
            }
        }

        private void GameSelectorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || GameSelectorCombo == null) return;
            UpdatePresetDetails();
            AutoDetectCurrentGamePath();
        }

        private void BtnAutoDetectPath_Click(object sender, RoutedEventArgs e)
        {
            AutoDetectCurrentGamePath();
        }

        private void BrowseGameLocationBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Chọn thư mục cấu hình game",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                GameLocationTextBox.Text = dialog.FolderName;
                AppendLog($"Đã chọn thư mục: {dialog.FolderName}");
            }
        }

        private MainWindow? GetMainWindow()
        {
            return (Window.GetWindow(this) as MainWindow) ?? (System.Windows.Application.Current.MainWindow as MainWindow);
        }

        private async void BtnApplyConfig_Click(object sender, RoutedEventArgs e)
        {
            string path = GameLocationTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                CustomMessageBoxWindow.Show("Vui lòng nhập hoặc chọn thư mục lưu file cấu hình của game.", "Thông báo");
                return;
            }

            string game = GetSelectedGameName();
            bool readOnly = ChkReadOnly.IsChecked == true;
            var mainWindow = GetMainWindow();

            btnApplyConfig.IsEnabled = false;
            AppendLog($"=== BẮT ĐẦU ÁP DỤNG CẤU HÌNH TỐI ƯU CHO {game.ToUpper()} ===");

            try
            {
                mainWindow?.ShowLoading($"Đang áp dụng cấu hình tối ưu cho {game}...", 1, 40);
                List<string> logs = new List<string>();

                switch (game)
                {
                    case "Delta Force":
                        logs = await GameDoctorHelper.ApplyDeltaForceOptimizedConfigAsync(path, readOnly);
                        break;

                    case "CS2":
                        logs = await GameDoctorHelper.ApplyCs2CompetitiveConfigAsync(path, readOnly);
                        break;

                    case "Battlefield 2042":
                    case "Battlefield V":
                    case "Battlefield 1":
                        logs = await GameDoctorHelper.ApplyBattlefieldOptimizedConfigAsync(path, readOnly);
                        break;

                    default:
                        logs = await GameDoctorHelper.ApplyDeltaForceOptimizedConfigAsync(path, readOnly);
                        break;
                }

                mainWindow?.UpdateLoadingProgress(1, 90, "Đang hoàn tất lưu cấu hình...");
                foreach (var log in logs)
                {
                    AppendLog(log);
                }

                mainWindow?.UpdateLoadingProgress(1, 100, "Hoàn tất!");
                AppendLog("=== ÁP DỤNG CẤU HÌNH HOÀN TẤT ===");
                mainWindow?.HideLoading();
                CustomMessageBoxWindow.Show($"Đã áp dụng thành công cấu hình tối ưu cho {game}!", "Thành công");
            }
            catch (Exception ex)
            {
                AppendLog($"[!] Lỗi áp dụng cấu hình: {ex.Message}");
                mainWindow?.HideLoading();
                CustomMessageBoxWindow.Show($"Lỗi: {ex.Message}", "Lỗi");
            }
            finally
            {
                mainWindow?.HideLoading();
                btnApplyConfig.IsEnabled = true;
            }
        }

        private void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            string path = GameLocationTextBox.Text.Trim();
            if (!Directory.Exists(path))
            {
                CustomMessageBoxWindow.Show("Thư mục cấu hình không tồn tại.", "Thông báo");
                return;
            }

            try
            {
                var dir = new DirectoryInfo(path);
                var backups = dir.GetFiles("*_Backup_*.ini").Concat(dir.GetFiles("*_Backup_*.cfg"))
                                 .OrderByDescending(f => f.LastWriteTime)
                                 .ToList();

                if (backups.Count == 0)
                {
                    AppendLog("[!] Không tìm thấy file sao lưu (.bak) nào trong thư mục.");
                    CustomMessageBoxWindow.Show("Không tìm thấy bản sao lưu cũ nào trong thư mục này.", "Thông báo");
                    return;
                }

                var latestBackup = backups.First();
                string targetFileName = latestBackup.Name.Contains("Engine") ? "Engine.ini" :
                                        latestBackup.Name.Contains("autoexec") ? "autoexec.cfg" : "user.cfg";

                string targetFile = Path.Combine(path, targetFileName);

                if (File.Exists(targetFile))
                {
                    var fi = new FileInfo(targetFile);
                    if (fi.IsReadOnly) fi.IsReadOnly = false;
                }

                File.Copy(latestBackup.FullName, targetFile, true);
                AppendLog($"[OK] Đã khôi phục file cấu hình từ bản sao lưu: {latestBackup.Name}");
                CustomMessageBoxWindow.Show($"Đã khôi phục thành công cấu hình từ file {latestBackup.Name}!", "Khôi phục");
            }
            catch (Exception ex)
            {
                AppendLog($"[!] Lỗi khôi phục: {ex.Message}");
                CustomMessageBoxWindow.Show($"Lỗi: {ex.Message}", "Lỗi");
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = GameLocationTextBox.Text.Trim();
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            else
            {
                CustomMessageBoxWindow.Show("Thư mục này chưa tồn tại trên ổ đĩa.", "Thông báo");
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            TxtLogConsole.Clear();
        }
    }
}
