using HJ_Inc_Backup.Services;
using Ookii.Dialogs.Wpf;
using System.IO;
using System.Windows;

namespace HJ_Inc_Backup
{
    public partial class MainWindow : Window
    {
        private bool _isBusy;
        private CancellationTokenSource? _cts;

        public MainWindow()
        {
            InitializeComponent();

            TxtSourcePath.Text = @"C:\xampp";
            TxtDestPath.Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "XamppBackups");

            RefreshServiceStatus();
        }

        // -------------------------------------------------------------------------
        // Browse
        // -------------------------------------------------------------------------

        private void BtnBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select XAMPP root folder",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog(this) == true)
                TxtSourcePath.Text = dialog.SelectedPath;
        }

        private void BtnBrowseDest_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select backup destination folder",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog(this) == true)
                TxtDestPath.Text = dialog.SelectedPath;
        }

        // -------------------------------------------------------------------------
        // Backup buttons
        // -------------------------------------------------------------------------

        private async void BtnFullBackup_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            await RunBackupAsync(isIncremental: false, stopService: true);
        }

        private async void BtnIncrementalBackup_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;
            await RunBackupAsync(isIncremental: true, stopService: false);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            Log("Cancellation requested...");
        }
        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            TxtLog.Clear();
        }
        // -------------------------------------------------------------------------
        // Restore
        // -------------------------------------------------------------------------

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            string backupRoot = TxtDestPath.Text.Trim();
            string targetRoot = TxtSourcePath.Text.Trim();

            if (string.IsNullOrWhiteSpace(backupRoot) || !Directory.Exists(backupRoot))
            {
                MessageBox.Show("Backup destination folder does not exist or is empty.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var restoreWindow = new RestoreWindow(backupRoot, targetRoot)
            {
                Owner = this
            };

            if (restoreWindow.ShowDialog() != true)
                return;

            string? backupFolder = restoreWindow.SelectedBackupFolder;
            string? target = restoreWindow.SelectedTargetPath;
            bool stopServices = restoreWindow.StopServices;

            if (string.IsNullOrEmpty(backupFolder) || string.IsNullOrEmpty(target))
                return;

            var confirm = MessageBox.Show(
                $"This will overwrite files in:\n\n{target}\n\nContinue?",
                "Confirm Restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            await RunRestoreAsync(backupFolder, target, stopServices);
        }

        // -------------------------------------------------------------------------
        // Service controls
        // -------------------------------------------------------------------------

        private void RefreshServiceStatus()
        {
            try
            {
                TxtServiceStatus.Text = "Status: " + BackupServiceController.GetStatus();
            }
            catch
            {
                TxtServiceStatus.Text = "Status: Unknown";
            }
        }

        private async void BtnServiceInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            try
            {
                _isBusy = true;
                SetUiBusy(true);
                Log("=== INSTALL SERVICE ===");

                await BackupServiceController.InstallAsync(sourceDir: null, msg =>
                    Dispatcher.Invoke(() => Log(msg)));

                MessageBox.Show(
                    "Service installed.\n\nFiles are in:\n" + BackupServiceController.GetServiceInstallDir(),
                    "Install",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                MessageBox.Show(ex.Message, "Install failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isBusy = false;
                SetUiBusy(false);
                RefreshServiceStatus();
            }
        }

        private async void BtnServiceUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            var confirm = MessageBox.Show(
                "Uninstall the backup Windows service?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                _isBusy = true;
                SetUiBusy(true);
                Log("=== UNINSTALL SERVICE ===");

                await BackupServiceController.UninstallAsync(msg =>
                    Dispatcher.Invoke(() => Log(msg)));

                MessageBox.Show("Service uninstalled.", "Uninstall",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                MessageBox.Show(ex.Message, "Uninstall failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isBusy = false;
                SetUiBusy(false);
                RefreshServiceStatus();
            }
        }

        private async void BtnServiceStart_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            try
            {
                _isBusy = true;
                SetUiBusy(true);
                await BackupServiceController.StartAsync(msg =>
                    Dispatcher.Invoke(() => Log(msg)));
            }
            finally
            {
                _isBusy = false;
                SetUiBusy(false);
                RefreshServiceStatus();
            }
        }

        private async void BtnServiceStop_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            try
            {
                _isBusy = true;
                SetUiBusy(true);
                await BackupServiceController.StopAsync(msg =>
                    Dispatcher.Invoke(() => Log(msg)));
            }
            finally
            {
                _isBusy = false;
                SetUiBusy(false);
                RefreshServiceStatus();
            }
        }

        // -------------------------------------------------------------------------
        // Backup engine
        // -------------------------------------------------------------------------

        private async System.Threading.Tasks.Task RunBackupAsync(bool isIncremental, bool stopService)
        {
            string source = TxtSourcePath.Text.Trim();
            string destRoot = TxtDestPath.Text.Trim();

            if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source))
            {
                MessageBox.Show("Please select a valid XAMPP root folder.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(destRoot))
            {
                MessageBox.Show("Please select a backup destination folder.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _cts = new CancellationTokenSource();
            var engine = new BackupEngine();

            engine.LogMessage += msg => Dispatcher.Invoke(() => Log(msg));
            engine.ProgressChanged += (percent, detail) =>
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressBar.Value = percent;
                    TxtProgressDetail.Text = detail;
                    TxtStatus.Text = $"Copying... {percent:F1}%";
                });
            };

            bool serviceWasStopped = false;

            try
            {
                _isBusy = true;
                SetUiBusy(true);

                if (stopService)
                {
                    Log("Stopping backup service for manual Full backup...");
                    serviceWasStopped = await BackupServiceController.StopAsync(msg =>
                        Dispatcher.Invoke(() => Log(msg)));
                }

                Log($"=== {(isIncremental ? "INCREMENTAL" : "FULL")} BACKUP STARTED ===");

                string resultPath = await engine.RunBackupAsync(
                    source, destRoot, isIncremental, _cts.Token);

                Log($"Backup finished: {resultPath}");
                TxtStatus.Text = "Backup completed successfully";
                MessageBox.Show("Backup completed successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                Log("Backup was cancelled.");
                TxtStatus.Text = "Cancelled";
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                MessageBox.Show(ex.Message, "Backup Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Error";
            }
            finally
            {
                if (serviceWasStopped)
                {
                    Log("Restarting backup service...");
                    await BackupServiceController.StartAsync(msg =>
                        Dispatcher.Invoke(() => Log(msg)));
                }

                _isBusy = false;
                SetUiBusy(false);
                ProgressBar.Value = 0;
                TxtProgressDetail.Text = "";
                RefreshServiceStatus();
                _cts?.Dispose();
                _cts = null;
            }
        }

        // -------------------------------------------------------------------------
        // Restore engine
        // -------------------------------------------------------------------------

        private async System.Threading.Tasks.Task RunRestoreAsync(
            string backupFolder,
            string targetRoot,
            bool stopAndStartServices = true)
        {
            _cts = new CancellationTokenSource();
            var engine = new RestoreEngine();

            engine.LogMessage += msg => Dispatcher.Invoke(() => Log(msg));
            engine.ProgressChanged += (percent, detail) =>
            {
                Dispatcher.Invoke(() =>
                {
                    ProgressBar.Value = percent;
                    TxtProgressDetail.Text = detail;
                    TxtStatus.Text = $"Restoring... {percent:F1}%";
                });
            };

            try
            {
                _isBusy = true;
                SetUiBusy(true);

                Log("=== RESTORE STARTED ===");
                Log($"From : {backupFolder}");
                Log($"To   : {targetRoot}");

                await engine.RestoreAsync(backupFolder, targetRoot, stopAndStartServices, _cts.Token);

                TxtStatus.Text = "Restore completed successfully";
                MessageBox.Show("Restore completed successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                Log("Restore was cancelled.");
                TxtStatus.Text = "Cancelled";
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                MessageBox.Show(ex.Message, "Restore Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                TxtStatus.Text = "Error";
            }
            finally
            {
                _isBusy = false;
                SetUiBusy(false);
                ProgressBar.Value = 0;
                TxtProgressDetail.Text = "";
                RefreshServiceStatus();
                _cts?.Dispose();
                _cts = null;
            }
        }

        // -------------------------------------------------------------------------
        // UI helpers
        // -------------------------------------------------------------------------

        private void SetUiBusy(bool busy)
        {
            BtnFullBackup.IsEnabled = !busy;
            BtnIncrementalBackup.IsEnabled = !busy;
            BtnRestore.IsEnabled = !busy;
            BtnBrowseSource.IsEnabled = !busy;
            BtnBrowseDest.IsEnabled = !busy;
            BtnCancel.IsEnabled = busy;

            BtnServiceInstall.IsEnabled = !busy;
            BtnServiceUninstall.IsEnabled = !busy;
            BtnServiceStart.IsEnabled = !busy;
            BtnServiceStop.IsEnabled = !busy;
        }

        private void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            TxtLog.AppendText(line + Environment.NewLine);
            TxtLog.ScrollToEnd();
        }

        private void TxtDestPath_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
    }
}