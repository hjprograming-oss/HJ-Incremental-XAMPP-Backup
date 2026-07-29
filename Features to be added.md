# Features to Be Added — Backup System

This document catalogs all enhancements needed to evolve HJ Incremental XAMPP Backup into a production-grade, professional backup solution.

---

## 🎯 HIGH PRIORITY — Core Professional Features

### 1. Chain Restore (Full + Incremental Merged Restore)
**Current**: User must restore Full first, then each Incremental manually. Incremental restore only overwrites files in that archive.
**Required**: 
- Automatic "Chain Restore" that selects a Full backup and all subsequent Incrementals (by date sequence)
- Restore engine reads all manifests in order, overlays files chronologically to reconstruct the exact state at any point in time
- One-click restore to latest state
- "Restore to specific point-in-time" — pick a date/time and the system reconstructs from the relevant chain

### 2. Service Source/Destination Path Management from GUI
**Current**: Backup Windows Service reads paths from `appsettings.json` — must edit file manually.
**Required**:
- Add "Service Settings" panel or tab in the GUI:
  - Source Path text field (linked to service config)
  - Destination Path text field (linked to service config)
  - Interval minutes spinner/dropdown (e.g., 30, 60, 120, 240, 1440)
- "Apply" button that writes `appsettings.json` to the service install directory and restarts the service
- "Reload from Service" button to read current settings

### 3. Title / Branding Customization
**Current**: Hardcoded title "HJ Incremental XAMPP Backup" in `MainWindow.xaml`.
**Required**:
- Configurable application title via settings file or registry
- Version number displayed in title bar: `"HJ Incremental XAMPP Backup v{version}"`
- Build number auto-increment or manual version entry in settings
- Ability to set a custom backup set name (e.g., "Production Server", "Dev Environment")

### 4. Retention Policy / Backup Rotation
**Current**: No automatic cleanup — backups accumulate forever.
**Required**:
- Configurable retention policies:
  - **By count**: Keep last N full backups + their incrementals
  - **By age**: Delete backups older than X days
  - **By space**: Delete oldest backups when destination exceeds Y GB
- Scheduled cleanup job in the service (runs weekly)
- GUI panel to configure retention rules
- Preview of what will be deleted before executing cleanup

### 5. Backup Schedule Configuration (Granular)
**Current**: Single `IntervalMinutes` setting — runs every N minutes.
**Required**:
- Full scheduler:
  - **Daily Full** at configurable time (e.g., 2:00 AM)
  - **Hourly Incrementals** during working hours (e.g., 8 AM – 6 PM Mon–Fri)
  - **Skip weekends / holidays** option
  - **Calendar-based exceptions** (no backups on specific dates)
- Cron-expression-like UI or simple time-of-day pickers
- Next-scheduled-run preview display

---

## 🔧 HIGH PRIORITY — Operational Excellence

### 6. Email / Notification Alerts
**Current**: Logs only to Event Log and GUI text box.
**Required**:
- SMTP email notifications on:
  - Backup success / failure
  - Restore completion
  - Disk space warnings
  - Service stopped unexpectedly
- Configurable: to, from, server, credentials, SSL/TLS
- Optional: send only on failures

### 7. Dashboard / Backup History View
**Current**: Only the Restore window shows a backup list. No history overview.
**Required**:
- Dedicated "Backup History" tab/window with:
  - Sortable/filterable grid: Date, Type (Full/Inc), Size, Duration, Status (Success/Failed), File count
  - Color coding: green = success, red = failed, yellow = warning
  - Search/filter by date range
- Backup statistics:
  - Total backups taken
  - Total data backed up (cumulative)
  - Average backup duration
  - Success rate percentage

### 8. Comprehensive Logging & Audit Trail
**Current**: Simple in-memory log in the GUI, EventLog for the service.
**Required**:
- Persistent log files in the destination folder: `logs/YYYY-MM-DD.log`
- Structured logging (JSON lines) for machine parsing
- Log levels: Debug, Info, Warning, Error
- Rotation: auto-delete logs older than 90 days
- Audit trail per backup file: who triggered it (manual/service), duration, file count, size

### 9. Progress Estimation & ETA
**Current**: Shows percentage complete based on file count.
**Required**:
- File-size-based progress (instead of file-count-based) for accurate percentage
- Estimated time remaining (ETA) calculated from throughput rate
- Bytes copied / total bytes display
- Current file being processed with transfer speed (MB/s)

---

## 🏗️ MEDIUM PRIORITY — UI/UX Improvements

### 10. Dark Mode / Theme Support
**Required**:
- System theme detection (Windows dark/light mode)
- Manual toggle between Light / Dark / System themes
- Proper contrast for all UI elements in both themes

### 11. Minimize to System Tray
**Required**:
- Minimize button → hide to system tray icon
- System tray context menu: "Open", "Backup Now (Full)", "Backup Now (Incremental)", "Exit"
- Toast notifications for backup completion (Windows native toast)
- Option to start minimized

### 12. Backup Verification (Integrity Check)
**Required**:
- After backup creation, verify archive integrity:
  - Extract manifest.json and validate JSON
  - Checksum a random sample of files (SHA-256) and compare with originals
  - Test that ZIP is not corrupt
- Bad backup detection with automatic retry (once)
- Verification badge/icon in backup history

### 13. One-Click Restore from Main Window
**Current**: Must click "Restore..." → browse → select → confirm.
**Required**:
- "Restore Latest" button that immediately restores the most recent full backup chain
- "Quick Restore" dropdown showing last 5 backups for instant selection
- Options dialog instead of full Restore Window for quick operations

### 14. Bandwidth Throttling
**Required**:
- Configurable I/O throttle for backups (e.g., "Slow" / "Normal" / "Fast")
- Limit read/write speed to avoid saturating disk during business hours
- CPU priority setting for the backup process (Idle/BelowNormal/Normal)

---

## 🛡️ MEDIUM PRIORITY — Reliability & Safety

### 15. Pre/Post Backup Scripts (Hooks)
**Required**:
- Configurable command-line scripts to run:
  - **Pre-backup script**: e.g., dump database, flush cache, notify other systems
  - **Post-backup script**: e.g., upload to cloud, trigger secondary backup
- Script timeout setting
- If pre-script fails → optionally abort backup

### 16. Backup Lock / Run Protection
**Required**:
- Prevent concurrent backup runs (already partially handled via `_isBusy`)
- Detect if another instance is already running (mutex)
- Crash recovery: if app crashes mid-backup, clean up temp files on next launch

### 17. Destination Space Check
**Required**:
- Before starting backup, estimate size and check available disk space
- Warn if destination has < 10% free space
- Abort backup if estimated size exceeds available space
- Low-space alert notification (even from service)

### 18. File Exclude/Include Patterns
**Current**: Only `tmp`, `temp`, `.git`, `node_modules` are skipped (hardcoded).
**Required**:
- GUI pattern editor with:
  - Include patterns (e.g., only `htdocs/*`, `apache/conf/*`)
  - Exclude patterns (e.g., `*.log`, `tmp/*`, `cache/*`)
  - Glob-style and regex support
  - Built-in common presets (e.g., "XAMPP only", "All files", "Custom")
- Saved per-backup-set configuration

### 19. Database-Aware Backup (MySQL Dump)
**Current**: File-level copy only — MySQL tables may be inconsistent.
**Required**:
- Option to dump MySQL/MariaDB databases before backup:
  - Run `mysqldump` (detect path automatically from XAMPP)
  - Save `.sql` dump into a temporary location
  - Include dump in the backup archive
  - Restore option to reimport dump
- List available databases and allow selection

---

## ☁️ LOW PRIORITY — Advanced / Enterprise Features

### 20. Cloud Upload (Azure / AWS / Google / SFTP)
**Required**:
- After local backup, sync to cloud storage:
  - **Azure Blob Storage**
  - **AWS S3 / S3-compatible** (Wasabi, Backblaze)
  - **Google Drive**
  - **SFTP / SCP** to remote server
- Configurable: keep local copy, cloud-only, or sync-and-delete-local
- Encrypted transfer (TLS/SSH)

### 21. Encryption at Rest
**Required**:
- AES-256-GCM encryption of `.hjbak` archives
- Password or certificate-based encryption
- Key management: store in Windows Certificate Store or encrypted config file
- Encrypted backups labeled differently (e.g., `.hjbak.enc`)
- Decryption prompt during restore

### 22. Differential Backup Mode
**Current**: Only Full and Incremental.
**Required**:
- **Differential**: Like full, but relative to the last Full backup (not each previous backup)
- Advantage: restore = last Full + last Differential (only 2 archives)

### 23. Multi-Profile / Backup Set Management
**Required**:
- Create multiple backup profiles (e.g., "Web Server", "Database Server", "Dev Machine")
- Each profile has its own:
  - Source/Destination paths
  - Schedule
  - Include/Exclude patterns
  - Retention policy
  - Pre/post scripts
- Select active profile from dropdown
- Profiles stored as JSON files in a `profiles/` directory

### 24. Backup Statistics / Reporting
**Required**:
- Generate PDF or HTML backup reports:
  - Summary for the week/month
  - Files backed up, sizes, success rate
  - Storage usage trend chart
- Email report automatically on schedule

### 25. Portable / Standalone Mode
**Required**:
- Option to create a portable version (no install required)
- Self-contained executable with embedded config
- Useful for USB drive backups of multiple machines

### 26. Multi-Language Support (i18n)
**Required**:
- Resource-based string localization
- Detect system language or manual selection
- English, Afrikaans, and others

### 27. Command-Line Interface (CLI)
**Required**:
- Run backup operations without GUI:
  ```bash
  HJ_Inc_Backup.exe --backup --full --source "C:\xampp" --dest "D:\Backups"
  HJ_Inc_Backup.exe --restore --file "2025-01-15_1430_Full.hjbak" --target "C:\xampp"
  HJ_Inc_Backup.exe --service --install
  HJ_Inc_Backup.exe --service --start
  ```
- Exit code for scripting (0 = success, 1 = warning, 2 = error)
- Silent mode (no UI shown, use event log only)

### 28. Windows Event Log Enhancements
**Current**: Uses generic event source.
**Required**:
- Custom event sources per operation type:
  - `HJ Backup - Full`
  - `HJ Backup - Incremental`
  - `HJ Backup - Restore`
  - `HJ Backup - Service`
  - `HJ Backup - Error`
- Structured event data (JSON embedded in event details)

### 29. Auto-Update Mechanism
**Required**:
- Check for updates on startup (configurable URL)
- Download and install new version silently
- Version comparison with changelog display
- Update channel: Stable / Beta

### 30. Database of Backup Operations (SQLite)
**Current**: Relies on file system and JSON manifests.
**Required**:
- Local SQLite database to track:
  - All backup operations (history, duration, size, result)
  - Service configuration
  - Profile settings
  - Backup chain relationships (Full → Inc → Inc)
- Faster queries than scanning directories
- Exportable to JSON/CSV

---

## 🐛 BUG FIXES / TECHNICAL DEBT

| # | Issue | Impact | File(s) |
|---|-------|--------|---------|
| 1 | Incremental restore doesn't verify timestamp order | Could overlay out-of-sequence files | `RestoreEngine.cs` |
| 2 | VSS fallback message always logged as WARNING | Confusing even when VSS works fine | `BackupEngine.cs` |
| 3 | No retry logic for transient file I/O errors | Network drives or USB may fail once | `BackupEngine.cs` |
| 4 | `latest_catalog.json` could become stale if backup cancelled mid-write | Incremental could miss files | `BackupEngine.cs` |
| 5 | XAMPP service detection only tries known names | Custom XAMPP installs may not match | `XamppServiceController.cs` |
| 6 | GUI doesn't refresh service status automatically | User must guess if service state changed | `MainWindow.xaml.cs` |
| 7 | Temp files are cleaned on `finally` — but if process killed, leftovers remain | Disk space leak | `BackupEngine.cs`, `RestoreEngine.cs` |
| 8 | No timeout on VSS snapshot creation | Could hang indefinitely on some systems | `VssSnapshotService.cs` |
| 9 | No validation of paths before backup starts | Invalid paths cause cryptic errors mid-operation | `MainWindow.xaml.cs` |
| 10 | Restore window blocks main UI (ShowDialog) | Can't monitor progress while choosing restore options | `MainWindow.xaml.cs` |

---

## ✅ Summary — Roadmap Priority

```
Phase 1 (Next Release)    │  Phase 2 (Short-term)      │  Phase 3 (Long-term)
──────────────────────────┼────────────────────────────┼─────────────────────────────
Chain Restore             │  Dashboard / History       │  Cloud Upload
Service Settings in GUI   │  Retention Policy          │  Encryption
Title Customization       │  Email Notifications       │  Differential Backup
Backup Schedule Config    │  Pre/Post Scripts          │  Multi-Profile
Database-Aware Backup     │  File Patterns             │  Reporting
System Tray Minimize      │  Dark Mode                 │  CLI Interface
Progress ETA              │  Bandwidth Throttling      │  Auto-Update
Integrity Check           │  Space Check               │  Multi-Language
                          │  Audit Logging             │  SQLite Database
```

---

*Last updated: 29 July 2026*
