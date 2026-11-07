using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using TXATOOL.Helpers;

namespace TXATOOL.Services
{
    public static class TxaRepoService
    {
        private const string REPO_URL = "https://github.com/TXAVL/txa-repo";
        private const string INSTALL_SCRIPT_URL = "https://raw.githubusercontent.com/TXAVL/txa-repo/main/install.ps1";

        public static string GetInstalledPath()
        {
            try
            {
                // Default install location is %LOCALAPPDATA%\TXA\txapm.exe or scripts
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string txaExe = Path.Combine(localAppData, "TXA", "txapm.exe");
                if (File.Exists(txaExe)) return txaExe;

                // Also check if available in PATH
                var result = Utilities.RunCommandWithOutput("where.exe txapm");
                if (!string.IsNullOrWhiteSpace(result.Output) && result.ExitCode == 0)
                {
                    var lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0 && File.Exists(lines[0].Trim()))
                    {
                        return lines[0].Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogCrash(ex, "TxaRepoService.GetInstalledPath");
            }

            return string.Empty;
        }

        public static bool IsInstalled()
        {
            return !string.IsNullOrEmpty(GetInstalledPath());
        }

        public static async Task<string> GetInstalledVersionAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!IsInstalled()) return string.Empty;
                    var result = Utilities.RunCommandWithOutput("txapm --version");
                    if (!string.IsNullOrWhiteSpace(result.Output))
                    {
                        return result.Output.Trim();
                    }
                }
                catch { }
                return "Installed";
            });
        }

        public static void Install(string lang = "vi")
        {
            try
            {
                // Run PowerShell with install script
                string script = $"& ([scriptblock]::Create((irm {INSTALL_SCRIPT_URL}))) -Lang {lang}";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -ExecutionPolicy Bypass -Command \"Write-Host '==================================================' -ForegroundColor Cyan; Write-Host '   DANG CAI DAT KHO PHAN MEM TXA (txapm)...' -ForegroundColor Green; Write-Host '   Nguon: {REPO_URL}' -ForegroundColor DarkGray; Write-Host '==================================================' -ForegroundColor Cyan; {script}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.LogCrash(ex, "TxaRepoService.Install");
                throw;
            }
        }

        public static void Uninstall()
        {
            try
            {
                string script = $"& ([scriptblock]::Create((irm {INSTALL_SCRIPT_URL}))) -Uninstall";
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -ExecutionPolicy Bypass -Command \"Write-Host '==================================================' -ForegroundColor Yellow; Write-Host '   DANG GO CAI DAT KHO PHAN MEM TXA (txapm)...' -ForegroundColor Yellow; Write-Host '==================================================' -ForegroundColor Yellow; {script}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.LogCrash(ex, "TxaRepoService.Uninstall");
                throw;
            }
        }

        public static void RunDoctor()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoExit -ExecutionPolicy Bypass -Command \"Write-Host '--- TXA REPO DOCTOR ---' -ForegroundColor Cyan; txapm doctor\"",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.LogCrash(ex, "TxaRepoService.RunDoctor");
            }
        }

        public static void OpenTerminal()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoExit -ExecutionPolicy Bypass -Command \"Write-Host '=== TXA PACKAGE MANAGER (txapm) CONSOLE ===' -ForegroundColor Cyan; Write-Host 'Go txapm --help de xem danh sach lenh.' -ForegroundColor Gray; txapm --version\"",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.LogCrash(ex, "TxaRepoService.OpenTerminal");
            }
        }
    }
}

