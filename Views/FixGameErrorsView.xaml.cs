using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TXATOOL.Helpers;
using TXATOOL.Views.Components;

namespace TXATOOL.Views
{
    public partial class FixGameErrorsView : UserControl
    {
        private bool _isLoaded = false;

        public FixGameErrorsView()
        {
            InitializeComponent();
            Loaded += FixGameErrorsView_Loaded;
        }

        private async void FixGameErrorsView_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            await RefreshDiagnosticsAsync();
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

        private async Task RefreshDiagnosticsAsync()
        {
            try
            {
                txtSecureBootDetails.Text = "Đang kiểm tra trạng thái Secure Boot & TPM 2.0...";
                txtRuntimesStatus.Text = "Đang quét các thư viện Visual C++ & DirectX...";

                var (sbInfo, rtInfo, isTurbo) = await Task.Run(() =>
                {
                    var sb = GameDoctorHelper.CheckSecureBootAndTpm();
                    var rt = GameDoctorHelper.CheckGameRuntimes();
                    var turbo = GameDoctorHelper.IsGameTurboActive();
                    return (sb, rt, turbo);
                });

                txtSecureBootDetails.Text = sbInfo.summary;

                var greenBg = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DCFCE7"));
                var greenBorder = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#86EFAC"));
                var greenText = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#15803D"));

                var yellowBg = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEF3C7"));
                var yellowBorder = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B"));
                var yellowText = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B45309"));

                // 1. Trạng thái Secure Boot & TPM
                if (sbInfo.secureBoot && sbInfo.tpm)
                {
                    AntiCheatBadgeText.Text = "READY";
                    AntiCheatStatusBadge.Background = greenBg;
                    AntiCheatStatusBadge.BorderBrush = greenBorder;
                    AntiCheatBadgeText.Foreground = greenText;
                }
                else
                {
                    AntiCheatBadgeText.Text = "ATTENTION";
                    AntiCheatStatusBadge.Background = yellowBg;
                    AntiCheatStatusBadge.BorderBrush = yellowBorder;
                    AntiCheatBadgeText.Foreground = yellowText;
                }

                // 2. Trạng thái Game Turbo & Low Latency
                if (isTurbo)
                {
                    if (TurboBadgeText != null) TurboBadgeText.Text = "ĐÃ KÍCH HOẠT";
                    if (TurboStatusBadge != null)
                    {
                        TurboStatusBadge.Background = greenBg;
                        TurboStatusBadge.BorderBrush = greenBorder;
                        if (TurboBadgeText != null) TurboBadgeText.Foreground = greenText;
                    }
                    if (btnApplyTurbo != null)
                    {
                        btnApplyTurbo.Content = "✓ Game Turbo Đang Hoạt Động (Kích hoạt lại)";
                        btnApplyTurbo.Style = (Style)FindResource("ProToolBtnStyle");
                    }
                }
                else
                {
                    if (TurboBadgeText != null) TurboBadgeText.Text = "CHƯA BẬT";
                    if (TurboStatusBadge != null)
                    {
                        TurboStatusBadge.Background = yellowBg;
                        TurboStatusBadge.BorderBrush = yellowBorder;
                        if (TurboBadgeText != null) TurboBadgeText.Foreground = yellowText;
                    }
                    if (btnApplyTurbo != null)
                    {
                        btnApplyTurbo.Content = "Kích hoạt Game Turbo Ngay";
                        btnApplyTurbo.Style = (Style)FindResource("PrimaryActionBtnStyle");
                    }
                }

                // 3. Trạng thái Runtimes & DirectX
                if (rtInfo.missingItems.Count == 0)
                {
                    txtRuntimesStatus.Text = "✓ Hệ thống đã có đầy đủ Visual C++ Redistributable (2005-2022) và DirectX. Không phát hiện thành phần bị thiếu.";
                    if (RuntimesBadgeText != null) RuntimesBadgeText.Text = "READY";
                    if (RuntimesStatusBadge != null)
                    {
                        RuntimesStatusBadge.Background = greenBg;
                        RuntimesStatusBadge.BorderBrush = greenBorder;
                        if (RuntimesBadgeText != null) RuntimesBadgeText.Foreground = greenText;
                    }
                    if (btnInstallRuntimes != null)
                    {
                        btnInstallRuntimes.Content = "✓ Đã Đầy Đủ Runtimes (Cài đặt lại)";
                        btnInstallRuntimes.Style = (Style)FindResource("ProToolBtnStyle");
                    }
                }
                else
                {
                    txtRuntimesStatus.Text = $"! Phát hiện {rtInfo.missingItems.Count} thành phần runtime bị thiếu:\n• {string.Join("\n• ", rtInfo.missingItems)}";
                    if (RuntimesBadgeText != null) RuntimesBadgeText.Text = "CẦN CÀI ĐẶT";
                    if (RuntimesStatusBadge != null)
                    {
                        RuntimesStatusBadge.Background = yellowBg;
                        RuntimesStatusBadge.BorderBrush = yellowBorder;
                        if (RuntimesBadgeText != null) RuntimesBadgeText.Foreground = yellowText;
                    }
                    if (btnInstallRuntimes != null)
                    {
                        btnInstallRuntimes.Content = "Tải & Cài Đặt Visual C++ AIO + DirectX";
                        btnInstallRuntimes.Style = (Style)FindResource("PrimaryActionBtnStyle");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Lỗi kiểm tra chẩn đoán: {ex.Message}");
            }
        }

        private void GameSelectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || GameSelectCombo == null) return;
            if (GameSelectCombo.SelectedItem is ComboBoxItem item)
            {
                string title = item.Content.ToString() ?? "";
                AppendLog($"Đã chọn cấu hình cho tựa game: {title}");

                if (title.Contains("Delta Force"))
                {
                    AppendLog("→ Gợi ý Delta Force: Thêm launch options '-dx11 -useallavailablecores' và kích hoạt Game Turbo.");
                }
                else if (title.Contains("Valorant"))
                {
                    AppendLog("→ Gợi ý Valorant: Yêu cầu Secure Boot & TPM 2.0 phải BẬT để tránh lỗi VAN 9003.");
                }
                else if (title.Contains("CS2"))
                {
                    AppendLog("→ Gợi ý CS2: Tắt gia tốc chuột (Raw Input) và chỉnh Timer Resolution xuống 0.5ms để giảm input lag.");
                }
            }
        }

        private MainWindow? GetMainWindow()
        {
            return (Window.GetWindow(this) as MainWindow) ?? (System.Windows.Application.Current.MainWindow as MainWindow);
        }

        private async void BtnApplyTurbo_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = GetMainWindow();
            btnApplyTurbo.IsEnabled = false;
            AppendLog("=== BẮT ĐẦU KÍCH HOẠT GAME TURBO & LOW LATENCY ===");

            try
            {
                mainWindow?.ShowLoading("Đang kích hoạt Game Turbo & Low Latency...", 1, 30);
                var logs = await GameDoctorHelper.ApplyGameTurboLatencyTweaksAsync();
                mainWindow?.UpdateLoadingProgress(1, 85, "Đang hoàn tất thiết lập hệ thống...");
                foreach (var log in logs)
                {
                    AppendLog(log);
                }

                mainWindow?.UpdateLoadingProgress(1, 100, "Hoàn tất!");
                AppendLog("=== HOÀN TẤT KÍCH HOẠT GAME TURBO ===");
                mainWindow?.HideLoading();
                CustomMessageBoxWindow.Show(
                    "Đã kích hoạt thành công chế độ Game Turbo!\n\n• MMCSS Priority 100% Gaming\n• Network Throttling Disabled\n• TCP Gaming Low-Latency Enabled\n• Raw Mouse Input 1:1",
                    "Game Turbo");
                await RefreshDiagnosticsAsync();
            }
            catch (Exception ex)
            {
                AppendLog($"[!] Lỗi khi kích hoạt Game Turbo: {ex.Message}");
                mainWindow?.HideLoading();
                CustomMessageBoxWindow.Show($"Lỗi: {ex.Message}", "Lỗi");
            }
            finally
            {
                mainWindow?.HideLoading();
                btnApplyTurbo.IsEnabled = true;
            }
        }

        private async void BtnClearShaders_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = GetMainWindow();
            btnClearShaders.IsEnabled = false;
            AppendLog("=== ĐANG XÓA TOÀN BỘ SHADER CACHE (DIRECTX / NVIDIA / AMD) ===");

            try
            {
                mainWindow?.ShowLoading("Đang quét và dọn dẹp Shader Cache...", 1, 30);
                var result = await GameDoctorHelper.ClearAllShaderCachesAsync();
                mainWindow?.UpdateLoadingProgress(1, 90, "Đang hoàn tất giải phóng bộ nhớ...");
                foreach (var log in result.logs)
                {
                    AppendLog(log);
                }

                mainWindow?.UpdateLoadingProgress(1, 100, "Dọn dẹp hoàn tất!");
                mainWindow?.HideLoading();
                double mb = result.bytesFreed / (1024.0 * 1024.0);
                CustomMessageBoxWindow.Show(
                    $"Đã dọn sạch {result.filesDeleted} file Shader Cache (~{mb:F1} MB).\n\nShader mới sẽ được biên dịch lại sạch sẽ khi bạn mở game, giúp triệt tiêu hiện tượng giật khựng khung hình.",
                    "Dọn Shader Cache");
            }
            catch (Exception ex)
            {
                AppendLog($"[!] Lỗi xóa Shader Cache: {ex.Message}");
                mainWindow?.HideLoading();
            }
            finally
            {
                mainWindow?.HideLoading();
                btnClearShaders.IsEnabled = true;
            }
        }

        private async void BtnRestartGpu_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = GetMainWindow();
            btnRestartGpu.IsEnabled = false;
            AppendLog("=== ĐANG RESET DỊCH VỤ HIỂN THỊ DWM & DRIVER VGA ===");

            try
            {
                mainWindow?.ShowLoading("Đang reset Driver màn hình & dịch vụ DWM...", 1, 50);
                var logs = await GameDoctorHelper.ResetGpuDriverPipelineAsync();
                foreach (var log in logs)
                {
                    AppendLog(log);
                }
                mainWindow?.UpdateLoadingProgress(1, 100, "Đã khởi động lại dịch vụ hiển thị!");
                mainWindow?.HideLoading();
            }
            catch (Exception ex)
            {
                AppendLog($"[!] Lỗi: {ex.Message}");
                mainWindow?.HideLoading();
            }
            finally
            {
                mainWindow?.HideLoading();
                btnRestartGpu.IsEnabled = true;
            }
        }

        private async void BtnCheckSecureBoot_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Đang kiểm tra lại trạng thái Secure Boot và TPM 2.0...");
            await RefreshDiagnosticsAsync();
            AppendLog("Đã cập nhật trạng thái chẩn đoán hệ thống.");
        }

        private void BtnFixAntiCheatGuide_Click(object sender, RoutedEventArgs e)
        {
            string guide = "HƯỚNG DẪN KHẮC PHỤC LỖI VAN 9003 & ANTI-CHEAT:\n\n" +
                           "1. Khởi động lại máy tính và nhấn phím BIOS (thường là F2 hoặc Del).\n" +
                           "2. Chuyển chế độ Boot Mode từ CSM/Legacy sang UEFI.\n" +
                           "3. Tìm mục Security > Secure Boot > Chuyển sang Enabled.\n" +
                           "4. Tìm mục Trusted Computing / AMD fTPM / Intel PTT > Chuyển sang Enabled.\n" +
                           "5. Nhấn F10 để lưu và khởi động lại vào Windows.\n\n" +
                           "Sau khi làm xong, bấm nút 'Kiểm Tra Lại' ở đây để xác nhận!";

            CustomMessageBoxWindow.Show(guide, "Hướng Dẫn Sửa Lỗi Anti-Cheat");
        }

        private async void BtnInstallRuntimes_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = GetMainWindow();
            btnInstallRuntimes.IsEnabled = false;
            AppendLog("=== ĐANG TIẾN HÀNH CÀI ĐẶT & TỐI ƯU VISUAL C++ RUNTIME & DIRECTX ===");

            try
            {
                mainWindow?.ShowLoading("Đang chuẩn bị kiểm tra Runtimes...", 1, 5);

                var logs = await GameDoctorHelper.InstallOrUpdateGameRuntimesAsync(
                    (pct, msg) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            mainWindow?.UpdateLoadingProgress(1, pct, msg);
                        });
                    },
                    (logMsg) =>
                    {
                        AppendLog(logMsg);
                    });

                await RefreshDiagnosticsAsync();
                mainWindow?.HideLoading();

                CustomMessageBoxWindow.Show(
                    "Đã hoàn tất kiểm tra & tối ưu Visual C++ Runtimes và DirectX!\n\nHệ thống đã có đầy đủ thư viện đồ họa & runtime cần thiết cho các tựa game hiện đại.",
                    "Runtimes & DirectX");
            }
            catch (Exception ex)
            {
                AppendLog($"[!] Lỗi cài đặt runtimes: {ex.Message}");
                mainWindow?.HideLoading();
                CustomMessageBoxWindow.Show($"Lỗi: {ex.Message}", "Lỗi");
            }
            finally
            {
                mainWindow?.HideLoading();
                btnInstallRuntimes.IsEnabled = true;
            }
        }

        private void BtnTimerResolution_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Đang mở công cụ TimerResolution (0.5ms)...");
            bool ok = GameDoctorHelper.LaunchTool("TimerResolution.exe");
            if (!ok)
            {
                AppendLog("[!] Không tìm thấy TimerResolution.exe trong thư mục Tools.");
                CustomMessageBoxWindow.Show("Không tìm thấy TimerResolution.exe. Hãy kiểm tra thư mục Tools.", "Thông báo");
            }
            else
            {
                AppendLog("[OK] Đã mở TimerResolution.exe. Nhấn nút 'Maximum' trong phần mềm để đưa timer về 0.5ms!");
            }
        }

        private void BtnMsiUtility_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Đang mở MSI Utility v3 (Message Signaled Interrupts)...");
            bool ok = GameDoctorHelper.LaunchTool("MSI.exe", "", true);
            if (!ok)
            {
                AppendLog("[!] Không tìm thấy MSI.exe trong thư mục Tools.");
                CustomMessageBoxWindow.Show("Không tìm thấy MSI.exe. Hãy kiểm tra thư mục Tools.", "Thông báo");
            }
            else
            {
                AppendLog("[OK] Đã mở MSI.exe quyền Administrator. Tích chọn card GPU của bạn và chọn Interrupt Priority = High rồi Apply!");
            }
        }

        private void BtnNvInspector_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Đang mở NVIDIA Profile Inspector...");
            bool ok = GameDoctorHelper.LaunchTool("Inspector.exe");
            if (!ok)
            {
                AppendLog("[!] Không tìm thấy Inspector.exe trong thư mục Tools.");
                CustomMessageBoxWindow.Show("Không tìm thấy Inspector.exe trong thư mục Tools.", "Thông báo");
            }
            else
            {
                AppendLog("[OK] Đã mở NVIDIA Profile Inspector.");
            }
        }

        private void BtnRamMap_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Đang mở Sysinternals RAMMap...");
            bool ok = GameDoctorHelper.LaunchTool("RAMMap64.exe", "", true);
            if (!ok)
            {
                AppendLog("[!] Không tìm thấy RAMMap64.exe.");
                CustomMessageBoxWindow.Show("Không tìm thấy RAMMap64.exe trong thư mục Tools.", "Thông báo");
            }
            else
            {
                AppendLog("[OK] Đã mở Sysinternals RAMMap quyền Administrator.");
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            TxtLogConsole.Clear();
        }
    }
}
