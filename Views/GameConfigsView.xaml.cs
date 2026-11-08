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
                if (text.Contains("EA SPORTS FC") || text.Contains("FC 26") || text.Contains("FC 25") || text.Contains("FC 24")) return "EA SPORTS FC";
                if (text.Contains("FIFA 23")) return "FIFA 23";
                if (text.Contains("FIFA 19") || text.Contains("FC 19")) return "FIFA 19";
                if (text.Contains("eFootball") || text.Contains("PES")) return "eFootball / PES";
                if (text.Contains("Delta Force")) return "Delta Force";
                if (text.Contains("Counter-Strike 2") || text.Contains("CS2")) return "CS2";
                if (text.Contains("PUBG")) return "PUBG";
                if (text.Contains("Battlefield 2042")) return "Battlefield 2042";
                if (text.Contains("Battlefield V")) return "Battlefield V";
                if (text.Contains("Battlefield 1")) return "Battlefield 1";
            }
            return "EA SPORTS FC";
        }

        private void UpdatePresetDetails()
        {
            if (TxtPresetDetails == null) return;
            string game = GetSelectedGameName();
            switch (game)
            {
                case "EA SPORTS FC":
                    TxtPresetDetails.Text =
                        "• Tối ưu fcsetup.ini chuẩn thi đấu Esport cho EA SPORTS FC 26 / 25 / 24:\n" +
                        "• Tắt STRAND_BASED_HAIR = 0 (khắc phục triệt để lỗi tụt sụt 50% FPS khi ăn mừng bàn thắng & cận cảnh cầu thủ).\n" +
                        "• Tắt Dynamic Resolution (DYNAMIC_RESOLUTION = 0) giữ hình ảnh 1080p sắc nét, không bị mờ nhòe khi chuyền dài.\n" +
                        "• Tắt V-Sync (WAITFORVSYNC = 0) và Fullscreen để triệt tiêu độ trễ input của tay cầm và bàn phím khi rê dắt sút bóng.\n" +
                        "• Mở khóa CONFIG_APP_MAX_FRAME_RATE = 0 và tự động gán High Performance GPU cho game.";
                    break;

                case "FIFA 23":
                    TxtPresetDetails.Text =
                        "• Tối ưu fifasetup.ini cho FIFA 23:\n" +
                        "• Tắt STRAND_BASED_HAIR = 0 và DYNAMIC_RESOLUTION = 0 chống tụt khung hình đột ngột.\n" +
                        "• Tắt WAITFORVSYNC = 0, đưa RENDERINGQUALITY = 1 (Tối ưu) và tắt MSAA để duy trì 144+ FPS ổn định.\n" +
                        "• Tự động đăng ký Windows High Performance GPU Preference cho FIFA23.exe.";
                    break;

                case "FIFA 19":
                    TxtPresetDetails.Text =
                        "• Tối ưu fifasetup.ini cho FIFA 19 (FC 19):\n" +
                        "• Mở khóa tốc độ khung hình (CONFIG_APP_MAX_FRAME_RATE = 0), tắt WAITFORVSYNC = 0 loại bỏ delay chuyền bóng.\n" +
                        "• Thiết lập Fullscreen 1080p tối ưu, tắt hiệu ứng khử răng cưa nặng và kích hoạt GPU tối đa cho FIFA19.exe.";
                    break;

                case "eFootball / PES":
                    TxtPresetDetails.Text =
                        "• Tối ưu cấu hình cho eFootball (PES 2026 / 2024 / 2021):\n" +
                        "• Tối ưu Engine.ini Unreal Engine: Tắt Motion Blur, Depth of Field, Bloom, khử mờ viền hạt (SceneColorFringe).\n" +
                        "• Tối ưu Streaming.PoolSize lên 3072 MB và Shaders.Optimize = 1 để chống giật khựng khi camera lia nhanh trên sân.\n" +
                        "• Đối với PES cổ điển (PES 2021, PES 2019): Tự động tạo settings.ini Fullscreen chất lượng cao tắt VSync.";
                    break;

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
            if (!string.IsNullOrEmpty(detected) && Directory.Exists(detected))
            {
                GameLocationTextBox.Text = detected;
                AppendLog($"[✓] Đã phát hiện game '{game}' được cài đặt trên máy: {detected}");
            }
            else
            {
                GameLocationTextBox.Text = string.Empty;
                AppendLog($"[i] Không tìm thấy game '{game}' cài đặt trên máy tính của bạn. Bạn có thể bấm 'Duyệt...' nếu game nằm ở thư mục tùy chỉnh.");
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
            string game = GetSelectedGameName();

            if (string.IsNullOrEmpty(path))
            {
                CustomMessageBoxWindow.Show(
                    $"Không tìm thấy game '{game}' trên máy tính của bạn.\n\nVui lòng cài đặt game trước hoặc bấm nút 'Duyệt...' để chọn thư mục cấu hình nếu bạn đã cài ở vị trí khác.",
                    "Chưa Tìm Thấy Game");
                AppendLog($"[!] Hủy áp dụng cấu hình: Chưa phát hiện đường dẫn cài đặt của game {game}.");
                return;
            }

            if (!Directory.Exists(path))
            {
                CustomMessageBoxWindow.Show(
                    $"Thư mục '{path}' không tồn tại trên hệ thống.\n\nVui lòng kiểm tra lại xem máy tính đã cài đặt game '{game}' chưa.",
                    "Thư Mục Không Tồn Tại");
                AppendLog($"[!] Thư mục không tồn tại: {path}");
                return;
            }

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
                    case "EA SPORTS FC":
                        logs = await GameDoctorHelper.ApplyEaSportsFcOptimizedConfigAsync(path, readOnly);
                        break;

                    case "FIFA 23":
                        logs = await GameDoctorHelper.ApplyFifa23OptimizedConfigAsync(path, readOnly);
                        break;

                    case "FIFA 19":
                        logs = await GameDoctorHelper.ApplyFifa19OptimizedConfigAsync(path, readOnly);
                        break;

                    case "eFootball / PES":
                        logs = await GameDoctorHelper.ApplyEFootballPesOptimizedConfigAsync(path, readOnly);
                        break;

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
