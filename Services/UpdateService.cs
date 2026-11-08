using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using TXATOOL.Helpers;

namespace TXATOOL.Services
{
    public class UpdateInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("release_date")]
        public string ReleaseDate { get; set; } = string.Empty;

        [JsonPropertyName("download_url_setup")]
        public string DownloadUrlSetup { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public List<string> Content { get; set; } = new List<string>();

        [JsonPropertyName("size")]
        public string Size { get; set; } = string.Empty;

        [JsonPropertyName("sha512")]
        public string Sha512 { get; set; } = string.Empty;

        [JsonPropertyName("is_critical")]
        public bool IsCritical { get; set; }
    }

    public class TxaYmlInfo
    {
        public string Version { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Sha512 { get; set; } = string.Empty;
        public long Size { get; set; }
        public string ReleaseDate { get; set; } = string.Empty;

        public static TxaYmlInfo? Parse(string yamlContent)
        {
            if (string.IsNullOrWhiteSpace(yamlContent)) return null;

            var info = new TxaYmlInfo();
            using var reader = new StringReader(yamlContent);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("version:", StringComparison.OrdinalIgnoreCase))
                {
                    info.Version = trimmed.Substring(8).Trim().Trim('\'', '"');
                }
                else if (trimmed.StartsWith("path:", StringComparison.OrdinalIgnoreCase))
                {
                    info.Path = trimmed.Substring(5).Trim().Trim('\'', '"');
                }
                else if (trimmed.StartsWith("url:", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(info.Path))
                {
                    info.Path = trimmed.Substring(4).Trim().Trim('\'', '"');
                }
                else if (trimmed.StartsWith("sha512:", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(info.Sha512))
                {
                    info.Sha512 = trimmed.Substring(7).Trim().Trim('\'', '"');
                }
                else if (trimmed.StartsWith("size:", StringComparison.OrdinalIgnoreCase) && info.Size == 0)
                {
                    if (long.TryParse(trimmed.Substring(5).Trim(), out var sz)) info.Size = sz;
                }
                else if (trimmed.StartsWith("releaseDate:", StringComparison.OrdinalIgnoreCase))
                {
                    info.ReleaseDate = trimmed.Substring(12).Trim().Trim('\'', '"');
                }
            }

            return string.IsNullOrEmpty(info.Version) ? null : info;
        }
    }

    public class UpdateService
    {
        private const string TXA_YML_URL = "https://raw.githubusercontent.com/TXAVL/TXATOOL/refs/heads/main/txa.yml";
        private const string UPDATE_EN_URL = "https://raw.githubusercontent.com/TXAVL/TXATOOL/refs/heads/main/update_en.json";
        private const string UPDATE_VN_URL = "https://raw.githubusercontent.com/TXAVL/TXATOOL/refs/heads/main/update_vn.json";
        private const string GITHUB_API_LATEST = "https://api.github.com/repos/TXAVL/TXATOOL/releases/latest";
        private static readonly HttpClient _httpClient = new HttpClient();

        public UpdateService()
        {
            // Set User-Agent as required by GitHub API and CDNs
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "TXATOOL-AutoUpdater");
            }
        }

        private static async Task<string> FetchFreshStringAsync(string url)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true,
                MustRevalidate = true
            };
            request.Headers.Pragma.ParseAdd("no-cache");
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public Version GetCurrentVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version ?? new Version(1, 0, 0, 0);
            }
            catch
            {
                return new Version(1, 0, 0, 0);
            }
        }

        public async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var currentVersion = GetCurrentVersion();

                // 1. First, attempt to check via txa.yml with no-cache headers
                try
                {
                    var ymlUrl = $"{TXA_YML_URL}?t={timestamp}";
                    var ymlResponse = await FetchFreshStringAsync(ymlUrl);
                    var ymlInfo = TxaYmlInfo.Parse(ymlResponse);

                    if (ymlInfo != null && !string.IsNullOrWhiteSpace(ymlInfo.Version))
                    {
                        var cleanYmlVer = ymlInfo.Version.ToLower().Replace("v", "").Trim();
                        if (Version.TryParse(cleanYmlVer, out var serverYmlVersion))
                        {
                            if (serverYmlVersion > currentVersion)
                            {
                                var updateInfo = new UpdateInfo
                                {
                                    Version = ymlInfo.Version,
                                    ReleaseDate = ymlInfo.ReleaseDate,
                                    Sha512 = ymlInfo.Sha512,
                                    DownloadUrlSetup = $"https://github.com/TXAVL/TXATOOL/releases/download/v{cleanYmlVer}/{ymlInfo.Path}",
                                    Size = ymlInfo.Size > 0 ? $"{Math.Round(ymlInfo.Size / (1024.0 * 1024.0), 1)} MB" : "137 MB"
                                };

                                // Load friendly changelog from JSON if available
                                await PopulateChangelogFromJsonAsync(updateInfo, timestamp);
                                return updateInfo;
                            }
                        }
                    }
                }
                catch (Exception ymlEx)
                {
                    Logger.LogCrash(ymlEx, "UpdateService.CheckForUpdateAsync.TxaYml");
                }

                // 2. Fallback to update_vn.json / update_en.json with no-cache headers
                try
                {
                    var currentLang = LocalizationService.CurrentLanguage?.ToLower() ?? "en";
                    var baseUrl = currentLang.Contains("vn") ? UPDATE_VN_URL : UPDATE_EN_URL;
                    var url = $"{baseUrl}?t={timestamp}";
                    var response = await FetchFreshStringAsync(url);

                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        var jsonUpdateInfo = JsonSerializer.Deserialize<UpdateInfo>(response);
                        if (jsonUpdateInfo != null && !string.IsNullOrWhiteSpace(jsonUpdateInfo.Version))
                        {
                            var cleanVersion = jsonUpdateInfo.Version.ToLower().Replace("v", "").Trim();
                            if (Version.TryParse(cleanVersion, out var serverVersion))
                            {
                                if (serverVersion > currentVersion)
                                {
                                    return jsonUpdateInfo;
                                }
                            }
                        }
                    }
                }
                catch (Exception jsonEx)
                {
                    Logger.LogCrash(jsonEx, "UpdateService.CheckForUpdateAsync.Json");
                }

                // 3. Fallback to GitHub Releases API (always real-time, no raw caching)
                try
                {
                    var ghResponse = await FetchFreshStringAsync(GITHUB_API_LATEST);
                    using var doc = JsonDocument.Parse(ghResponse);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("tag_name", out var tagElem))
                    {
                        var tag = tagElem.GetString() ?? "";
                        var cleanTag = tag.ToLower().Replace("v", "").Trim();
                        if (Version.TryParse(cleanTag, out var ghVersion) && ghVersion > currentVersion)
                        {
                            string downloadUrl = "";
                            long exeSize = 0;
                            if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var asset in assetsElem.EnumerateArray())
                                {
                                    if (asset.TryGetProperty("name", out var nameElem) && nameElem.GetString()?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
                                    {
                                        if (asset.TryGetProperty("browser_download_url", out var dlElem))
                                            downloadUrl = dlElem.GetString() ?? "";
                                        if (asset.TryGetProperty("size", out var szElem))
                                            exeSize = szElem.GetInt64();
                                        break;
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(downloadUrl))
                            {
                                var ghUpdate = new UpdateInfo
                                {
                                    Version = cleanTag,
                                    ReleaseDate = root.TryGetProperty("published_at", out var pubElem) ? pubElem.GetString() ?? "" : "",
                                    DownloadUrlSetup = downloadUrl,
                                    Size = exeSize > 0 ? $"{Math.Round(exeSize / (1024.0 * 1024.0), 1)} MB" : "140 MB"
                                };
                                await PopulateChangelogFromJsonAsync(ghUpdate, timestamp);
                                return ghUpdate;
                            }
                        }
                    }
                }
                catch (Exception ghEx)
                {
                    Logger.LogCrash(ghEx, "UpdateService.CheckForUpdateAsync.GitHubApi");
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.LogCrash(ex, "UpdateService.CheckForUpdateAsync");
                return null; // Fail silently on network errors
            }
        }

        private async Task PopulateChangelogFromJsonAsync(UpdateInfo updateInfo, long timestamp)
        {
            try
            {
                var currentLang = LocalizationService.CurrentLanguage?.ToLower() ?? "en";
                var baseUrl = currentLang.Contains("vn") ? UPDATE_VN_URL : UPDATE_EN_URL;
                var url = $"{baseUrl}?t={timestamp}";
                var response = await FetchFreshStringAsync(url);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    var jsonInfo = JsonSerializer.Deserialize<UpdateInfo>(response);
                    if (jsonInfo != null && jsonInfo.Content != null && jsonInfo.Content.Count > 0)
                    {
                        updateInfo.Content = jsonInfo.Content;
                        if (!string.IsNullOrWhiteSpace(jsonInfo.ReleaseDate)) updateInfo.ReleaseDate = jsonInfo.ReleaseDate;
                    }
                }
            }
            catch { }
        }

        public async Task DownloadAndRunInstallerAsync(string url, IProgress<double> progress)
        {
            try
            {
                var fileName = "TXATOOL_Setup_Update.exe";
                var tempPath = Path.Combine(Path.GetTempPath(), fileName);

                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes != -1 && progress != null;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var totalRead = 0L;
                        var buffer = new byte[8192];
                        var isMoreToRead = true;

                        do
                        {
                            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            if (read == 0)
                            {
                                isMoreToRead = false;
                            }
                            else
                            {
                                await fileStream.WriteAsync(buffer, 0, read);

                                totalRead += read;
                                if (canReportProgress)
                                {
                                    var percentage = Math.Round((double)totalRead / totalBytes * 100, 2);
                                    progress?.Report(percentage);
                                }
                            }
                        }
                        while (isMoreToRead);
                    }
                }

                // Run NSIS installer with interactive wizard (not silent) so user can proceed manually
                var psi = new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(psi);
                
                // Exit app so installer can overwrite files
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                Logger.LogCrash(ex, "UpdateService.DownloadAndRunInstallerAsync");
                throw;
            }
        }
    }
}
