using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TXATOOL.Helpers;
using TXATOOL.Models;
using TXATOOL.Views.Components;

namespace TXATOOL.Views
{
    public partial class DownloadView : UserControl
    {
        public List<CategoryGroup> Categories { get; set; }
        private List<CategoryGroup> _allCategories;

        public DownloadView()
        {
            InitializeComponent();
            Helpers.LocalizationService.LanguageChanged += ApplyTranslations;
            // Data loading moved to Loaded event
        }

        private void ApplyTranslations()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(ApplyTranslations);
                return;
            }

            btnInstallSelected.Content = Helpers.LocalizationService.Get("btnInstallSelected");
            btnUninstallSelected.Content = Helpers.LocalizationService.Get("btnUninstallSelected");
            btnClearSelection.Content = Helpers.LocalizationService.Get("btnClearSelection");
            btnUpdateAll.Content = Helpers.LocalizationService.Get("btnUpdateAll");

            bool isVn = Helpers.LocalizationService.IsVietnamese;
            if (txtDownloadSearch != null)
            {
                txtDownloadSearch.ToolTip = isVn ? "Gõ tên ứng dụng để tìm kiếm nhanh..." : "Type application name to search...";
            }
            if (txtNoResultsDesc != null)
            {
                txtNoResultsDesc.Text = isVn 
                    ? "Hãy thử tìm kiếm bằng từ khóa khác hoặc tên gói phần mềm." 
                    : "Try searching with a different keyword or package name.";
            }

            UpdateAppStatusTexts();
        }

        private void UpdateAppStatusTexts()
        {
            var source = _allCategories ?? Categories;
            if (source == null) return;

            string installedText = Helpers.LocalizationService.Get("txtInstalled");

            foreach (var group in source)
            {
                if (group.Apps == null) continue;
                foreach (var app in group.Apps)
                {
                    if (app.IsInstalled)
                    {
                        app.StatusText = installedText;
                    }
                }
            }
        }

        private static bool _hasCheckedDependencies = false;

        private async void DownloadView_Loaded(object sender, RoutedEventArgs e)
        {
             ApplyTranslations();
             await LoadData();
        }

        private void UpdateRepoBadgeUI()
        {
            try
            {
                bool isTxa = AppConfigManager.Config.DownloadSource == "txa-repo" && Services.TxaRepoService.IsInstalled();
                bool isVn = (Helpers.LocalizationService.CurrentLanguage ?? "en").ToLower().Contains("vn");

                if (txtRepoBadge != null)
                {
                    txtRepoBadge.Text = isTxa
                        ? (isVn ? "Nguồn: Kho phần mềm TXA (txa-repo)" : "Source: TXA Store (txa-repo)")
                        : (isVn ? "Nguồn: Mặc định (Winget / Choco)" : "Source: Default (Winget / Choco)");
                }
                if (txtRepoBadgeIcon != null)
                {
                    txtRepoBadgeIcon.Text = isTxa ? "⚡" : "📦";
                }
            }
            catch { }
        }

        private async Task LoadData()
        {
            try
            {
                UpdateRepoBadgeUI();

                bool isTxaRepo = AppConfigManager.Config.DownloadSource == "txa-repo" && Services.TxaRepoService.IsInstalled();

                if (!isTxaRepo && !_hasCheckedDependencies)
                {
                    _hasCheckedDependencies = true;

                    if (!await DownloadHelper.IsChocoInstalled())
                    {
                        var infoWindow = new ModernConfirmationWindow(
                            Helpers.LocalizationService.Get("msgChocoInstalling"),
                            Helpers.LocalizationService.Get("msgSettingUp"),
                            "OK",
                            null);

                        if (Window.GetWindow(this) != null) infoWindow.Owner = Window.GetWindow(this);

                        infoWindow.Show();

                        try
                        {
                            await DownloadHelper.InstallChoco();
                        }
                        finally
                        {
                            infoWindow.Close();
                        }
                    }

                    bool isSystemWinget = await DownloadHelper.DetermineWingetPathAsync();
                    if (!isSystemWinget)
                    {
                        var notifyWindow = new ModernConfirmationWindow(
                            Helpers.LocalizationService.Get("msgWingetInstallingBackground"),
                            Helpers.LocalizationService.Get("msgSettingUp"),
                            "OK",
                            null);

                        if (Window.GetWindow(this) != null) notifyWindow.Owner = Window.GetWindow(this);
                        notifyWindow.Show();

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await DownloadHelper.InstallWinget();
                                Logger.LogInfo("DOWNLOAD", "Background Winget installation completed.");
                            }
                            catch (System.Exception ex)
                            {
                                Logger.LogCrash(ex, "BackgroundInstallWinget");
                            }
                            finally
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    try { notifyWindow.Close(); } catch { }
                                });
                            }
                        });
                    }
                }
                else
                {
                    await DownloadHelper.DetermineWingetPathAsync();
                }

                Categories = await DownloadHelper.LoadAppsAsync();
                _allCategories = Categories;
                CategoriesList.ItemsSource = Categories;

                await DownloadHelper.UpdateInstalledStatusAsync(_allCategories ?? Categories);
                UpdateAppStatusTexts();
                UpdateRepoBadgeUI();
            }
            catch (System.Exception ex)
            {
                Logger.LogCrash(ex, nameof(DownloadView) + ".LoadData");
            }
        }

        private async void InstallSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedApps = GetSelectedApps();
            if (!selectedApps.Any()) return;

            List<PackageOperationResult> operationResults = new List<PackageOperationResult>();
            SetLoading(true, Helpers.LocalizationService.Get("msgInstallingApps"), 0);

            try
            {
                operationResults = await DownloadHelper.InstallPackages(selectedApps, p =>
                {
                    Dispatcher.Invoke(() => SetLoading(true, Helpers.LocalizationService.Get("msgInstallingApps"), (int)p));
                });
            }
            catch (System.Exception ex)
            {
                Logger.LogCrash(ex, nameof(DownloadView) + ".InstallSelected_Click");
            }
            finally
            {
                SetLoading(false);
            }

            LogOperationSummary("Install", operationResults);
            _ = RefreshStatusesAfterOperationAsync(selectedApps, true, "install");

            var fellBackApps = operationResults
                .Where(r => r.FellBackFromTxaRepo)
                .Select(r => r.App?.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList();

            if (fellBackApps.Any())
            {
                bool isVn = (LocalizationService.CurrentLanguage ?? "en").ToLower().Contains("vn");
                string msg = isVn
                    ? $"Các ứng dụng sau chưa có trên kho TXA Repo và đã được tự động chuyển sang cài đặt qua Winget:\n\n• {string.Join("\n• ", fellBackApps)}"
                    : $"The following applications are not yet available in TXA Repo and were automatically installed via Winget:\n\n• {string.Join("\n• ", fellBackApps)}";
                CustomMessageBoxWindow.Show(msg, isVn ? "Thông Báo Chuyển Nguồn" : "Source Fallback Notice");
            }
        }

        private async void UninstallSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedApps = GetSelectedApps();
            if (!selectedApps.Any()) return;

            List<PackageOperationResult> operationResults = new List<PackageOperationResult>();
            SetLoading(true, Helpers.LocalizationService.Get("msgUninstallingApps"), 0);

            try
            {
                operationResults = await DownloadHelper.UninstallPackages(selectedApps, p =>
                {
                    Dispatcher.Invoke(() => SetLoading(true, Helpers.LocalizationService.Get("msgUninstallingApps"), (int)p));
                });
            }
            catch (System.Exception ex)
            {
                Logger.LogCrash(ex, nameof(DownloadView) + ".UninstallSelected_Click");
            }
            finally
            {
                SetLoading(false);
            }

            LogOperationSummary("Uninstall", operationResults);
            _ = RefreshStatusesAfterOperationAsync(selectedApps, false, "uninstall");
        }

        private async void UpdateAll_Click(object sender, RoutedEventArgs e)
        {
            SetLoading(true, Helpers.LocalizationService.Get("msgUpdatingApps"), 0);
            try
            {
                await DownloadHelper.UpdateAll();
            }
            catch (System.Exception ex)
            {
                Logger.LogCrash(ex, nameof(DownloadView) + ".UpdateAll_Click");
            }
            finally
            {
                SetLoading(false);
            }

            _ = RefreshAllStatusesAsync("update-all");
        }

        private async Task RefreshStatusesAfterOperationAsync(List<AppItem> apps, bool expectedInstalled, string operationName)
        {
            if (apps == null || apps.Count == 0)
            {
                return;
            }

            try
            {
                var verificationResults = await DownloadHelper.VerifyInstalledAppsAsync(apps, expectedInstalled);
                ApplyVerificationResults(verificationResults);

                if (verificationResults.Any(result => result.Status == PackageOperationStatus.VerificationDeferred))
                {
                    Logger.LogInfo("DOWNLOAD", $"{operationName} verification deferred. Scheduling full refresh.");
                    _ = RefreshAllStatusesAsync(operationName + "-deferred");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogCrash(ex, nameof(DownloadView) + ".RefreshStatusesAfterOperationAsync");
                _ = RefreshAllStatusesAsync(operationName + "-fallback");
            }
        }

        private async Task RefreshAllStatusesAsync(string reason)
        {
            try
            {
                var source = _allCategories ?? Categories;
                if (source == null)
                {
                    return;
                }

                Logger.LogInfo("DOWNLOAD", $"Refreshing all download statuses. Reason={reason}.");
                await DownloadHelper.UpdateInstalledStatusAsync(source);
                UpdateAppStatusTexts();
            }
            catch (System.Exception ex)
            {
                Logger.LogCrash(ex, nameof(DownloadView) + ".RefreshAllStatusesAsync");
            }
        }

        private void ApplyVerificationResults(IEnumerable<PackageOperationResult> results)
        {
            if (results == null)
            {
                return;
            }

            foreach (var result in results)
            {
                if (result.App == null)
                {
                    continue;
                }

                if (result.DetectedInstalledState.HasValue)
                {
                    result.App.IsInstalled = result.DetectedInstalledState.Value;
                }

                if (result.Status == PackageOperationStatus.VerificationDeferred)
                {
                    Logger.LogInfo("DOWNLOAD", $"Verification deferred for {result.App.Name}.");
                }
                else if (result.Status == PackageOperationStatus.Failed)
                {
                    Logger.LogInfo("DOWNLOAD", $"Verification mismatch for {result.App.Name}: {result.Message}");
                }
            }

            UpdateAppStatusTexts();
        }

        private void LogOperationSummary(string operationName, IEnumerable<PackageOperationResult> results)
        {
            if (results == null)
            {
                return;
            }

            int succeeded = results.Count(r => r.Status == PackageOperationStatus.Succeeded);
            int failed = results.Count(r => r.Status == PackageOperationStatus.Failed);
            int timedOut = results.Count(r => r.Status == PackageOperationStatus.TimedOut);
            int deferred = results.Count(r => r.Status == PackageOperationStatus.VerificationDeferred);

            Logger.LogInfo(
                "DOWNLOAD",
                $"{operationName} summary: Succeeded={succeeded}; Failed={failed}; TimedOut={timedOut}; Deferred={deferred}");
        }

        private void ClearSelection_Click(object sender, RoutedEventArgs e)
        {
            var source = _allCategories ?? Categories;
            if (source == null) return;
            foreach (var group in source)
            {
                if (group.Apps == null) continue;
                foreach (var app in group.Apps)
                {
                    app.IsSelected = false;
                }
            }
        }

        private List<AppItem> GetSelectedApps()
        {
            var selected = new List<AppItem>();
            var source = _allCategories ?? Categories;
            if (source == null) return selected;

            foreach (var group in source)
            {
                if (group.Apps == null) continue;
                selected.AddRange(group.Apps.Where(a => a.IsSelected));
            }
            return selected;
        }

        private void SetLoading(bool isLoading, string message = "", int percentage = 0)
        {
             var mainWindow = Window.GetWindow(this) as MainWindow;
             if (mainWindow == null) return;

             if (isLoading)
             {
                 int step = 1;
                 int localPercent = 0;

                 if (percentage < 33)
                 {
                     step = 1;
                     localPercent = (int)(percentage * 3.03);
                 }
                 else if (percentage < 66)
                 {
                     step = 2;
                     localPercent = (int)((percentage - 33) * 3.03);
                 }
                 else
                 {
                     step = 3;
                     localPercent = (int)((percentage - 66) * 3.03);
                 }

                 mainWindow.ShowLoading(message, step, localPercent);
             }
             else
             {
                 mainWindow.HideLoading();
             }
        }

        private void InnerScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var parentScrollViewer = GetScrollViewer(CategoriesList);
            if (parentScrollViewer != null)
            {
                if (e.Delta < 0)
                {
                    parentScrollViewer.LineDown();
                    parentScrollViewer.LineDown();
                    parentScrollViewer.LineDown();
                }
                else
                {
                    parentScrollViewer.LineUp();
                    parentScrollViewer.LineUp();
                    parentScrollViewer.LineUp();
                }
                e.Handled = true;
            }
        }

        private void AppItem_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is FrameworkElement element && element.DataContext is AppItem app)
            {
                app.IsSelected = !app.IsSelected;
                e.Handled = true;
            }
        }

        private static ScrollViewer? GetScrollViewer(DependencyObject o)
        {
            if (o is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = VisualTreeHelper.GetChild(o, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }
        private bool _isFilteringInternal = false;

        private void DownloadSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFilteringInternal) return;
            string query = txtDownloadSearch?.Text ?? "";
            FilterApps(query);

            try
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow?.SearchBox != null && mainWindow.SearchBox.Text != query)
                {
                    _isFilteringInternal = true;
                    mainWindow.SearchBox.Text = query;
                    _isFilteringInternal = false;
                }
            }
            catch { }
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();
            foreach (var c in normalizedString)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC)
                .Replace('đ', 'd').Replace('Đ', 'D');
        }

        public void FilterApps(string searchText)
        {
            if (_allCategories == null) return;

            if (!_isFilteringInternal && txtDownloadSearch != null && txtDownloadSearch.Text != searchText)
            {
                _isFilteringInternal = true;
                txtDownloadSearch.Text = searchText;
                _isFilteringInternal = false;
            }

            string cleanSearch = searchText?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(cleanSearch))
            {
                Categories = _allCategories;
                if (pnlNoResults != null) pnlNoResults.Visibility = Visibility.Collapsed;
            }
            else
            {
                string lowerQuery = cleanSearch.ToLowerInvariant();
                string normQuery = RemoveDiacritics(lowerQuery);

                var filteredGroups = new List<CategoryGroup>();
                foreach (var group in _allCategories)
                {
                    if (group.Apps == null) continue;

                    var matchingApps = group.Apps.Where(app =>
                    {
                        if (app == null) return false;

                        // 1. Direct name match
                        string name = app.Name ?? "";
                        if (name.IndexOf(cleanSearch, StringComparison.OrdinalIgnoreCase) >= 0) return true;

                        // 2. Unaccented name match (e.g. coc coc -> Cốc Cốc)
                        string normName = RemoveDiacritics(name.ToLowerInvariant());
                        if (normName.IndexOf(normQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;

                        // 3. ID match (e.g. vscode, googlechrome, unikey)
                        string id = app.Id ?? "";
                        if (id.IndexOf(lowerQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;

                        // 4. Description match
                        string desc = app.Description ?? "";
                        if (desc.IndexOf(cleanSearch, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                        string normDesc = RemoveDiacritics(desc.ToLowerInvariant());
                        if (normDesc.IndexOf(normQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;

                        // 5. Package IDs
                        if (!string.IsNullOrEmpty(app.WingetId) && app.WingetId.IndexOf(lowerQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                        if (!string.IsNullOrEmpty(app.ChocoId) && app.ChocoId.IndexOf(lowerQuery, StringComparison.OrdinalIgnoreCase) >= 0) return true;

                        // 6. Category match
                        string cat = app.Category ?? "";
                        if (cat.IndexOf(cleanSearch, StringComparison.OrdinalIgnoreCase) >= 0) return true;

                        return false;
                    }).ToList();

                    if (matchingApps.Any())
                    {
                        filteredGroups.Add(new CategoryGroup
                        {
                            CategoryName = group.CategoryName,
                            Apps = matchingApps
                        });
                    }
                }
                Categories = filteredGroups;

                if (pnlNoResults != null)
                {
                    bool isVn = (LocalizationService.CurrentLanguage ?? "en").ToLower().Contains("vn");
                    if (txtNoResultsTitle != null)
                        txtNoResultsTitle.Text = isVn 
                            ? $"Không tìm thấy ứng dụng phù hợp với \"{cleanSearch}\""
                            : $"No applications found for \"{cleanSearch}\"";
                    pnlNoResults.Visibility = (filteredGroups.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            CategoriesList.ItemsSource = null;
            CategoriesList.ItemsSource = Categories;
        }
    }
}
