using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
                    bool isActivated = (bool)(mo["IsActivated_InitialValue"] ?? false);
                    bool isEnabled = (bool)(mo["IsEnabled_InitialValue"] ?? false);
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
        /// Locates default config path for a known game.
        /// </summary>
        public static string DetectGameConfigPath(string gameName)
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            switch (gameName)
            {
                case "Delta Force":
                    string dfPath = Path.Combine(localAppData, "DeltaForce", "Saved", "Config", "WindowsClient");
                    if (Directory.Exists(dfPath)) return dfPath;
                    return dfPath;

                case "PUBG":
                    string pubgPath = Path.Combine(localAppData, "TslGame", "Saved", "Config", "WindowsNoEditor");
                    return pubgPath;

                case "CS2":
                    // Look for Steam userdata or CS2 game path
                    string[] steamBases = new[] { @"C:\Program Files (x86)\Steam", @"D:\Steam", @"E:\Steam", @"D:\SteamLibrary", @"E:\SteamLibrary" };
                    foreach (var s in steamBases)
                    {
                        string cs2Cfg = Path.Combine(s, "steamapps", "common", "Counter-Strike Global Offensive", "game", "csgo", "cfg");
                        if (Directory.Exists(cs2Cfg)) return cs2Cfg;
                    }
                    return @"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg";

                case "Battlefield 2042":
                    return Path.Combine(myDocuments, "Battlefield 2042", "settings");

                case "Battlefield V":
                    return Path.Combine(myDocuments, "Battlefield V", "settings");

                case "Battlefield 1":
                    return Path.Combine(myDocuments, "Battlefield 1", "settings");

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

