namespace HJ_Inc_Backup.Models
{
    public class BackupManifest
    {
        public string BackupId { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public bool IsFull { get; set; }
        public string SourceRoot { get; set; } = string.Empty;
        public string BackupFolderName { get; set; } = string.Empty;

        /// <summary>
        /// Relative path → FileEntry
        /// </summary>
        public Dictionary<string, FileEntry> Files { get; set; } = new();
    }
}