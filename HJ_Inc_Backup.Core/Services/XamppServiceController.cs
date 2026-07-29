using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace HJ_Inc_Backup.Services
{
    public static class XamppServiceController
    {
        // Common service names used by different XAMPP versions
        private static readonly string[] ApacheCandidates =
        {
            "Apache2.4",
            "Apache2.4",
            "apache2.4",
            "Apache",
            "xamppapache"
        };

        private static readonly string[] MysqlCandidates =
        {
            "mysql",
            "MySQL",
            "mariadb",
            "xamppmysql"
        };

        public static async Task StopServicesAsync(Action<string>? log = null)
        {
            log?.Invoke("Stopping XAMPP services...");

            await StopServiceAsync(ApacheCandidates, "Apache", log);
            await StopServiceAsync(MysqlCandidates, "MySQL/MariaDB", log);

            // Give the services a moment to fully release file locks
            await Task.Delay(1500);
            log?.Invoke("Services stopped.");
        }

        public static async Task StartServicesAsync(Action<string>? log = null)
        {
            log?.Invoke("Starting XAMPP services...");

            await StartServiceAsync(MysqlCandidates, "MySQL/MariaDB", log);
            await StartServiceAsync(ApacheCandidates, "Apache", log);

            log?.Invoke("Services started.");
        }

        private static async Task StopServiceAsync(string[] candidates, string friendlyName, Action<string>? log)
        {
            foreach (var name in candidates)
            {
                try
                {
                    using var sc = new ServiceController(name);
                    if (sc.Status == ServiceControllerStatus.Running ||
                        sc.Status == ServiceControllerStatus.StartPending)
                    {
                        log?.Invoke($"Stopping {friendlyName} ({name})...");
                        sc.Stop();
                        await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30)));
                        log?.Invoke($"{friendlyName} stopped.");
                        return;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Service not found under this name – try next
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Warning while stopping {name}: {ex.Message}");
                }
            }

            // Fallback: try net stop
            foreach (var name in candidates)
            {
                try
                {
                    var psi = new ProcessStartInfo("net", $"stop \"{name}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        await p.WaitForExitAsync();
                        if (p.ExitCode == 0)
                        {
                            log?.Invoke($"{friendlyName} stopped via net stop.");
                            return;
                        }
                    }
                }
                catch { }
            }

            log?.Invoke($"Could not find a running {friendlyName} service (it may already be stopped).");
        }

        private static async Task StartServiceAsync(string[] candidates, string friendlyName, Action<string>? log)
        {
            foreach (var name in candidates)
            {
                try
                {
                    using var sc = new ServiceController(name);
                    if (sc.Status == ServiceControllerStatus.Stopped ||
                        sc.Status == ServiceControllerStatus.StopPending)
                    {
                        log?.Invoke($"Starting {friendlyName} ({name})...");
                        sc.Start();
                        await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(45)));
                        log?.Invoke($"{friendlyName} started.");
                        return;
                    }
                    else if (sc.Status == ServiceControllerStatus.Running)
                    {
                        log?.Invoke($"{friendlyName} is already running.");
                        return;
                    }
                }
                catch (InvalidOperationException) { }
                catch (Exception ex)
                {
                    log?.Invoke($"Warning while starting {name}: {ex.Message}");
                }
            }

            // Fallback
            foreach (var name in candidates)
            {
                try
                {
                    var psi = new ProcessStartInfo("net", $"start \"{name}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        await p.WaitForExitAsync();
                        if (p.ExitCode == 0)
                        {
                            log?.Invoke($"{friendlyName} started via net start.");
                            return;
                        }
                    }
                }
                catch { }
            }

            log?.Invoke($"Could not start {friendlyName}. You may need to start it manually from the XAMPP Control Panel.");
        }
    }
}