using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace HJ_Inc_Backup.Services
{
    public static class BackupServiceController
    {
        public const string ServiceName = "HJ XAMPP Incremental Backup";
        public const string ServiceExeName = "HJ_Inc_Backup.Service.exe";

        /// <summary>
        /// Folder under the main app where service files live:
        /// {AppDir}\Service\
        /// </summary>
        public static string GetServiceInstallDir()
        {
            return Path.Combine(AppContext.BaseDirectory, "Service");
        }

        public static string GetServiceExePath()
        {
            return Path.Combine(GetServiceInstallDir(), ServiceExeName);
        }

        public static string GetStatus()
        {
            try
            {
                using var sc = new ServiceController(ServiceName);
                return sc.Status.ToString();
            }
            catch (InvalidOperationException)
            {
                return "Not installed";
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        public static bool IsInstalled()
        {
            try
            {
                using var sc = new ServiceController(ServiceName);
                var _ = sc.Status;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> StopAsync(Action<string>? log = null)
        {
            try
            {
                using var sc = new ServiceController(ServiceName);

                if (sc.Status == ServiceControllerStatus.Stopped ||
                    sc.Status == ServiceControllerStatus.StopPending)
                {
                    log?.Invoke($"Service '{ServiceName}' is already stopped.");
                    return true;
                }

                log?.Invoke($"Stopping service '{ServiceName}'...");
                sc.Stop();
                await Task.Run(() =>
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(45)));

                log?.Invoke("Service stopped.");
                return true;
            }
            catch (InvalidOperationException)
            {
                log?.Invoke($"Service '{ServiceName}' is not installed.");
                return false;
            }
            catch (Exception ex)
            {
                log?.Invoke($"Could not stop service: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> StartAsync(Action<string>? log = null)
        {
            try
            {
                using var sc = new ServiceController(ServiceName);

                if (sc.Status == ServiceControllerStatus.Running ||
                    sc.Status == ServiceControllerStatus.StartPending)
                {
                    log?.Invoke($"Service '{ServiceName}' is already running.");
                    return true;
                }

                log?.Invoke($"Starting service '{ServiceName}'...");
                sc.Start();
                await Task.Run(() =>
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(45)));

                log?.Invoke("Service started.");
                return true;
            }
            catch (InvalidOperationException)
            {
                log?.Invoke($"Service '{ServiceName}' is not installed.");
                return false;
            }
            catch (Exception ex)
            {
                log?.Invoke($"Could not start service: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Copies service files into {AppDir}\Service\ and registers the Windows service.
        /// sourceDir = folder that currently contains HJ_Inc_Backup.Service.exe (e.g. publish output).
        /// </summary>
        public static async Task InstallAsync(string? sourceDir, Action<string>? log = null)
        {
            string installDir = GetServiceInstallDir();
            string exePath = GetServiceExePath();

            // Resolve source
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
            {
                // Default: look next to the app for a pre-shipped Service folder payload
                sourceDir = Path.Combine(AppContext.BaseDirectory, "ServicePayload");
            }

            if (!Directory.Exists(sourceDir))
            {
                // Try relative to solution publish path (dev convenience)
                string devPath = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory, "..", "..", "..", "..",
                    "HJ_Inc_Backup.Service", "bin", "Release", "net10.0-windows"));

                if (Directory.Exists(devPath) && File.Exists(Path.Combine(devPath, ServiceExeName)))
                    sourceDir = devPath;
            }

            if (!Directory.Exists(sourceDir) || !File.Exists(Path.Combine(sourceDir, ServiceExeName)))
            {
                throw new DirectoryNotFoundException(
                    "Could not find service files to install.\n\n" +
                    "Publish the service first, or place files in:\n" +
                    Path.Combine(AppContext.BaseDirectory, "ServicePayload"));
            }

            log?.Invoke($"Copying service files to: {installDir}");
            Directory.CreateDirectory(installDir);

            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
            {
                string dest = Path.Combine(installDir, Path.GetFileName(file));
                File.Copy(file, dest, overwrite: true);
            }

            // Also copy any runtimes / native subfolders if present
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string name = Path.GetFileName(dir);
                string destDir = Path.Combine(installDir, name);
                CopyDirectory(dir, destDir);
            }

            log?.Invoke("Files copied.");

            if (IsInstalled())
            {
                log?.Invoke("Service already registered. Stopping before reconfigure...");
                await StopAsync(log);
                await RunScAsync($"delete \"{ServiceName}\"", log);
                await Task.Delay(1000);
            }

            string binPath = exePath;
            // sc.exe needs spaces escaped carefully
            string createArgs = $"create \"{ServiceName}\" binPath= \"{binPath}\" start= auto DisplayName= \"HJ XAMPP Incremental Backup\"";

            log?.Invoke("Registering Windows service...");
            await RunScAsync(createArgs, log);

            await RunScAsync($"description \"{ServiceName}\" \"Hourly incremental + daily full backup of XAMPP\"", log);

            log?.Invoke("Service installed successfully.");
            log?.Invoke($"Executable: {binPath}");
        }

        public static async Task UninstallAsync(Action<string>? log = null)
        {
            if (!IsInstalled())
            {
                log?.Invoke("Service is not installed.");
                return;
            }

            await StopAsync(log);
            await Task.Delay(500);

            log?.Invoke("Removing Windows service registration...");
            await RunScAsync($"delete \"{ServiceName}\"", log);

            log?.Invoke("Service uninstalled.");
            // Leave files in place so user can reinstall quickly; optional cleanup:
            // try { Directory.Delete(GetServiceInstallDir(), true); } catch { }
        }

        private static async Task RunScAsync(string args, Action<string>? log)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start sc.exe");

            string stdout = await p.StandardOutput.ReadToEndAsync();
            string stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(stdout))
                log?.Invoke(stdout.Trim());
            if (!string.IsNullOrWhiteSpace(stderr))
                log?.Invoke(stderr.Trim());

            if (p.ExitCode != 0 && p.ExitCode != 1072) // 1072 = marked for deletion
            {
                // sc delete while running can return non-zero; still surface it
                log?.Invoke($"sc.exe exit code: {p.ExitCode}");
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
        }
    }
}