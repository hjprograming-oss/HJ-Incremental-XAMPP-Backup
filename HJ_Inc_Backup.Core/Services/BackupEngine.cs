using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HJ_Inc_Backup.Models;

namespace HJ_Inc_Backup.Services
{
    public class BackupEngine
    {
        public const string BackupExtension = ".hjbak";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public event Action<string>? LogMessage;
        public event Action<double, string>? ProgressChanged;

        /// <summary>
        /// Smart backup used by both UI and Windows Service.
        /// - Creates a Full if none exists for today
        /// - Otherwise creates an Incremental (max 24 per day)
        /// </summary>
        public async Task<string?> RunScheduledBackupAsync(
            string sourceRoot,
            string destinationRoot,
            CancellationToken cancellationToken = default)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string dayFolder = Path.Combine(destinationRoot, today);
            Directory.CreateDirectory(dayFolder);

            var existing = Directory.GetFiles(dayFolder, "*" + BackupExtension)
                                    .Select(Path.GetFileName)
                                    .Where(f => f != null)
                                    .Cast<string>()
                                    .ToList();

            bool hasFull = existing.Any(f => f.Contains("_Full", StringComparison.OrdinalIgnoreCase));
            int incCount = existing.Count(f => f.Contains("_Inc", StringComparison.OrdinalIgnoreCase));

            if (!hasFull)
            {
                Log($"No Full backup for {today} yet → creating Full.");
                return await RunBackupAsync(sourceRoot, destinationRoot, isIncremental: false, cancellationToken);
            }

            if (incCount >= 24)
            {
                Log($"Already have 24 Incrementals for {today}. Skipping.");
                return null;
            }

            // Only create an Inc if at least ~55 minutes have passed since the last backup in this folder
            var lastWrite = existing
                .Select(f => new FileInfo(Path.Combine(dayFolder, f)))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .FirstOrDefault();

            if (lastWrite != null && (DateTime.UtcNow - lastWrite.LastWriteTimeUtc).TotalMinutes < 55)
            {
                Log("Last backup was less than 55 minutes ago. Skipping.");
                return null;
            }

            Log($"Creating Incremental #{incCount + 1} for {today}.");
            return await RunBackupAsync(sourceRoot, destinationRoot, isIncremental: true, cancellationToken);
        }

        public async Task<string> RunBackupAsync(
            string sourceRoot,
            string destinationRoot,
            bool isIncremental,
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(sourceRoot))
                throw new DirectoryNotFoundException($"Source folder not found: {sourceRoot}");

            Directory.CreateDirectory(destinationRoot);

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string dayFolder = Path.Combine(destinationRoot, today);
            Directory.CreateDirectory(dayFolder);

            string timePart = DateTime.Now.ToString("HHmm");
            string backupType = isIncremental ? "Inc" : "Full";
            string backupFileName = $"{today}_{timePart}_{backupType}{BackupExtension}";
            string backupFilePath = Path.Combine(dayFolder, backupFileName);

            string tempFolder = Path.Combine(Path.GetTempPath(), "HJ_Inc_Backup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempFolder);

            Log($"Creating backup archive: {today}\\{backupFileName}");

            VssSnapshotService? vss = null;
            string effectiveSourceRoot = sourceRoot;
            bool usedVss = false;

            try
            {
                try
                {
                    Log("Creating Volume Shadow Copy...");
                    vss = new VssSnapshotService();
                    vss.CreateSnapshot(sourceRoot);
                    effectiveSourceRoot = vss.MapToSnapshotPath(sourceRoot);
                    usedVss = true;
                    Log($"Shadow copy ready. Reading from: {effectiveSourceRoot}");
                }
                catch (Exception ex)
                {
                    Log($"WARNING: VSS failed ({ex.Message}). Falling back to direct copy.");
                    vss?.Dispose();
                    vss = null;
                    effectiveSourceRoot = sourceRoot;
                    usedVss = false;
                }

                Dictionary<string, FileEntry> previousFiles = new();
                if (isIncremental)
                {
                    previousFiles = LoadLatestCatalog(destinationRoot);
                    Log($"Loaded previous catalog with {previousFiles.Count} files.");
                }

                Log("Scanning source files...");
                var allFiles = await Task.Run(() => CollectFiles(effectiveSourceRoot), cancellationToken);
                Log($"Found {allFiles.Count} files.");

                var filesToCopy = new List<(string FullPath, string RelativePath, FileInfo Info)>();

                foreach (var file in allFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string relative = Path.GetRelativePath(effectiveSourceRoot, file.FullName)
                                          .Replace('\\', '/');

                    bool needsCopy = true;
                    if (isIncremental && previousFiles.TryGetValue(relative, out var prev))
                    {
                        if (file.Length == prev.Size &&
                            file.LastWriteTimeUtc == prev.LastWriteUtc)
                        {
                            needsCopy = false;
                        }
                    }

                    if (needsCopy)
                        filesToCopy.Add((file.FullName, relative, file));
                }

                Log($"Files to include: {filesToCopy.Count}");

                var newManifest = new BackupManifest
                {
                    BackupId = Guid.NewGuid().ToString("N"),
                    CreatedUtc = DateTime.UtcNow,
                    IsFull = !isIncremental,
                    SourceRoot = sourceRoot,
                    BackupFolderName = backupFileName
                };

                int total = filesToCopy.Count;
                int current = 0;

                foreach (var item in filesToCopy)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    current++;
                    double percent = total == 0 ? 50.0 : (current * 50.0) / total;

                    string destFile = Path.Combine(tempFolder,
                        item.RelativePath.Replace('/', Path.DirectorySeparatorChar));

                    string? destDir = Path.GetDirectoryName(destFile);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);

                    ProgressChanged?.Invoke(percent, item.RelativePath);

                    await Task.Run(() =>
                    {
                        using var src = new FileStream(item.FullPath, FileMode.Open, FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete, 1024 * 1024);
                        using var dst = new FileStream(destFile, FileMode.Create, FileAccess.Write,
                            FileShare.None, 1024 * 1024);
                        src.CopyTo(dst);
                    }, cancellationToken);

                    newManifest.Files[item.RelativePath] = new FileEntry
                    {
                        RelativePath = item.RelativePath,
                        Size = item.Info.Length,
                        LastWriteUtc = item.Info.LastWriteTimeUtc
                    };
                }

                if (isIncremental)
                {
                    foreach (var kvp in previousFiles)
                    {
                        if (!newManifest.Files.ContainsKey(kvp.Key))
                            newManifest.Files[kvp.Key] = kvp.Value;
                    }
                }

                string manifestPath = Path.Combine(tempFolder, "manifest.json");
                string json = JsonSerializer.Serialize(newManifest, JsonOptions);
                await File.WriteAllTextAsync(manifestPath, json, cancellationToken);

                Log("Compressing into .hjbak archive...");
                ProgressChanged?.Invoke(55, "Compressing...");

                if (File.Exists(backupFilePath))
                    File.Delete(backupFilePath);

                await Task.Run(() =>
                {
                    ZipFile.CreateFromDirectory(
                        tempFolder,
                        backupFilePath,
                        CompressionLevel.Optimal,
                        includeBaseDirectory: false);
                }, cancellationToken);

                string catalogPath = Path.Combine(destinationRoot, "latest_catalog.json");
                await File.WriteAllTextAsync(catalogPath, json, cancellationToken);

                ProgressChanged?.Invoke(100, "Done");

                Log(usedVss
                    ? "Backup completed using Volume Shadow Copy (no downtime)."
                    : "Backup completed (direct copy).");
                Log($"Archive saved: {backupFilePath}");

                return backupFilePath;
            }
            finally
            {
                vss?.Dispose();
                try
                {
                    if (Directory.Exists(tempFolder))
                        Directory.Delete(tempFolder, true);
                }
                catch { }
            }
        }

        private List<FileInfo> CollectFiles(string root)
        {
            var result = new List<FileInfo>();
            CollectRecursive(new DirectoryInfo(root), result, new[] { "tmp", "temp", ".git", "node_modules" });
            return result;
        }

        private void CollectRecursive(DirectoryInfo dir, List<FileInfo> list, string[] skipFolders)
        {
            try
            {
                foreach (var file in dir.GetFiles())
                    list.Add(file);

                foreach (var sub in dir.GetDirectories())
                {
                    if (skipFolders.Any(s => sub.Name.Equals(s, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    CollectRecursive(sub, list, skipFolders);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }
            catch (IOException) { }
        }

        private Dictionary<string, FileEntry> LoadLatestCatalog(string destinationRoot)
        {
            string catalogPath = Path.Combine(destinationRoot, "latest_catalog.json");
            if (!File.Exists(catalogPath))
                return new Dictionary<string, FileEntry>();

            try
            {
                string json = File.ReadAllText(catalogPath);
                var manifest = JsonSerializer.Deserialize<BackupManifest>(json, JsonOptions);
                return manifest?.Files ?? new Dictionary<string, FileEntry>();
            }
            catch
            {
                return new Dictionary<string, FileEntry>();
            }
        }

        private void Log(string message) => LogMessage?.Invoke(message);
    }
}