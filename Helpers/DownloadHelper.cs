using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using TXATOOL.Models;

namespace TXATOOL.Helpers
{
    public static class DownloadHelper
    {
        private static readonly string JsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Download", "applications.json");
        private static readonly string ImagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "Apps");

        private const int InstallTimeoutMs = 12 * 60 * 1000;
        private const int UninstallTimeoutMs = 10 * 60 * 1000;
        private const int RefreshTimeoutMs = 20 * 1000;
        private const int WingetVerifyTimeoutMs = 15 * 1000;
        private const int ChocoVerifyTimeoutMs = 10 * 1000;

        private static readonly HashSet<string> _txaRepoAvailablePackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _txaRepoIndexLoaded = false;
        private static readonly object _txaRepoLock = new object();

        private static void PopulateTxaRepoPackagesFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("packages", out var packagesElem) || packagesElem.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                lock (_txaRepoLock)
                {
                    _txaRepoAvailablePackages.Clear();
                    foreach (var pkg in packagesElem.EnumerateArray())
                    {
                        if (pkg.TryGetProperty("id", out var idElem) && !string.IsNullOrWhiteSpace(idElem.GetString()))
                        {
                            _txaRepoAvailablePackages.Add(idElem.GetString()!);
                        }
                        if (pkg.TryGetProperty("alias", out var aliasElem) && !string.IsNullOrWhiteSpace(aliasElem.GetString()))
                        {
                            _txaRepoAvailablePackages.Add(aliasElem.GetString()!);
                        }
                        if (pkg.TryGetProperty("name", out var nameElem) && !string.IsNullOrWhiteSpace(nameElem.GetString()))
                        {
                            _txaRepoAvailablePackages.Add(nameElem.GetString()!);
                        }
                    }
                    _txaRepoIndexLoaded = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error populating txa-repo packages cache: {ex.Message}");
            }
        }

        public static void EnsureTxaRepoIndexLoaded()
        {
            if (_txaRepoIndexLoaded && _txaRepoAvailablePackages.Count > 0) return;

            string cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TXATOOL", "txa_repo_cache.json");

            if (File.Exists(cachePath))
            {
                try
                {
                    string json = File.ReadAllText(cachePath);
                    PopulateTxaRepoPackagesFromJson(json);
                }
                catch { }
            }
        }

        public static async Task<bool> IsPackageInTxaRepoAsync(string packageQuery)
        {
            if (string.IsNullOrWhiteSpace(packageQuery)) return false;
            EnsureTxaRepoIndexLoaded();
            if (_txaRepoAvailablePackages.Count == 0)
            {
                await LoadTxaRepoAppsAsync();
            }

            lock (_txaRepoLock)
            {
                if (_txaRepoAvailablePackages.Contains(packageQuery)) return true;
                return _txaRepoAvailablePackages.Any(p => p.IndexOf(packageQuery, StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }

        public static bool IsPackageInTxaRepo(AppItem app)
        {
            if (app == null) return false;
            if (!string.IsNullOrEmpty(app.Id) && app.Id.StartsWith("TXAVL.", StringComparison.OrdinalIgnoreCase)) return true;

            EnsureTxaRepoIndexLoaded();

            lock (_txaRepoLock)
            {
                if (_txaRepoAvailablePackages.Count == 0) return false;

                if (!string.IsNullOrEmpty(app.Id) && _txaRepoAvailablePackages.Contains(app.Id)) return true;
                if (!string.IsNullOrEmpty(app.ChocoId) && _txaRepoAvailablePackages.Contains(app.ChocoId)) return true;
                if (!string.IsNullOrEmpty(app.WingetId) && _txaRepoAvailablePackages.Contains(app.WingetId)) return true;
                if (!string.IsNullOrEmpty(app.Name) && _txaRepoAvailablePackages.Contains(app.Name)) return true;

                if (!string.IsNullOrEmpty(app.Name))
                {
                    string simple = app.Name.Split(' ')[0];
                    if (_txaRepoAvailablePackages.Contains(simple)) return true;
                }
            }

            return false;
        }

        public static async Task<List<CategoryGroup>> LoadAppsAsync()
        {
            if (AppConfigManager.Config.DownloadSource == "txa-repo" && Services.TxaRepoService.IsInstalled())
            {
                var txaApps = await LoadTxaRepoAppsAsync();
                if (txaApps != null && txaApps.Count > 0)
                {
                    return txaApps;
                }
            }

            return await Task.Run(() =>
            {
                if (!File.Exists(JsonPath)) return new List<CategoryGroup>();

                try
                {
                    string json = File.ReadAllText(JsonPath);
                    var rawData = JsonSerializer.Deserialize<Dictionary<string, AppJson>>(json);

                    if (rawData == null) return new List<CategoryGroup>();

                    var apps = new List<AppItem>();
                    var iconFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (Directory.Exists(ImagesPath))
                    {
                        foreach (var file in Directory.GetFiles(ImagesPath))
                        {
                            iconFiles[Path.GetFileNameWithoutExtension(file)] = file;
                        }
                    }

                    foreach (var kvp in rawData)
                    {
                        string id = kvp.Key;
                        var data = kvp.Value;
                        string? iconPath = null;

                        if (iconFiles.TryGetValue(id, out string? realPath))
                        {
                            iconPath = realPath;
                        }

                        apps.Add(new AppItem
                        {
                            Id = id,
                            Name = data.content,
                            Description = data.description,
                            Category = data.category,
                            Link = data.link,
                            ChocoId = data.choco,
                            WingetId = data.winget,
                            IconPath = iconPath,
                            IsSelected = false,
                            IsInstalled = false
                        });
                    }

                    return apps
                        .GroupBy(a => a.Category)
                        .Select(g => new CategoryGroup
                        {
                            CategoryName = g.Key,
                            Apps = g.ToList()
                        })
                        .OrderBy(g => g.CategoryName)
                        .ToList();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading apps: {ex.Message}");
                    Logger.LogCrash(ex, nameof(DownloadHelper) + ".LoadAppsAsync");
                    return new List<CategoryGroup>();
                }
            });
        }

        public static async Task<List<CategoryGroup>> LoadTxaRepoAppsAsync()
        {
            string cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TXATOOL", "txa_repo_cache.json");

            string json = string.Empty;

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                json = await client.GetStringAsync("https://txavl.github.io/txa-repo/api/txa/v1/index.json");
                try
                {
                    var dir = Path.GetDirectoryName(cachePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(cachePath, json);
                }
                catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to fetch online txa-repo index: {ex.Message}");
                if (File.Exists(cachePath))
                {
                    try { json = File.ReadAllText(cachePath); } catch { }
                }
            }

            if (string.IsNullOrWhiteSpace(json)) return new List<CategoryGroup>();

            PopulateTxaRepoPackagesFromJson(json);

            return await Task.Run(() =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (!doc.RootElement.TryGetProperty("packages", out var packagesElem) || packagesElem.ValueKind != JsonValueKind.Array)
                    {
                        return new List<CategoryGroup>();
                    }

                    bool isVn = (LocalizationService.CurrentLanguage ?? "en").ToLower().Contains("vn");

                    var iconFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (Directory.Exists(ImagesPath))
                    {
                        foreach (var file in Directory.GetFiles(ImagesPath))
                        {
                            iconFiles[Path.GetFileNameWithoutExtension(file)] = file;
                        }
                    }

                    var apps = new List<AppItem>();

                    foreach (var pkg in packagesElem.EnumerateArray())
                    {
                        string id = pkg.TryGetProperty("id", out var idElem) ? idElem.GetString() ?? "" : "";
                        if (string.IsNullOrEmpty(id)) continue;

                        string name = pkg.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? id : id;
                        string alias = pkg.TryGetProperty("alias", out var aliasElem) ? aliasElem.GetString() ?? "" : "";
                        string summary = pkg.TryGetProperty("summary", out var sumElem) ? sumElem.GetString() ?? "" : "";

                        if (pkg.TryGetProperty("summaries", out var sumsElem) && sumsElem.ValueKind == JsonValueKind.Object)
                        {
                            if (isVn && sumsElem.TryGetProperty("vi-VN", out var viSum) && !string.IsNullOrEmpty(viSum.GetString()))
                            {
                                summary = viSum.GetString()!;
                            }
                            else if (!isVn && sumsElem.TryGetProperty("en-US", out var enSum) && !string.IsNullOrEmpty(enSum.GetString()))
                            {
                                summary = enSum.GetString()!;
                            }
                        }

                        var tags = new List<string>();
                        if (pkg.TryGetProperty("tags", out var tagsElem) && tagsElem.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var t in tagsElem.EnumerateArray())
                            {
                                string? ts = t.GetString();
                                if (!string.IsNullOrEmpty(ts)) tags.Add(ts.ToLowerInvariant());
                            }
                        }

                        // Categorize accurately based on tags and ID
                        string category;
                        string idLower = id.ToLowerInvariant();

                        if (tags.Any(t => t.Contains("game") || t.Contains("gaming") || t.Contains("gta") || t.Contains("steam") || t.Contains("launcher"))
                            || idLower.Contains("warframe") || idLower.Contains("steam") || idLower.Contains("epicgames") || idLower.Contains("rockstar") || idLower.Contains("ubisoft") || idLower.Contains("gameloop") || idLower.Contains("unity"))
                        {
                            category = isVn ? "🎮 Trò Chơi & Game Launchers" : "🎮 Games & Launchers";
                        }
                        else if (tags.Any(t => t.Contains("agent") || t.Contains("ai") || t.Contains("code") || t.Contains("develop") || t.Contains("programming") || t.Contains("editor") || t.Contains("ide") || t.Contains("llm") || t.Contains("git"))
                            || idLower.Contains("antigravity") || idLower.Contains("python") || idLower.Contains("nodejs") || idLower.Contains("git") || idLower.Contains("rust") || idLower.Contains("golang") || idLower.Contains("visualstudio") || idLower.Contains("vscode") || idLower.Contains("jetbrains") || idLower.Contains("neovim"))
                        {
                            category = isVn ? "🤖 AI & Công Cụ Lập Trình" : "🤖 AI & Developer Tools";
                        }
                        else if (tags.Any(t => t.Contains("browser") || t.Contains("chat") || t.Contains("messaging") || t.Contains("remote") || t.Contains("rdp") || t.Contains("network"))
                            || idLower.Contains("chrome") || idLower.Contains("firefox") || idLower.Contains("edge") || idLower.Contains("brave") || idLower.Contains("zalo") || idLower.Contains("telegram") || idLower.Contains("discord") || idLower.Contains("anydesk") || idLower.Contains("teamviewer") || idLower.Contains("zoom") || idLower.Contains("wireshark"))
                        {
                            category = isVn ? "🌐 Trình Duyệt & Giao Tiếp" : "🌐 Browsers & Communication";
                        }
                        else if (tags.Any(t => t.Contains("media") || t.Contains("video") || t.Contains("audio") || t.Contains("player") || t.Contains("graphics") || t.Contains("photo") || t.Contains("3d") || t.Contains("draw") || t.Contains("vfx"))
                            || idLower.Contains("vlc") || idLower.Contains("blender") || idLower.Contains("audacity") || idLower.Contains("gimp") || idLower.Contains("inkscape") || idLower.Contains("photoshop") || idLower.Contains("capcut") || idLower.Contains("obs") || idLower.Contains("spotify") || idLower.Contains("handbrake") || idLower.Contains("ffmpeg"))
                        {
                            category = isVn ? "🎨 Đồ Họa & Đa Phương Tiện" : "🎨 Media & Graphics";
                        }
                        else if (tags.Any(t => t.Contains("office") || t.Contains("document") || t.Contains("notes") || t.Contains("markdown") || t.Contains("pdf") || t.Contains("reader") || t.Contains("typing") || t.Contains("password") || t.Contains("security"))
                            || idLower.Contains("office") || idLower.Contains("notion") || idLower.Contains("obsidian") || idLower.Contains("unikey") || idLower.Contains("bitwarden") || idLower.Contains("keepass") || idLower.Contains("foxit") || idLower.Contains("acrobat") || idLower.Contains("sumatrapdf") || idLower.Contains("winrar") || idLower.Contains("sevenzip"))
                        {
                            category = isVn ? "📄 Văn Phòng & Năng Suất" : "📄 Office & Productivity";
                        }
                        else
                        {
                            category = isVn ? "⚡ Hệ Thống & Tiện Ích" : "⚡ System & Utilities";
                        }

                        // Look for icon matching alias or id
                        string? iconPath = null;
                        if (!string.IsNullOrEmpty(alias) && iconFiles.TryGetValue(alias, out string? p1))
                        {
                            iconPath = p1;
                        }
                        else if (iconFiles.TryGetValue(id, out string? p2))
                        {
                            iconPath = p2;
                        }
                        else
                        {
                            string simpleName = name.Split(' ')[0].ToLowerInvariant();
                            if (iconFiles.TryGetValue(simpleName, out string? p3))
                            {
                                iconPath = p3;
                            }
                        }

                        apps.Add(new AppItem
                        {
                            Id = id,
                            Name = name,
                            Description = summary,
                            Category = category,
                            Link = "https://txavl.github.io/txa-repo/",
                            WingetId = id,
                            ChocoId = !string.IsNullOrEmpty(alias) ? alias : id,
                            IconPath = iconPath,
                            IsSelected = false,
                            IsInstalled = false
                        });
                    }

                    return apps
                        .GroupBy(a => a.Category)
                        .Select(g => new CategoryGroup
                        {
                            CategoryName = g.Key,
                            Apps = g.OrderBy(a => a.Name).ToList()
                        })
                        .OrderBy(g => g.CategoryName)
                        .ToList();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error parsing txa-repo json: {ex.Message}");
                    return new List<CategoryGroup>();
                }
            });
        }

        public static async Task UpdateInstalledStatusAsync(List<CategoryGroup> categories)
        {
            if (categories == null || categories.Count == 0)
            {
                return;
            }

            if (AppConfigManager.Config.DownloadSource == "txa-repo" && Services.TxaRepoService.IsInstalled())
            {
                var txaInstalled = await GetInstalledTxaRepoPackages();
                foreach (var app in categories.SelectMany(group => group.Apps ?? Enumerable.Empty<AppItem>()))
                {
                    bool isInstalled = (!string.IsNullOrEmpty(app.Id) && txaInstalled.Contains(app.Id))
                                       || (!string.IsNullOrEmpty(app.WingetId) && txaInstalled.Contains(app.WingetId))
                                       || (!string.IsNullOrEmpty(app.ChocoId) && txaInstalled.Contains(app.ChocoId));
                    app.IsInstalled = isInstalled;
                    app.StatusText = isInstalled ? "Installed" : "";
                }
                return;
            }

            var chocoTask = GetInstalledChocoPackages(RefreshTimeoutMs, CommandKind.Refresh);
            var wingetTask = GetInstalledWingetPackages(RefreshTimeoutMs, CommandKind.Refresh);

            await Task.WhenAll(chocoTask, wingetTask);

            ApplyInstalledState(
                categories.SelectMany(group => group.Apps ?? Enumerable.Empty<AppItem>()),
                chocoTask.Result,
                wingetTask.Result);
        }

        public static async Task<HashSet<string>> GetInstalledTxaRepoPackages()
        {
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var res = await Utilities.RunCommandAsync("txapm list", true);
                if (!string.IsNullOrWhiteSpace(res.Output))
                {
                    var lines = res.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("Id", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("-"))
                            continue;

                        var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            installed.Add(parts[0].Trim());
                        }
                    }
                }
            }
            catch { }
            return installed;
        }

        public static async Task<HashSet<string>> GetInstalledChocoPackages(int timeoutMs = RefreshTimeoutMs, CommandKind commandKind = CommandKind.Refresh)
        {
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string cmd = $"{GetChocoCommand()} list --local-only --limit-output";
                var result = await Utilities.RunCommandMonitoredAsync(cmd, commandKind, timeoutMs, captureOutput: true);
                if (result.TimedOut)
                {
                    Logger.LogInfo("DOWNLOAD", "Chocolatey refresh timed out.");
                    return installed;
                }

                if (string.IsNullOrWhiteSpace(result.StdOut))
                {
                    return installed;
                }

                using var reader = new StringReader(result.StdOut);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split('|');
                    if (parts.Length > 0)
                    {
                        installed.Add(parts[0].Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting choco packages: {ex.Message}");
                Logger.LogCrash(ex, nameof(DownloadHelper) + ".GetInstalledChocoPackages");
            }
            return installed;
        }

        public static async Task<HashSet<string>> GetInstalledWingetPackages(int timeoutMs = RefreshTimeoutMs, CommandKind commandKind = CommandKind.Refresh)
        {
            var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string psCommand = $"& '{WingetPath}' list | Out-String -Width 512";
                string cmd = $"powershell -NoProfile -Command \"{psCommand}\"";

                var result = await Utilities.RunCommandMonitoredAsync(cmd, commandKind, timeoutMs, captureOutput: true);
                if (result.TimedOut)
                {
                    Logger.LogInfo("DOWNLOAD", "Winget refresh timed out.");
                    return installed;
                }

                if (string.IsNullOrWhiteSpace(result.StdOut)) return installed;

                using var reader = new StringReader(result.StdOut);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.StartsWith("Name") && line.Contains("Id")) continue;
                    if (line.StartsWith("---")) continue;

                    var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var token in tokens)
                    {
                        if (token.Length > 2)
                        {
                            installed.Add(token.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting winget packages: {ex.Message}");
                Logger.LogCrash(ex, nameof(DownloadHelper) + ".GetInstalledWingetPackages");
            }
            return installed;
        }

        private static string GetChocoCommand()
        {
            string chocoPath = @"C:\ProgramData\chocolatey\bin\choco.exe";
            if (File.Exists(chocoPath))
            {
                return $"\"{chocoPath}\"";
            }
            return "choco";
        }

        public static async Task<List<PackageOperationResult>> InstallPackages(List<AppItem> apps, Action<double>? progressCallback = null)
        {
            return await RunPackageOperationsAsync(apps, CommandKind.Install, InstallTimeoutMs, progressCallback);
        }

        public static async Task<List<PackageOperationResult>> UninstallPackages(List<AppItem> apps, Action<double>? progressCallback = null)
        {
            return await RunPackageOperationsAsync(apps, CommandKind.Uninstall, UninstallTimeoutMs, progressCallback);
        }

        public static async Task<List<PackageOperationResult>> VerifyInstalledAppsAsync(IEnumerable<AppItem> apps, bool expectedInstalled)
        {
            var results = new List<PackageOperationResult>();
            if (apps == null)
            {
                return results;
            }

            foreach (var app in apps.Where(a => a != null).Distinct())
            {
                var packageManager = GetPackageManager(app);
                if (packageManager == PackageManagerKind.Unknown)
                {
                    results.Add(new PackageOperationResult
                    {
                        App = app,
                        PackageManager = packageManager,
                        Status = PackageOperationStatus.VerificationDeferred,
                        Message = "No supported package manager metadata found."
                    });
                    continue;
                }

                CommandExecutionResult execution;
                if (packageManager == PackageManagerKind.Winget)
                {
                    string wingetId = app.WingetId ?? string.Empty;
                    string source = IsMsStoreApp(app) ? "msstore" : "winget";
                    string psCommand = $"& '{WingetPath}' list --id '{EscapePowerShellValue(wingetId)}' --exact --source {source} | Out-String -Width 512";
                    execution = await Utilities.RunCommandMonitoredAsync(
                        $"powershell -NoProfile -Command \"{psCommand}\"",
                        CommandKind.Verify,
                        WingetVerifyTimeoutMs,
                        captureOutput: true);
                }
                else
                {
                    string chocoId = SanitizePackageId(app.ChocoId);
                    string cmd = $"{GetChocoCommand()} list --local-only {chocoId} --limit-output";
                    execution = await Utilities.RunCommandMonitoredAsync(
                        cmd,
                        CommandKind.Verify,
                        ChocoVerifyTimeoutMs,
                        captureOutput: true);
                }

                if (execution.TimedOut)
                {
                    results.Add(new PackageOperationResult
                    {
                        App = app,
                        PackageManager = packageManager,
                        Status = PackageOperationStatus.VerificationDeferred,
                        Execution = execution,
                        Message = "Verification timed out."
                    });
                    continue;
                }

                bool isInstalled = packageManager == PackageManagerKind.Winget
                    ? OutputIndicatesWingetPackagePresent(execution.StdOut, app.WingetId)
                    : OutputIndicatesChocolateyPackagePresent(execution.StdOut, app.ChocoId);

                results.Add(new PackageOperationResult
                {
                    App = app,
                    PackageManager = packageManager,
                    Status = isInstalled == expectedInstalled ? PackageOperationStatus.Succeeded : PackageOperationStatus.Failed,
                    Execution = execution,
                    DetectedInstalledState = isInstalled,
                    Message = isInstalled == expectedInstalled ? "Verification succeeded." : "Verification result did not match expected state."
                });
            }

            return results;
        }

        private static async Task<List<PackageOperationResult>> RunPackageOperationsAsync(
            List<AppItem> apps,
            CommandKind commandKind,
            int timeoutMs,
            Action<double>? progressCallback)
        {
            var results = new List<PackageOperationResult>();
            if (apps == null || apps.Count == 0) return results;

            int total = apps.Count;
            int processed = 0;

            foreach (var app in apps)
            {
                var (packageManager, fellBack) = ResolvePackageManager(app);
                PackageOperationResult operationResult;

                if (fellBack)
                {
                    bool isVn = (LocalizationService.CurrentLanguage ?? "en").ToLower().Contains("vn");
                    string fallbackNotice = isVn
                        ? $"[Thông báo] Ứng dụng '{app.Name}' chưa có trên kho TXA Repo. Tự động chuyển sang cài đặt qua {packageManager}."
                        : $"[Notice] App '{app.Name}' is not available in TXA Repo. Automatically switching to {packageManager}.";
                    Logger.LogInfo("DOWNLOAD", fallbackNotice);
                }

                if (packageManager == PackageManagerKind.Unknown)
                {
                    operationResult = new PackageOperationResult
                    {
                        App = app,
                        PackageManager = packageManager,
                        FellBackFromTxaRepo = fellBack,
                        Status = PackageOperationStatus.Failed,
                        Message = fellBack
                            ? "Package not available in TXA Repo and no fallback manager found."
                            : "No supported package manager metadata found."
                    };
                }
                else if (commandKind == CommandKind.Uninstall && packageManager == PackageManagerKind.Chocolatey)
                {
                    Logger.LogInfo("DOWNLOAD", $"{commandKind} started for {app.Name} via {packageManager}.");
                    operationResult = await RunChocolateyUninstallAsync(app, timeoutMs);
                    operationResult.FellBackFromTxaRepo = fellBack;
                }
                else
                {
                    string command = BuildPackageCommand(app, packageManager, commandKind);
                    Logger.LogInfo("DOWNLOAD", $"{commandKind} started for {app.Name} via {packageManager}.");
                    var execution = await Utilities.RunCommandMonitoredAsync(command, commandKind, timeoutMs, captureOutput: false, windowStyle: ProcessWindowStyle.Minimized);

                    string opMessage = BuildOperationMessage(execution);
                    if (fellBack)
                    {
                        bool isVn = (LocalizationService.CurrentLanguage ?? "en").ToLower().Contains("vn");
                        string prefix = isVn ? "[Chuyển từ TXA Repo sang Winget] " : "[Fallback from TXA Repo to Winget] ";
                        opMessage = prefix + opMessage;
                    }

                    operationResult = new PackageOperationResult
                    {
                        App = app,
                        PackageManager = packageManager,
                        FellBackFromTxaRepo = fellBack,
                        Execution = execution,
                        Status = MapCommandResultStatus(execution, commandKind),
                        Message = opMessage
                    };
                }

                results.Add(operationResult);
                processed++;
                progressCallback?.Invoke((double)processed / total * 100);
                Logger.LogInfo("DOWNLOAD", $"{commandKind} finished for {app.Name}. Status={operationResult.Status}.");
            }

            return results;
        }

        private static async Task<PackageOperationResult> RunChocolateyUninstallAsync(AppItem app, int timeoutMs)
        {
            string baseId = SanitizePackageId(app.ChocoId);
            var installedPackages = await GetInstalledChocoPackages(RefreshTimeoutMs, CommandKind.Refresh);
            var uninstallTargets = GetChocolateyPackageAliases(baseId)
                .Where(installedPackages.Contains)
                .OrderBy(target => target.Equals(baseId, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ToList();

            if (uninstallTargets.Count == 0)
            {
                return new PackageOperationResult
                {
                    App = app,
                    PackageManager = PackageManagerKind.Chocolatey,
                    Status = PackageOperationStatus.Succeeded,
                    Message = "Package is already absent."
                };
            }

            CommandExecutionResult? lastExecution = null;
            bool anyTimedOut = false;
            var messageParts = new List<string>();

            foreach (string target in uninstallTargets)
            {
                string uninstallCmd = $"{GetChocoCommand()} uninstall {target} -y";
                lastExecution = await Utilities.RunCommandMonitoredAsync(uninstallCmd, CommandKind.Uninstall, timeoutMs, captureOutput: false, windowStyle: ProcessWindowStyle.Minimized);
                messageParts.Add($"{target}: {BuildOperationMessage(lastExecution)}");

                if (lastExecution.TimedOut)
                {
                    anyTimedOut = true;
                    continue;
                }

                if (!IsSuccessfulUninstallExitCode(lastExecution.ExitCode))
                {
                    string forceCmd = $"{GetChocoCommand()} uninstall {target} -y -n --skip-autouninstaller";
                    Logger.LogInfo("DOWNLOAD", $"Retrying uninstall for {app.Name} target {target} with Chocolatey force options.");
                    lastExecution = await Utilities.RunCommandMonitoredAsync(forceCmd, CommandKind.Uninstall, timeoutMs, captureOutput: false, windowStyle: ProcessWindowStyle.Minimized);
                    messageParts.Add($"{target} force: {BuildOperationMessage(lastExecution)}");
                    if (lastExecution.TimedOut)
                    {
                        anyTimedOut = true;
                    }
                }
            }

            var remainingPackages = await GetInstalledChocoPackages(RefreshTimeoutMs, CommandKind.Refresh);
            bool stillInstalled = GetChocolateyPackageAliases(baseId).Any(remainingPackages.Contains);

            return new PackageOperationResult
            {
                App = app,
                PackageManager = PackageManagerKind.Chocolatey,
                Execution = lastExecution,
                Status = stillInstalled
                    ? (anyTimedOut ? PackageOperationStatus.TimedOut : PackageOperationStatus.Failed)
                    : PackageOperationStatus.Succeeded,
                Message = stillInstalled
                    ? $"Remaining Chocolatey packages detected for {baseId}. {string.Join(" | ", messageParts)}"
                    : string.Join(" | ", messageParts)
            };
        }

        private static string BuildPackageCommand(AppItem app, PackageManagerKind packageManager, CommandKind commandKind)
        {
            if (packageManager == PackageManagerKind.TxaRepo)
            {
                string target = !string.IsNullOrWhiteSpace(app.Id) ? app.Id : app.WingetId ?? app.ChocoId ?? "";
                if (commandKind == CommandKind.Install)
                {
                    return $"txapm install {target}";
                }
                else
                {
                    return $"txapm uninstall {target} -y";
                }
            }

            if (packageManager == PackageManagerKind.Winget)
            {
                string wingetId = SanitizePackageId(app.WingetId);
                string source = IsMsStoreApp(app) ? "msstore" : "winget";
                string exePath = WingetPath == "winget" ? "winget" : $"\"{WingetPath}\"";
                string cmd;
                if (commandKind == CommandKind.Install)
                {
                    cmd = $"{exePath} install --id {wingetId} --exact --source {source} --accept-source-agreements --accept-package-agreements";
                }
                else
                {
                    cmd = $"{exePath} uninstall --id {wingetId} --exact --source {source} --accept-source-agreements";
                }
                return WingetPath == "winget" ? cmd : $"\"{cmd}\"";
            }

            string chocoId = SanitizePackageId(app.ChocoId);
            if (commandKind == CommandKind.Install)
            {
                return $"{GetChocoCommand()} install {chocoId} -y";
            }

            return $"{GetChocoCommand()} uninstall {chocoId} -y";
        }

        private static PackageOperationStatus MapCommandResultStatus(CommandExecutionResult execution, CommandKind commandKind)
        {
            if (execution.TimedOut)
            {
                return PackageOperationStatus.TimedOut;
            }

            bool success = commandKind == CommandKind.Uninstall
                ? IsSuccessfulUninstallExitCode(execution.ExitCode)
                : IsSuccessfulInstallExitCode(execution.ExitCode);

            return success ? PackageOperationStatus.Succeeded : PackageOperationStatus.Failed;
        }

        private static bool IsSuccessfulInstallExitCode(int exitCode)
        {
            return exitCode == 0 || exitCode == 1641 || exitCode == 3010;
        }

        private static bool IsSuccessfulUninstallExitCode(int exitCode)
        {
            return exitCode == 0 || exitCode == 1605 || exitCode == 1614 || exitCode == 1641 || exitCode == 3010;
        }

        private static string BuildOperationMessage(CommandExecutionResult execution)
        {
            if (execution.TimedOut)
            {
                return "Command timed out.";
            }

            if (!string.IsNullOrWhiteSpace(execution.StdErr))
            {
                return execution.StdErr.Trim();
            }

            if (!string.IsNullOrWhiteSpace(execution.StdOut))
            {
                return execution.StdOut.Trim();
            }

            return execution.ExitCode == 0 ? "Command completed." : $"Command failed with exit code {execution.ExitCode}.";
        }

        public static (PackageManagerKind pm, bool fellBackFromTxaRepo) ResolvePackageManager(AppItem app)
        {
            bool preferTxa = AppConfigManager.Config.DownloadSource == "txa-repo" && Services.TxaRepoService.IsInstalled();

            if (preferTxa)
            {
                if (IsPackageInTxaRepo(app))
                {
                    return (PackageManagerKind.TxaRepo, false);
                }

                // If not available on TXA Repo, automatically fall back to Winget / Chocolatey
                if (IsWingetApp(app))
                {
                    return (PackageManagerKind.Winget, true);
                }

                if (!string.IsNullOrWhiteSpace(app.ChocoId) &&
                    !app.ChocoId.Equals("na", StringComparison.OrdinalIgnoreCase))
                {
                    return (PackageManagerKind.Chocolatey, true);
                }

                return (PackageManagerKind.Unknown, true);
            }

            if (IsWingetApp(app))
            {
                return (PackageManagerKind.Winget, false);
            }

            if (!string.IsNullOrWhiteSpace(app.ChocoId) &&
                !app.ChocoId.Equals("na", StringComparison.OrdinalIgnoreCase))
            {
                return (PackageManagerKind.Chocolatey, false);
            }

            return (PackageManagerKind.Unknown, false);
        }

        private static PackageManagerKind GetPackageManager(AppItem app)
        {
            var (pm, _) = ResolvePackageManager(app);
            return pm;
        }

        private static void ApplyInstalledState(IEnumerable<AppItem> apps, HashSet<string> installedChoco, HashSet<string> installedWinget)
        {
            foreach (var app in apps)
            {
                bool installed = IsChocolateyAppInstalled(app, installedChoco);

                if (!installed && !string.IsNullOrEmpty(app.WingetId) && installedWinget.Contains(app.WingetId))
                {
                    installed = true;
                }

                app.IsInstalled = installed;
            }
        }

        private static bool IsChocolateyAppInstalled(AppItem app, HashSet<string> installedChoco)
        {
            if (string.IsNullOrWhiteSpace(app.ChocoId) || app.ChocoId.Equals("na", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return GetChocolateyPackageAliases(app.ChocoId).Any(installedChoco.Contains);
        }

        private static IEnumerable<string> GetChocolateyPackageAliases(string? chocoId)
        {
            string baseId = SanitizePackageId(chocoId);
            if (string.IsNullOrWhiteSpace(baseId))
            {
                return Enumerable.Empty<string>();
            }

            if (baseId.EndsWith(".install", StringComparison.OrdinalIgnoreCase) ||
                baseId.EndsWith(".portable", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { baseId };
            }

            return new[]
            {
                baseId,
                baseId + ".install",
                baseId + ".portable"
            };
        }

        private static bool OutputIndicatesWingetPackagePresent(string output, string? wingetId)
        {
            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(wingetId))
            {
                return false;
            }

            if (output.IndexOf("No installed package found", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            using var reader = new StringReader(output);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.IndexOf(wingetId, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OutputIndicatesChocolateyPackagePresent(string output, string? chocoId)
        {
            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(chocoId))
            {
                return false;
            }

            var aliases = GetChocolateyPackageAliases(chocoId);
            using var reader = new StringReader(output);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                foreach (string alias in aliases)
                {
                    if (line.StartsWith(alias + "|", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsWingetApp(AppItem app)
        {
            return (string.IsNullOrEmpty(app.ChocoId) || app.ChocoId.Equals("na", StringComparison.OrdinalIgnoreCase))
                   && !string.IsNullOrEmpty(app.WingetId);
        }

        private static bool IsMsStoreApp(AppItem app)
        {
            return !string.IsNullOrEmpty(app.WingetId) && !app.WingetId.Contains('.');
        }

        private static string SanitizePackageId(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace("\"", string.Empty).Trim();
        }

        private static string EscapePowerShellValue(string value)
        {
            return value.Replace("'", "''");
        }

        public static async Task UpdateAll()
        {
            if (AppConfigManager.Config.DownloadSource == "txa-repo" && Services.TxaRepoService.IsInstalled())
            {
                var txaResult = await Utilities.RunCommandMonitoredAsync("txapm upgrade --all", CommandKind.Install, InstallTimeoutMs, captureOutput: false, windowStyle: ProcessWindowStyle.Minimized);
                Logger.LogInfo("DOWNLOAD", $"UpdateAll TXA Repo (txapm) completed with exit code {txaResult.ExitCode}.");
            }

            string cmdChoco = $"{GetChocoCommand()} upgrade all -y";
            string exePath = WingetPath == "winget" ? "winget" : $"\"{WingetPath}\"";
            string cmdWinget = $"{exePath} upgrade --all --silent --accept-source-agreements --accept-package-agreements";
            if (WingetPath != "winget") cmdWinget = $"\"{cmdWinget}\"";

            var chocoResult = await Utilities.RunCommandMonitoredAsync(cmdChoco, CommandKind.Install, InstallTimeoutMs, captureOutput: false, windowStyle: ProcessWindowStyle.Minimized);
            Logger.LogInfo("DOWNLOAD", $"UpdateAll Chocolatey completed with exit code {chocoResult.ExitCode}.");

            var wingetResult = await Utilities.RunCommandMonitoredAsync(cmdWinget, CommandKind.Install, InstallTimeoutMs, captureOutput: false, windowStyle: ProcessWindowStyle.Minimized);
            Logger.LogInfo("DOWNLOAD", $"UpdateAll Winget completed with exit code {wingetResult.ExitCode}.");
        }

        public static async Task<bool> IsChocoInstalled()
        {
            if (File.Exists(@"C:\ProgramData\chocolatey\bin\choco.exe")) return true;

            var result = await Utilities.RunCommandAsync("choco -v", true);
            string version = result.Output;
            return !string.IsNullOrWhiteSpace(version) && !version.ToLower().Contains("not recognized");
        }

        public static async Task InstallChoco()
        {
             string installCmd = @"powershell -NoProfile -ExecutionPolicy Bypass -Command ""Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))""";
             await Utilities.RunCommandAsync(installCmd);
        }

        public static string WingetPath { get; private set; } = "winget";
        private static bool _isWingetResolved = false;

        public static async Task<bool> DetermineWingetPathAsync()
        {
            if (_isWingetResolved) return true;

            try
            {
                var result = await Utilities.RunCommandAsync("winget -v", true);
                string output = result.Output?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(output) && !output.ToLower().Contains("not recognized") && !output.ToLower().Contains("is not recognized"))
                {
                    WingetPath = "winget";
                    _isWingetResolved = true;
                    return true;
                }
            }
            catch { }

            // Check %LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe
            try
            {
                string userAppsWinget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
                if (File.Exists(userAppsWinget))
                {
                    var result = await Utilities.RunCommandAsync($"\"{userAppsWinget}\" -v", true);
                    string output = result.Output?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(output) && (output.StartsWith("v", StringComparison.OrdinalIgnoreCase) || output.Contains(".")))
                    {
                        WingetPath = userAppsWinget;
                        _isWingetResolved = true;
                        return true;
                    }
                }
            }
            catch { }

            // Check bundled portable winget-cli
            string portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "winget-cli", "winget.exe");
            if (File.Exists(portablePath))
            {
                WingetPath = portablePath;
                _isWingetResolved = true;
                return true;
            }

            WingetPath = "winget";
            return false;
        }

        public static async Task InstallWinget()
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "winget-install.ps1");
            if (!File.Exists(scriptPath))
            {
                Logger.LogInfo("DOWNLOAD", $"Winget install script not found at: {scriptPath}");
                return;
            }

            string installCmd = $"powershell -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
            await Utilities.RunCommandAsync(installCmd);
        }
    }
}

