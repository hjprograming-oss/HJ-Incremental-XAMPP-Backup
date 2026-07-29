using System;
using System.IO;
using Alphaleonis.Win32.Vss;

namespace HJ_Inc_Backup.Services
{
    /// <summary>
    /// Creates a Volume Shadow Copy for reading locked files.
    /// Uses a writer-less context so it works from a Windows Service
    /// (avoids IVssWriterCallback 0x80070005 Access denied).
    /// </summary>
    public sealed class VssSnapshotService : IDisposable
    {
        private IVssBackupComponents? _backup;
        private Guid _snapshotSetId;
        private Guid _snapshotId;
        private string? _snapshotDevice;
        private string? _originalVolume;

        public bool IsActive => !string.IsNullOrEmpty(_snapshotDevice);

        public string CreateSnapshot(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                throw new ArgumentException("Source path is empty.", nameof(sourcePath));

            string volume = Path.GetPathRoot(Path.GetFullPath(sourcePath))
                            ?? throw new InvalidOperationException("Cannot determine volume root.");

            if (!volume.EndsWith("\\", StringComparison.Ordinal))
                volume += "\\";

            _originalVolume = volume;

            var factory = VssFactoryProvider.Default.GetVssFactory();
            _backup = factory.CreateVssBackupComponents();

            _backup.InitializeForBackup(null);

            // Writer-less context — critical for services (no IVssWriterCallback)
            _backup.SetContext(VssSnapshotContext.FileShareBackup);

            _backup.SetBackupState(
                selectComponents: false,
                backupBootableSystemState: false,
                backupType: VssBackupType.Full,
                partialFileSupport: false);

            // Do NOT call GatherWriterMetadata() — that triggers the access-denied callback path

            _snapshotSetId = _backup.StartSnapshotSet();
            _snapshotId = _backup.AddToSnapshotSet(volume);

            // PrepareForBackup can still talk to writers on some systems; skip if it fails
            try
            {
                _backup.PrepareForBackup();
            }
            catch
            {
                // Continue — FileShareBackup often works without prepare
            }

            _backup.DoSnapshotSet();

            var props = _backup.GetSnapshotProperties(_snapshotId);
            _snapshotDevice = props.SnapshotDeviceObject;

            if (string.IsNullOrEmpty(_snapshotDevice))
                throw new InvalidOperationException("Failed to obtain snapshot device path.");

            return _snapshotDevice;
        }

        public string MapToSnapshotPath(string originalPath)
        {
            if (!IsActive || _originalVolume == null || _snapshotDevice == null)
                throw new InvalidOperationException("No active snapshot.");

            string full = Path.GetFullPath(originalPath);

            if (!full.StartsWith(_originalVolume, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Path is not on the snapshotted volume.");

            string relative = full.Substring(_originalVolume.Length);
            return Path.Combine(_snapshotDevice, relative);
        }

        public void Dispose()
        {
            try
            {
                if (_backup != null)
                {
                    try
                    {
                        _backup.DeleteSnapshotSet(_snapshotSetId, forceDelete: true);
                    }
                    catch { }

                    _backup.Dispose();
                    _backup = null;
                }
            }
            catch { }

            _snapshotDevice = null;
            _originalVolume = null;
        }
    }
}