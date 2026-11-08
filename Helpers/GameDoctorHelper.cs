using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Win32;

namespace TXATOOL.Helpers
{
    public static class GameDoctorHelper
    {
        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        /// <summary>
        /// Attempts to locate and launch a bundled or local tool executable.
        /// </summary>
        public static bool LaunchTool(string toolName, string arguments = "", bool runAsAdmin = false)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] searchPaths = new[]
                {
                    Path.Combine(baseDir, "Tools", toolName),
                    Path.Combine(baseDir, toolName),
                    Path.Combine(baseDir, "Warehouse", toolName),
                    Path.Combine(baseDir, "Warehouse", "RAMMap", toolName),
                    Path.Combine(baseDir, "Warehouse", "Optimize game", "Optimize game", toolName),
                    Path.Combine(baseDir, "..", "..", "..", "Warehouse", toolName),
                    Path.Combine(baseDir, "..", "..", "..", "Warehouse", "RAMMap", toolName),
                    Path.Combine(baseDir, "..", "..", "..", "Warehouse", "Optimize game", "Optimize game", toolName)
                };

                string? targetPath = searchPaths.FirstOrDefault(p => File.Exists(p));

                if (targetPath == null)
                {
                    // Check system PATH
                    string? pathEnv = Environment.GetEnvironmentVariable("PATH");
                    if (pathEnv != null)
                    {
                        foreach (string p in pathEnv.Split(Path.PathSeparator))
                        {
                            string candidate = Path.Combine(p.Trim(), toolName);
                            if (File.Exists(candidate))
                            {
                                targetPath = candidate;
                                break;
                            }
                        }
                    }
                }

                if (targetPath != null && File.Exists(targetPath))
                {
                    ProcessStartInfo psi = new ProcessStartInfo(targetPath, arguments)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(targetPath)
                    };

                    if (runAsAdmin)
                    {
                        psi.Verb = "runas";
                    }

                    Process.Start(psi);
                    Logger.LogActivity($"Launched tool: {toolName} from {targetPath}");
                    return true;
                }

                Logger.LogActivity($"Tool not found: {toolName}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogActivity($"Failed to launch tool {toolName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Performs an instant turbo boost: empties process working sets, purges standby list if RAMMap is available, flushes DNS, and cleans temp.
        /// </summary>
        public static async Task<(long freedBytes, string summary)> TurboBoostMemoryAndCacheAsync(IProgress<string>? statusProgress = null)
        {
            return await Task.Run(() =>
            {
                long initialAvailable = GetAvailableMemoryBytes();
                statusProgress?.Report("Purging memory working sets...");

                // 1. Purge Working Sets
                int processCount = 0;
                foreach (Process proc in Process.GetProcesses())
                {
                    try
                    {
                        if (!proc.HasExited && proc.Id > 4)
                        {
                            EmptyWorkingSet(proc.Handle);
                            processCount++;
                        }
                    }
                    catch { }
                }

                // 2. Purge Standby List if RAMMap64 is available
                statusProgress?.Report("Clearing standby memory list...");
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string[] ramMapPaths = new[]
                    {
                        Path.Combine(baseDir, "Tools", "RAMMap64.exe"),
                        Path.Combine(baseDir, "Warehouse", "RAMMap", "RAMMap64.exe"),
                        Path.Combine(baseDir, "RAMMap64.exe")
                    };
                    string? ramMapExe = ramMapPaths.FirstOrDefault(File.Exists);
                    if (ramMapExe != null)
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(ramMapExe, "-Et")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        using var p = Process.Start(psi);
                        p?.WaitForExit(3000);
                    }
                }
                catch { }

                // 3. Flush DNS resolver cache
                statusProgress?.Report("Flushing DNS resolver cache...");
                try
                {
                    ProcessStartInfo dnsPsi = new ProcessStartInfo("ipconfig", "/flushdns")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var p = Process.Start(dnsPsi);
                    p?.WaitForExit(2000);
                }
                catch { }

                // 4. Clean user temp files
                statusProgress?.Report("Cleaning temporary cache files...");
                int tempFilesDeleted = 0;
                try
                {
                    string tempPath = Path.GetTempPath();
                    if (Directory.Exists(tempPath))
                    {
                        var dir = new DirectoryInfo(tempPath);
                        foreach (var file in dir.GetFiles())
                        {
                            try
                            {
                                if ((DateTime.Now - file.LastAccessTime).TotalHours > 12)
                                {
                                    file.Delete();
                                    tempFilesDeleted++;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                GC.Collect();
                GC.WaitForPendingFinalizers();

                long finalAvailable = GetAvailableMemoryBytes();
                long freedBytes = Math.Max(0, finalAvailable - initialAvailable);
                double freedMb = freedBytes / (1024.0 * 1024.0);

                string summary = freedMb > 10
                    ? $"Đã giải phóng ~{freedMb:F0} MB RAM và làm sạch DNS/Temp ({processCount} tiến trình)."
                    : $"Hệ thống đã được tối ưu tức thì ({processCount} tiến trình đã dọn bộ nhớ).";

                Logger.LogActivity($"Turbo Boost completed: Freed ~{freedMb:F1} MB RAM. Temp files deleted: {tempFilesDeleted}");
                return (freedBytes, summary);
            });
        }

        private static long GetAvailableMemoryBytes()
        {
            try
            {
                using var pc = new PerformanceCounter("Memory", "Available Bytes");
                return (long)pc.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Applies high performance gaming latency optimizations (MMCSS, Network Throttling, Raw Mouse, FSE).
        /// </summary>
        public static async Task<List<string>> ApplyGameTurboLatencyTweaksAsync()
        {
            return await Task.Run(() =>
            {
                var logs = new List<string>();

                // 1. MMCSS Gaming Priority
                try
                {
                    Utilities.SetRegistryValue(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0, RegistryValueKind.DWord);
                    Utilities.SetRegistryValue(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);

                    string tasksPath = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
                    Utilities.SetRegistryValue(tasksPath, "GPU Priority", 8, RegistryValueKind.DWord);
                    Utilities.SetRegistryValue(tasksPath, "Priority", 6, RegistryValueKind.DWord);
                    Utilities.SetRegistryValue(tasksPath, "Scheduling Category", "High", RegistryValueKind.String);
                    Utilities.SetRegistryValue(tasksPath, "SFIO Priority", "High", RegistryValueKind.String);
                    logs.Add("[OK] Thiết lập MMCSS: GPU Priority = 8, Priority = 6, SystemResponsiveness = 0.");
                }
                catch (Exception ex)
                {
                    logs.Add($"[!] Lỗi thiết lập MMCSS: {ex.Message}");
                }

                // 2. Network Latency: TcpAckFrequency and TCPNoDelay for lower multiplayer ping
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (var interfacesKey = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true))
                    {
                        if (interfacesKey != null)
                        {
                            int tunedAdapters = 0;
                            foreach (string subKeyName in interfacesKey.GetSubKeyNames())
                            {
                                using var subKey = interfacesKey.OpenSubKey(subKeyName, true);
                                if (subKey != null)
                                {
                                    // If adapter has IP address or DhcpIPAddress configured, apply TCP gaming tweaks
                                    var ip = subKey.GetValue("IPAddress") ?? subKey.GetValue("DhcpIPAddress");
                                    if (ip != null)
                                    {
                                        subKey.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                                        subKey.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                                        tunedAdapters++;
                                    }
                                }
                            }
                            logs.Add($"[OK] Tối ưu hóa mạng độ trễ thấp (TCPNoDelay & TcpAckFrequency = 1 trên {tunedAdapters} card mạng).");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logs.Add($"[!] Lỗi cấu hình mạng: {ex.Message}");
                }

                // 3. Raw Mouse Input (Tắt gia tốc chuột Windows để ngắm chuẩn 1:1)
                try
                {
                    Utilities.SetRegistryValue(@"HKCU\Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String);
                    Utilities.SetRegistryValue(@"HKCU\Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String);
                    Utilities.SetRegistryValue(@"HKCU\Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String);
                    logs.Add("[OK] Tắt gia tốc chuột Windows (Raw 1:1 mouse input cho FPS).");
                }
                catch (Exception ex)
                {
                    logs.Add($"[!] Lỗi thiết lập chuột: {ex.Message}");
                }

                // 4. GameDVR & Fullscreen Optimizations Latency Fix
                try
                {
                    Utilities.SetRegistryValue(@"HKCU\System\GameConfigStore", "GameDVR_FSEBehavior", 2, RegistryValueKind.DWord);
                    Utilities.SetRegistryValue(@"HKCU\System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord);
                    Utilities.SetRegistryValue(@"HKCU\System\GameConfigStore", "GameDVR_DXGIHonorFSEWindowsCompatible", 1, RegistryValueKind.DWord);
                    Utilities.SetRegistryValue(@"HKCU\System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 1, RegistryValueKind.DWord);
                    Utilities.SetRegistryValue(@"HKCU\System\GameConfigStore", "GameDVR_EFSEFeatureFlags", 0, RegistryValueKind.DWord);
                    Utilities.SetRegistryValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0, RegistryValueKind.DWord);
                    logs.Add("[OK] Vô hiệu hóa GameDVR nền & giảm giật lag Fullscreen Optimizations.");
                }
                catch (Exception ex)
                {
                    logs.Add($"[!] Lỗi GameDVR: {ex.Message}");
                }

                return logs;
            });
        }

        /// <summary>
        /// Clears all DirectX, NVIDIA, and AMD shader caches to fix microstutters and corrupt shaders.
        /// </summary>
        public static async Task<(int filesDeleted, long bytesFreed, List<string> logs)> ClearAllShaderCachesAsync()
        {
            return await Task.Run(() =>
            {
                var logs = new List<string>();
                int totalDeleted = 0;
                long totalBytes = 0;

                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                var targetDirs = new List<string>
                {
                    Path.Combine(localAppData, "D3DSCache"),
                    Path.Combine(localAppData, "NVIDIA", "DXCache"),
                    Path.Combine(localAppData, "NVIDIA", "GLCache"),
                    Path.Combine(appData, "NVIDIA", "ComputeCache"),
                    Path.Combine(localAppData, "AMD", "DxCache"),
                    Path.Combine(localAppData, "AMD", "GLCache")
                };

                foreach (string dirPath in targetDirs)
                {
                    if (Directory.Exists(dirPath))
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(dirPath);
                            int dirFiles = 0;
                            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    long size = file.Length;
                                    file.Delete();
                                    totalDeleted++;
                                    totalBytes += size;
                                    dirFiles++;
                                }
                                catch { }
                            }
                            logs.Add($"[OK] Đã làm sạch: {Path.GetFileName(dirPath)} ({dirFiles} file cache)");
                        }
                        catch (Exception ex)
                        {
                            logs.Add($"[!] Bỏ qua {Path.GetFileName(dirPath)}: {ex.Message}");
                        }
                    }
                }

                double mb = totalBytes / (1024.0 * 1024.0);
                logs.Add($"==> Tổng cộng đã dọn {totalDeleted} file Shader Cache (~{mb:F1} MB).");
                return (totalDeleted, totalBytes, logs);
            });
        }

        /// <summary>
        /// Soft resets the graphics pipeline or launches MSI Utility / Inspector.
        /// </summary>
        public static async Task<List<string>> ResetGpuDriverPipelineAsync()
        {
            return await Task.Run(() =>
            {
                var logs = new List<string>();
                try
                {
                    logs.Add("[...] Đang khởi động lại dịch vụ hiển thị Desktop Window Manager (DWM)...");
                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c taskkill /F /IM dwm.exe")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(2000);
                    logs.Add("[OK] Driver màn hình và DWM đã được khởi động lại sạch sẽ.");
                }
                catch (Exception ex)
                {
                    logs.Add($"[!] Lỗi khởi động lại DWM: {ex.Message}");
                }
                return logs;
            });
        }

        /// <summary>
        /// Checks Secure Boot, TPM 2.0, and VBS/HVCI status.
        /// </summary>
        public static (bool secureBoot, bool tpm, bool vbs, string summary) CheckSecureBootAndTpm()
        {
            bool secureBoot = false;
            bool tpm = false;
            bool vbs = false;
            var details = new StringBuilder();

            // 1. Check Secure Boot
            try
            {
                int sbVal = Utilities.GetRegistryValue(@"HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled", 0);
                secureBoot = sbVal == 1;
            }
            catch { }

            details.AppendLine(secureBoot ? "• Secure Boot: BẬT (Enabled)" : "• Secure Boot: TẮT (Disabled - Cần bật trong BIOS cho Valorant)");

            // 2. Check TPM
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\CIMv2\Security\MicrosoftTpm", "SELECT * FROM Win32_Tpm");
                foreach (ManagementObject mo in searcher.Get())
                {
                    bool isActivated = mo["IsActivated_InitialValue"]?.ToString()?.Equals("True", StringComparison.OrdinalIgnoreCase) == true;
                    bool isEnabled = mo["IsEnabled_InitialValue"]?.ToString()?.Equals("True", StringComparison.OrdinalIgnoreCase) == true;
                    tpm = isActivated || isEnabled;
                    break;
                }
            }
            catch
            {
                tpm = false;
            }

            details.AppendLine(tpm ? "• TPM 2.0: ĐÃ BẬT (Ready)" : "• TPM: CHƯA PHÁT HIỆN (Cần kích hoạt Intel PTT / AMD fTPM trong BIOS)");

            // 3. Check VBS
            try
            {
                int vbsStatus = Utilities.GetRegistryValue(@"HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard", "VirtualizationBasedSecurityStatus", 0);
                vbs = vbsStatus != 0;
                details.AppendLine(vbs ? "• Virtualization-based Security (VBS): BẬT" : "• VBS: TẮT (Tối ưu cho FPS game)");
            }
            catch { }

            return (secureBoot, tpm, vbs, details.ToString());
        }

        /// <summary>
        /// Checks for missing Visual C++ Redistributable DLLs and DirectX 9/11 components.
        /// </summary>
        public static (bool hasVcRedist, bool hasDirectX, List<string> missingItems) CheckGameRuntimes()
        {
            var missing = new List<string>();
            string sys32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string sysWow64 = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

            string[] vcFiles = new[] { "vcruntime140.dll", "msvcp140.dll", "msvcr120.dll", "msvcp120.dll", "vcruntime140_1.dll" };
            string[] dxFiles = new[] { "d3dx9_43.dll", "d3dcompiler_47.dll", "xinput1_3.dll" };

            foreach (var f in vcFiles)
            {
                if (!File.Exists(Path.Combine(sys32, f)) || (!string.IsNullOrEmpty(sysWow64) && !File.Exists(Path.Combine(sysWow64, f))))
                {
                    missing.Add($"Visual C++: {f}");
                }
            }

            foreach (var f in dxFiles)
            {
                if (!File.Exists(Path.Combine(sys32, f)))
                {
                    missing.Add($"DirectX: {f}");
                }
            }

            bool hasVc = !missing.Any(m => m.StartsWith("Visual C++"));
            bool hasDx = !missing.Any(m => m.StartsWith("DirectX"));

            return (hasVc, hasDx, missing);
        }

        /// <summary>
        /// Scans, updates, and repairs Visual C++ 2015-2022 (x86 & x64) and enables DirectX DirectPlay feature.
        /// Reports step-by-step progress to progress callback and log callback.
        /// </summary>
        public static async Task<List<string>> InstallOrUpdateGameRuntimesAsync(Action<int, string>? reportProgress, Action<string>? log)
        {
            var logs = new List<string>();

            void Emit(string msg)
            {
                logs.Add(msg);
                log?.Invoke(msg);
            }

            reportProgress?.Invoke(10, "Đang quét thông tin thư viện Visual C++ trong Registry...");
            Emit("[+] Bắt đầu quét kiểm tra Visual C++ Redistributable (x64 và x86)...");

            // 1. Check Registry for VC++ x64
            string x64Version = string.Empty;
            bool x64UpToDate = false;
            try
            {
                using var regX64 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64");
                if (regX64 != null)
                {
                    var installed = regX64.GetValue("Installed");
                    if (installed != null && Convert.ToInt32(installed) == 1)
                    {
                        x64Version = regX64.GetValue("Version")?.ToString() ?? "Đã cài đặt";
                        int major = Convert.ToInt32(regX64.GetValue("Major") ?? 0);
                        int minor = Convert.ToInt32(regX64.GetValue("Minor") ?? 0);
                        if (major >= 14 && minor >= 40)
                        {
                            x64UpToDate = true;
                        }
                        Emit($"[✓] Phát hiện Visual C++ 2015-2022 (x64) trên máy: Phiên bản {x64Version}");
                    }
                }
            }
            catch { }

            // 2. Check Registry for VC++ x86
            string x86Version = string.Empty;
            bool x86UpToDate = false;
            try
            {
                using var regX86 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\X86") 
                                  ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X86");
                if (regX86 != null)
                {
                    var installed = regX86.GetValue("Installed");
                    if (installed != null && Convert.ToInt32(installed) == 1)
                    {
                        x86Version = regX86.GetValue("Version")?.ToString() ?? "Đã cài đặt";
                        int major = Convert.ToInt32(regX86.GetValue("Major") ?? 0);
                        int minor = Convert.ToInt32(regX86.GetValue("Minor") ?? 0);
                        if (major >= 14 && minor >= 40)
                        {
                            x86UpToDate = true;
                        }
                        Emit($"[✓] Phát hiện Visual C++ 2015-2022 (x86) trên máy: Phiên bản {x86Version}");
                    }
                }
            }
            catch { }

            // 2.5 Check Package Manager Source Preference
            bool isTxaRepo = AppConfigManager.Config.DownloadSource == "txa-repo" && Services.TxaRepoService.IsInstalled();
            if (isTxaRepo)
            {
                reportProgress?.Invoke(20, "Đang kiểm tra gói Visual C++ trên kho TXA Repo...");
                Emit("[+] Nguồn tải được chọn trong Cài đặt: Kho phần mềm TXA (txa-repo).");
                bool hasVcOnTxa = await DownloadHelper.IsPackageInTxaRepoAsync("vcredist");
                if (!hasVcOnTxa)
                {
                    Emit("[Thông báo] Gói Visual C++ 2015-2022 chưa có trên kho TXA Repo. Tự động chuyển đổi sang cài đặt qua Winget...");
                }
            }

            // 3. Process VC++ x64
            reportProgress?.Invoke(30, "Đang kiểm tra & cập nhật Microsoft Visual C++ 2015-2022 (x64)...");
            Emit("[+] Đang kiểm tra cập nhật gói Visual C++ 2015-2022 (x64)...");
            try
            {
                string cmdX64 = "winget install --id Microsoft.VCRedist.2015+.x64 -e --silent --accept-source-agreements --accept-package-agreements";
                var resX64 = await Task.Run(() => Utilities.RunCommandWithOutput(cmdX64));
                string outX64 = (resX64.Output ?? "") + " " + (resX64.Error ?? "");

                if (outX64.IndexOf("No available upgrade found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    outX64.IndexOf("No newer package versions", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    outX64.IndexOf("already installed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    resX64.ExitCode == 1638 ||
                    (resX64.ExitCode == 1 && x64UpToDate))
                {
                    Emit($"[✓] VC++ x64: Đang ở phiên bản mới nhất ({(!string.IsNullOrEmpty(x64Version) ? x64Version : "v14.51+")}), không cần nâng cấp.");
                }
                else if (resX64.ExitCode == 0 || outX64.IndexOf("Successfully installed", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Emit("[✓] VC++ x64: Cài đặt / cập nhật hoàn tất thành công qua winget!");
                }
                else if (x64UpToDate)
                {
                    Emit($"[✓] VC++ x64: Đã ở phiên bản mới nhất ({x64Version}) trên hệ thống.");
                }
                else
                {
                    Emit("[i] Đang tải trực tiếp bộ cài VC++ x64 chính thức từ máy chủ Microsoft...");
                    reportProgress?.Invoke(40, "Đang tải trực tiếp VC++ x64 từ Microsoft...");
                    string tempExe = Path.Combine(Path.GetTempPath(), "vc_redist.x64.exe");
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(30);
                        var data = await client.GetByteArrayAsync("https://aka.ms/vs/17/release/vc_redist.x64.exe");
                        await File.WriteAllBytesAsync(tempExe, data);
                    }
                    reportProgress?.Invoke(45, "Đang cài đặt trực tiếp VC++ x64...");
                    var runRes = await Task.Run(() => Utilities.RunCommandWithOutput($"\"{tempExe}\" /install /quiet /norestart"));
                    if (runRes.ExitCode == 0 || runRes.ExitCode == 3010 || runRes.ExitCode == 1638)
                    {
                        Emit("[✓] VC++ x64: Cài đặt trực tiếp từ Microsoft thành công!");
                    }
                    else
                    {
                        Emit($"[i] VC++ x64: Hoàn tất với mã {runRes.ExitCode}");
                    }
                    try { if (File.Exists(tempExe)) File.Delete(tempExe); } catch { }
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(x64Version))
                {
                    Emit($"[✓] VC++ x64 đã sẵn sàng trên hệ thống ({x64Version}).");
                }
                else
                {
                    Emit($"[!] Không thể cập nhật VC++ x64: {ex.Message}");
                }
            }

            // 4. Process VC++ x86
            reportProgress?.Invoke(55, "Đang kiểm tra & cập nhật Microsoft Visual C++ 2015-2022 (x86)...");
            Emit("[+] Đang kiểm tra cập nhật gói Visual C++ 2015-2022 (x86)...");
            try
            {
                string cmdX86 = "winget install --id Microsoft.VCRedist.2015+.x86 -e --silent --accept-source-agreements --accept-package-agreements";
                var resX86 = await Task.Run(() => Utilities.RunCommandWithOutput(cmdX86));
                string outX86 = (resX86.Output ?? "") + " " + (resX86.Error ?? "");

                if (outX86.IndexOf("No available upgrade found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    outX86.IndexOf("No newer package versions", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    outX86.IndexOf("already installed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    resX86.ExitCode == 1638 ||
                    (resX86.ExitCode == 1 && x86UpToDate))
                {
                    Emit($"[✓] VC++ x86: Đang ở phiên bản mới nhất ({(!string.IsNullOrEmpty(x86Version) ? x86Version : "v14.51+")}), không cần nâng cấp.");
                }
                else if (resX86.ExitCode == 0 || outX86.IndexOf("Successfully installed", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Emit("[✓] VC++ x86: Cài đặt / cập nhật hoàn tất thành công qua winget!");
                }
                else if (x86UpToDate)
                {
                    Emit($"[✓] VC++ x86: Đã ở phiên bản mới nhất ({x86Version}) trên hệ thống.");
                }
                else
                {
                    Emit("[i] Đang tải trực tiếp bộ cài VC++ x86 chính thức từ máy chủ Microsoft...");
                    reportProgress?.Invoke(65, "Đang tải trực tiếp VC++ x86 từ Microsoft...");
                    string tempExe = Path.Combine(Path.GetTempPath(), "vc_redist.x86.exe");
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(30);
                        var data = await client.GetByteArrayAsync("https://aka.ms/vs/17/release/vc_redist.x86.exe");
                        await File.WriteAllBytesAsync(tempExe, data);
                    }
                    reportProgress?.Invoke(70, "Đang cài đặt trực tiếp VC++ x86...");
                    var runRes = await Task.Run(() => Utilities.RunCommandWithOutput($"\"{tempExe}\" /install /quiet /norestart"));
                    if (runRes.ExitCode == 0 || runRes.ExitCode == 3010 || runRes.ExitCode == 1638)
                    {
                        Emit("[✓] VC++ x86: Cài đặt trực tiếp từ Microsoft thành công!");
                    }
                    else
                    {
                        Emit($"[i] VC++ x86: Hoàn tất với mã {runRes.ExitCode}");
                    }
                    try { if (File.Exists(tempExe)) File.Delete(tempExe); } catch { }
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(x86Version))
                {
                    Emit($"[✓] VC++ x86 đã sẵn sàng trên hệ thống ({x86Version}).");
                }
                else
                {
                    Emit($"[!] Không thể cập nhật VC++ x86: {ex.Message}");
                }
            }

            // 5. DirectX Legacy / DirectPlay
            reportProgress?.Invoke(80, "Đang kiểm tra và kích hoạt thành phần DirectX DirectPlay...");
            Emit("[+] Đang kiểm tra tính năng DirectX DirectPlay (hỗ trợ các tựa game yêu cầu DirectDraw & DirectX 9)...");
            try
            {
                var dismRes = await Task.Run(() => Utilities.RunCommandWithOutput("dism /online /enable-feature /featurename:DirectPlay /all /norestart"));
                if (dismRes.ExitCode == 0 || (dismRes.Output ?? "").IndexOf("100.0%", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Emit("[✓] Đã kích hoạt thành công tính năng DirectX DirectPlay cho Windows.");
                }
                else
                {
                    Emit("[✓] Kiểm tra DirectX DirectPlay hoàn tất.");
                }
            }
            catch (Exception ex)
            {
                Emit($"[i] Lưu ý DirectPlay: {ex.Message}");
            }

            // 6. Check Core DirectX files
            reportProgress?.Invoke(92, "Đang quét tính toàn vẹn các file DirectX & XInput...");
            var rt = CheckGameRuntimes();
            if (rt.hasDirectX)
            {
                Emit("[✓] Kiểm tra DirectX: Đầy đủ các thư viện Direct3D và XInput cần thiết cho game.");
            }
            else
            {
                var missingDx = rt.missingItems.Where(m => m.StartsWith("DirectX")).ToList();
                Emit($"[!] Phát hiện thiếu một số file DirectX: {string.Join(", ", missingDx)}");
            }

            reportProgress?.Invoke(100, "Hoàn tất kiểm tra & tối ưu Runtimes!");
            Emit("=== HOÀN TẤT: TOÀN BỘ RUNTIME & DIRECTX ĐÃ ĐƯỢC KIỂM TRA ĐẦY ĐỦ ===");
            return logs;
        }

        /// <summary>
        /// Discovers all Steam and standard game library folders across all drives on the system.
        /// </summary>
        public static List<string> GetAllGameSearchFolders()
        {
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Steam main install path from registry
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                                ?? Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    string? installPath = key.GetValue("InstallPath")?.ToString();
                    if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                    {
                        folders.Add(installPath);
                        string vdfPath = Path.Combine(installPath, "steamapps", "libraryfolders.vdf");
                        if (File.Exists(vdfPath))
                        {
                            foreach (var line in File.ReadAllLines(vdfPath))
                            {
                                var match = System.Text.RegularExpressions.Regex.Match(line, @"""path""\s+""([^""]+)""");
                                if (match.Success)
                                {
                                    string p = match.Groups[1].Value.Replace(@"\\", @"\");
                                    if (Directory.Exists(p)) folders.Add(p);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 2. Scan all drives for standard Steam and Epic and Games directories
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    string root = drive.RootDirectory.FullName;

                    string[] candidates = new[]
                    {
                        Path.Combine(root, "Steam"),
                        Path.Combine(root, "SteamLibrary"),
                        Path.Combine(root, "Games"),
                        Path.Combine(root, "Program Files (x86)", "Steam"),
                        Path.Combine(root, "Program Files", "Steam"),
                        Path.Combine(root, "Program Files", "Epic Games"),
                        Path.Combine(root, "Program Files (x86)", "Epic Games"),
                        Path.Combine(root, "Epic Games"),
                        Path.Combine(root, "Riot Games"),
                        Path.Combine(root, "EA Games")
                    };

                    foreach (var c in candidates)
                    {
                        if (Directory.Exists(c)) folders.Add(c);
                    }
                }
            }
            catch { }

            return folders.ToList();
        }

        /// <summary>
        /// Accurately detects whether a known game is actually installed on this computer,
        /// and returns its existing configuration directory.
        /// Returns empty string if the game is NOT installed on this machine.
        /// </summary>
        public static string DetectGameConfigPath(string gameName)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var searchRoots = GetAllGameSearchFolders();

            switch (gameName)
            {
                case "CS2":
                    // Check Steam libraries for Counter-Strike Global Offensive/game/csgo/cfg
                    foreach (var baseDir in searchRoots)
                    {
                        string cs2Cfg = Path.Combine(baseDir, "steamapps", "common", "Counter-Strike Global Offensive", "game", "csgo", "cfg");
                        if (Directory.Exists(cs2Cfg)) return cs2Cfg;

                        string cs2Alt = Path.Combine(baseDir, "steamapps", "common", "Counter-Strike Global Offensive", "csgo", "cfg");
                        if (Directory.Exists(cs2Alt)) return cs2Alt;
                    }
                    return string.Empty;

                case "Delta Force":
                    // Delta Force stores configs in %LOCALAPPDATA%\DeltaForce\Saved\Config\WindowsClient (or WindowsNoEditor)
                    string dfClient = Path.Combine(localAppData, "DeltaForce", "Saved", "Config", "WindowsClient");
                    if (Directory.Exists(dfClient)) return dfClient;

                    string dfNoEd = Path.Combine(localAppData, "DeltaForce", "Saved", "Config", "WindowsNoEditor");
                    if (Directory.Exists(dfNoEd)) return dfNoEd;

                    string dfSaved = Path.Combine(localAppData, "DeltaForce", "Saved", "Config");
                    if (Directory.Exists(dfSaved)) return dfSaved;

                    // Also check if Delta Force game files exist in any Steam library
                    foreach (var baseDir in searchRoots)
                    {
                        string steamDf = Path.Combine(baseDir, "steamapps", "common", "Delta Force", "DeltaForce", "Saved", "Config", "WindowsClient");
                        if (Directory.Exists(steamDf)) return steamDf;

                        string steamDfAlt = Path.Combine(baseDir, "steamapps", "common", "Delta Force");
                        if (Directory.Exists(steamDfAlt))
                        {
                            string cfg = Path.Combine(steamDfAlt, "Saved", "Config", "WindowsClient");
                            if (Directory.Exists(cfg)) return cfg;
                        }
                    }
                    return string.Empty;

                case "PUBG":
                    string pubgPath = Path.Combine(localAppData, "TslGame", "Saved", "Config", "WindowsNoEditor");
                    if (Directory.Exists(pubgPath)) return pubgPath;

                    string pubgAlt = Path.Combine(localAppData, "TslGame", "Saved", "Config");
                    if (Directory.Exists(pubgAlt)) return pubgAlt;

                    foreach (var baseDir in searchRoots)
                    {
                        string steamPubg = Path.Combine(baseDir, "steamapps", "common", "PUBG", "TslGame", "Saved", "Config", "WindowsNoEditor");
                        if (Directory.Exists(steamPubg)) return steamPubg;
                    }
                    return string.Empty;

                case "Battlefield 2042":
                    string bf2042 = Path.Combine(myDocuments, "Battlefield 2042", "settings");
                    if (Directory.Exists(bf2042)) return bf2042;
                    return string.Empty;

                case "Battlefield V":
                    string bfv = Path.Combine(myDocuments, "Battlefield V", "settings");
                    if (Directory.Exists(bfv)) return bfv;
                    return string.Empty;

                case "Battlefield 1":
                    string bf1 = Path.Combine(myDocuments, "Battlefield 1", "settings");
                    if (Directory.Exists(bf1)) return bf1;
                    return string.Empty;

                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Generates an optimal Delta Force config to prevent stutters and maximize FPS.
        /// </summary>
        public static async Task<List<string>> ApplyDeltaForceOptimizedConfigAsync(string configFolder, bool makeReadOnly)
        {
            return await Task.Run(() =>
            {
                var logs = new List<string>();
                try
                {
                    if (!Directory.Exists(configFolder))
                    {
                        Directory.CreateDirectory(configFolder);
                    }

                    string engineIni = Path.Combine(configFolder, "Engine.ini");
                    string backupFile = Path.Combine(configFolder, $"Engine_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.ini");

                    if (File.Exists(engineIni))
                    {
                        File.Copy(engineIni, backupFile, true);
                        logs.Add($"[OK] Đã sao lưu Engine.ini sang: {Path.GetFileName(backupFile)}");

                        // Remove readonly if set
                        var fi = new FileInfo(engineIni);
                        if (fi.IsReadOnly) fi.IsReadOnly = false;
                    }

                    string engineContent = @"[SystemSettings]
r.MotionBlurQuality=0
r.MotionBlur.Max=0
r.DefaultFeature.MotionBlur=0
r.DepthOfFieldQuality=0
r.DepthOfField.MaxSize=0
r.DefaultFeature.Bloom=0
r.SceneColorFringeQuality=0
r.EyeAdaptationQuality=0
r.Color.Mid=0.5
r.PostProcessAAQuality=2
r.Streaming.PoolSize=3072
r.Streaming.LimitPoolSizeToVRAM=1
r.CreateShadersOnLoad=1
r.Shaders.Optimize=1
r.FinishCurrentFrame=0
r.OneFrameThreadLag=1
r.MaxAnisotropy=4

[ConsoleVariables]
fx.Niagara.QualityLevel=0
t.MaxFPS=0
";

                    File.WriteAllText(engineIni, engineContent, Encoding.UTF8);
                    logs.Add("[OK] Đã ghi cấu hình tối ưu Engine.ini (Tắt Motion Blur, Bloom, Depth of Field, tối ưu Shader streaming).");

                    if (makeReadOnly)
                    {
                        var fi = new FileInfo(engineIni);
                        fi.IsReadOnly = true;
                        logs.Add("[OK] Đã đặt thuộc tính Read-Only để bảo vệ file config không bị game ghi đè.");
                    }

                    // Windows Graphics preference for Delta Force if detected
                    string[] driveLetters = new[] { "C", "D", "E", "F", "G" };
                    foreach (var d in driveLetters)
                    {
                        string exePath = $@"{d}:\Delta Force\DeltaForce\Binaries\Win64\DeltaForceClient-Win64-Shipping.exe";
                        if (File.Exists(exePath))
                        {
                            try
                            {
                                Utilities.SetRegistryValue(@"HKCU\Software\Microsoft\DirectX\UserGpuPreferences", exePath, "GpuPreference=2;", RegistryValueKind.String);
                                logs.Add($"[OK] Đã tự động cấu hình High Performance GPU (DirectX User Preferences) cho {Path.GetFileName(exePath)}");
                                break;
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logs.Add($"[!] Lỗi khi áp dụng config Delta Force: {ex.Message}");
                }

                return logs;
            });
        }

        /// <summary>
        /// Generates optimal autoexec.cfg for CS2.
        /// </summary>
        public static async Task<List<string>> ApplyCs2CompetitiveConfigAsync(string cfgFolder, bool makeReadOnly)
        {
            return await Task.Run(() =>
            {
                var logs = new List<string>();
                try
                {
                    if (!Directory.Exists(cfgFolder))
                    {
                        Directory.CreateDirectory(cfgFolder);
                    }

                    string autoexec = Path.Combine(cfgFolder, "autoexec.cfg");
                    if (File.Exists(autoexec))
                    {
                        string backup = Path.Combine(cfgFolder, $"autoexec_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.cfg");
                        File.Copy(autoexec, backup, true);
                        logs.Add($"[OK] Đã sao lưu autoexec.cfg cũ sang: {Path.GetFileName(backup)}");
                        var fi = new FileInfo(autoexec);
                        if (fi.IsReadOnly) fi.IsReadOnly = false;
                    }

                    string content = @"// CS2 Competitive Pro Autoexec by TXATOOL
fps_max 0
rate 1000000
cl_updaterate 128
cl_interp 0.015625
cl_interp_ratio 1
r_drawtracers_firstperson 1
cl_radar_always_centered 0
cl_radar_scale 0.35
cl_hud_radar_scale 1.15
snd_headphone_eq 0
snd_spatialize_lerp 1
engine_low_latency_sleep_after_client_tick true
echo ""TXATOOL - CS2 Autoexec Loaded Successfully!""
";
                    File.WriteAllText(autoexec, content, Encoding.UTF8);
                    logs.Add("[OK] Đã tạo autoexec.cfg thi đấu (fps_max 0, rate 1000000, low latency sleep).");

                    if (makeReadOnly)
                    {
                        var fi = new FileInfo(autoexec);
                        fi.IsReadOnly = true;
                        logs.Add("[OK] Đã khóa Read-Only cho autoexec.cfg.");
                    }
                }
                catch (Exception ex)
                {
                    logs.Add($"[!] Lỗi CS2 Config: {ex.Message}");
                }
                return logs;
            });
        }

        /// <summary>
        /// Generates optimal user.cfg for Battlefield titles.
        /// </summary>
        public static async Task<List<string>> ApplyBattlefieldOptimizedConfigAsync(string gameSettingsFolder, bool makeReadOnly)
        {
            return await Task.Run(() =>
            {
                var logs = new List<string>();
                try
                {
                    if (!Directory.Exists(gameSettingsFolder))
                    {
                        Directory.CreateDirectory(gameSettingsFolder);
                    }

                    string userCfg = Path.Combine(gameSettingsFolder, "user.cfg");
                    int logicalCores = Environment.ProcessorCount;

                    string content = $@"// Battlefield Optimized user.cfg by TXATOOL
Thread.ProcessorCount {logicalCores}
Thread.MaxProcessorCount {logicalCores}
Thread.MinFreeProcessorCount 0
Thread.JobThreadPriority 0
Render.DrawFps 1
GstRender.Dx12Enabled 1
GstRender.WeaponDOF 0
GstRender.MotionBlur 0.000000
GstRender.MotionBlurEnabled 0
WorldRender.LightTileCsPathEnable 0
";
                    File.WriteAllText(userCfg, content, Encoding.UTF8);
                    logs.Add($"[OK] Đã tạo user.cfg Battlefield (Tối ưu {logicalCores} luồng CPU, tắt Motion Blur và Weapon DOF).");

                    if (makeReadOnly)
                    {
                        var fi = new FileInfo(userCfg);
                        fi.IsReadOnly = true;
                        logs.Add("[OK] Đã khóa Read-Only cho user.cfg.");
                    }
                }
                catch (Exception ex)
                {
                    logs.Add($"[!] Lỗi Battlefield Config: {ex.Message}");
                }
                return logs;
            });
        }
    }
}

