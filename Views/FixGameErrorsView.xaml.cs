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

                var (sbInfo, rtInfo) = await Task.Run(() =>
                {
                    var sb = GameDoctorHelper.CheckSecureBootAndTpm();
                    var rt = GameDoctorHelper.CheckGameRuntimes();
                    return (sb, rt);
                });

                txtSecureBootDetails.Text = sbInfo.summary;

                var greenBg = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DCFCE7"));
                var greenBorder = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#86EFAC"));
                var greenText = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#15803D"));

                var yellowBg = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEF3C7"));
                var yellowBorder = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B"));
                var yellowText = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#B45309"));

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

                if (rtInfo.missingItems.Count == 0)
                {
                    txtRuntimesStatus.Text = "✓ Hệ thống đã có đầy đủ Visual C++ Redistributable và DirectX. Không phát hiện file DLL bị thiếu.";
                }
                else
                {
                    txtRuntimesStatus.Text = $"! Phát hiện {rtInfo.missingItems.Count} thành phần runtime bị thiếu:\n• {string.Join("\n• ", rtInfo.missingItems)}";
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

        private async void BtnApplyTurbo_Click(object sender, RoutedEventArgs e)
        {
            btnApplyTurbo.IsEnabled = false;
            AppendLog("=== BẮT ĐẦU KÍCH HOẠT GAME TURBO & LOW LATENCY ===");

            try
            {
                var logs = await GameDoctorHelper.ApplyGameTurboLatencyTweaksAsync();
                foreach (var log in logs)
                {
                    AppendLog(log);
                }

                AppendLog("=== HOÀN TẤT KÍCH HOẠT GAME TURBO ===");
                CustomMessageBoxWindow.Show(
                    "Đã kích hoạt thành công chế độ Game Turbo!\n\n• MMCSS Priority 100% Gaming\n• Network Throttling Disabled\n• TCP Gaming Low-Latency Enabled\n• Raw Mouse Input 1:1",
                    "Game Turbo");
            }
            catch (Exception ex)
            {
                AppendLog($"[!] Lỗi khi kích hoạt Game Turbo: {ex.Message}");
                CustomMessageBoxWindow.Show($"Lỗi: {ex.Message}", "Lỗi");
            }
            finally
            {
                btnApplyTurbo.IsEnabled = true;
            }
        }

        private async void BtnClearShaders_Click(object sender, RoutedEventArgs e)
        {
            btnClearShaders.IsEnabled = false;
            AppendLog("=== ĐANG XÓA TOÀN BỘ SHADER CACHE (DIRECTX / NVIDIA / AMD) ===");

            try
            {
                var result = await GameDoctorHelper.ClearAllShaderCachesAsync();
                foreach (var log in result.logs)
                {
                    AppendLog(log);
                }

                double mb = result.bytesFreed / (1024.0 * 1024.0);
                CustomMessageBoxWindow.Show(
                    $"Đã dọn sạch {result.filesDeleted} file Shader Cache (~{mb:F1} MB).\n\nShader mới sẽ được biên dịch lại sạch sẽ khi bạn mở game, giúp triệt tiêu hiện tượng giật khựng khung hình.",
                    "Dọn Shader Cache");
            }
            catch (Exception ex)
            {
                AppendLog($"[!] Lỗi xóa Shader Cache: {ex.Message}");
            }
            finally
            {
                btnClearShaders.IsEnabled = true;
            }
        }

        private async void BtnRestartGpu_Click(object sender, RoutedEventArgs e)
        {
            btnRestartGpu.IsEnabled = false;
            AppendLog("=== ĐANG RESET DỊCH VỤ HIỂN THỊ DWM & DRIVER VGA ===");

            try
            {
                var logs = await GameDoctorHelper.ResetGpuDriverPipelineAsync();
                foreach (var log in logs)
                {
                    AppendLog(log);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[!] Lỗi: {ex.Message}");
            }
            finally
            {
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
            btnInstallRuntimes.IsEnabled = false;
            AppendLog("=== ĐANG TIẾN HÀNH CÀI ĐẶT VISUAL C++ RUNTIME & DIRECTX ===");

            try
            {
                AppendLog("Đang thực thi lệnh cài đặt Microsoft Visual C++ All-in-One qua winget...");
                string cmd = "winget install --id Microsoft.VCRedist.2015+.x64 -e --silent --accept-source-agreements --accept-package-agreements";
                var res = await Utilities.RunCommandAsync(cmd, true);
                AppendLog($"Kết quả cài đặt VC++ x64: Mã thoát {res.ExitCode}");

                string cmdX86 = "winget install --id Microsoft.VCRedist.2015+.x86 -e --silent --accept-source-agreements --accept-package-agreements";
                var resX86 = await Utilities.RunCommandAsync(cmdX86, true);
                AppendLog($"Kết quả cài đặt VC++ x86: Mã thoát {resX86.ExitCode}");

                await RefreshDiagnosticsAsync();
                CustomMessageBoxWindow.Show("Đã tiến hành cài đặt/cập nhật Visual C++ Runtimes mới nhất!", "Runtimes");
            }
            catch (Exception ex)
            {
                AppendLog($"[!] Lỗi cài đặt runtimes: {ex.Message}");
                CustomMessageBoxWindow.Show($"Lỗi: {ex.Message}", "Lỗi");
            }
            finally
            {
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
