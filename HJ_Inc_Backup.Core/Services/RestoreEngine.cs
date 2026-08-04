using HJ_Inc_Backup.Models;
using System.IO.Compression;
using System.Text.Json;

namespace HJ_Inc_Backup.Services
{
    public class RestoreEngine
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public event Action<string>? LogMessage;
        public event Action<double, string>? ProgressChanged;

        public async Task RestoreAsync(
            string backupArchivePath,          // now a .hjbak file
            string targetRoot,
            bool stopAndStartServices = true,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(backupArchivePath))
                throw new FileNotFoundException("Backup archive not found.", backupArchivePath);

            if (!backupArchivePath.EndsWith(BackupEngine.BackupExtension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Expected a {BackupEngine.BackupExtension} file.");

            string tempFolder = Path.Combine(Path.GetTempPath(), "HJ_Restore_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            try
            {
                Log($"Extracting archive: {Path.GetFileName(backupArchivePath)}");
                ProgressChanged?.Invoke(5, "Extracting archive...");

                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(backupArchivePath, tempFolder, overwriteFiles: true);
                }, cancellationToken);

                string manifestPath = Path.Combine(tempFolder, "manifest.json");
                if (!File.Exists(manifestPath))
                    throw new FileNotFoundException("manifest.json not found inside the archive.");

                string json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                var manifest = JsonSerializer.Deserialize<BackupManifest>(json, JsonOptions)
                               ?? throw new InvalidOperationException("Could not read manifest.json");

                Log($"Restoring: {manifest.BackupFolderName}");
                Log($"Created  : {manifest.CreatedUtc:yyyy-MM-dd HH:mm:ss} UTC");
                Log($"Type     : {(manifest.IsFull ? "Full" : "Incremental")}");
                Log($"Files in archive: {manifest.Files.Count}");

                if (!manifest.IsFull)
                {
                    Log("WARNING: This is an Incremental backup.");
                    Log("Only the files contained in this archive will be restored.");
                }

                Directory.CreateDirectory(targetRoot);

                if (stopAndStartServices)
                    await XamppServiceController.StopServicesAsync(msg => Log(msg));

                try
                {
                    var filesOnDisk = Directory.GetFiles(tempFolder, "*", SearchOption.AllDirectories);
                    var filesToRestore = new System.Collections.Generic.List<string>();

                    foreach (var f in filesOnDisk)
                    {
                        if (Path.GetFileName(f).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                            continue;
                        filesToRestore.Add(f);
                    }

                    int total = filesToRestore.Count;
                    int current = 0;

                    Log($"Copying {total} files to {targetRoot}...");

                    foreach (var sourceFile in filesToRestore)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        current++;
                        double percent = total == 0 ? 100.0 : 10 + (current * 85.0) / total;

                        string relative = Path.GetRelativePath(tempFolder, sourceFile);
                        string destFile = Path.Combine(targetRoot, relative);

                        string? destDir = Path.GetDirectoryName(destFile);
                        if (!string.IsNullOrEmpty(destDir))
                            Directory.CreateDirectory(destDir);

                        ProgressChanged?.Invoke(percent, relative);

                        await Task.Run(() => File.Copy(sourceFile, destFile, overwrite: true), cancellationToken);
                    }

                    Log("File restore completed.");
                }
                finally
                {
                    if (stopAndStartServices)
                        await XamppServiceController.StartServicesAsync(msg => Log(msg));
                }

                ProgressChanged?.Invoke(100, "Done");
                Log("Restore finished successfully.");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempFolder))
                        Directory.Delete(tempFolder, recursive: true);
                }
                catch { /* ignore */ }
            }
        }

        private void Log(string message) => LogMessage?.Invoke(message);
    }
}