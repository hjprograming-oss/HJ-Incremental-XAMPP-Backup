namespace HJ_Inc_Backup.Models
{
    public class FileEntry
    {
        public string RelativePath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastWriteUtc { get; set; }
        public string? Sha256 { get; set; }   // optional – we will fill it later if needed
    }
}