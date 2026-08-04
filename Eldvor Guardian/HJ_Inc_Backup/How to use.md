# Eldvor Guardian — Complete How-To Guide

---

## Project Structure (3 Projects)

| Project | Path | Purpose |
|---------|------|---------|
| **Eldvor Guardian** | `HJ_Inc_Backup_source/HJ Inc Backup/` | WPF Desktop GUI — manual backups, restores, service management |
| **Eldvor Guardian.Core** | `HJ_Inc_Backup_source/HJ_Inc_Backup.Core/` | Shared library — backup/restore engines, VSS, service controllers |
| **Eldvor Guardian.Service** | `HJ_Inc_Backup_source/HJ_Inc_Backup.Service/` | Windows Service — automated scheduled backups |

---

## 📘 HOW TO USE — Desktop GUI (Manual Operation)

### 1. Launching
- Build & run `Eldvor Guardian` WPF project (requires **Administrator rights** — manifest enforces `requireAdministrator`)
- Main window opens with status "Ready"

### 2. Source & Destination Configuration

| Field | Default | Description |
|-------|---------|-------------|
| **XAMPP Root Folder** (Source) | `C:\xampp` | The folder to back up |
| **Backup Destination** | `Documents\XamppBackups` | Where `.hjbak` archives are saved |

Click **Browse...** to change either path.

### 3. Performing a Backup

| Button | Behavior |
|--------|----------|
| **Full Backup** | Captures ALL files from source → creates `{date}_{time}_Full.hjbak`. Stops the backup Windows service temporarily, then restarts it. Uses Volume Shadow Copy (VSS) for locked files. |
| **Incremental Backup** | Compares current files against `latest_catalog.json` — only archives new/changed files (by size + LastWrite time). Does NOT stop the backup service. |
| **Cancel** | Cancels the running backup/restore operation |

### 4. Restoring a Backup

Click **Restore...** → Opens Restore Window:

1. **Select Backup**: Lists all `.hjbak` files found in backup destination. Shows Date/Time, Type (Full/Inc), file count, and filename.
2. **Target Path**: Defaults to your XAMPP root — change via **Browse...**
3. **Option**: "Stop XAMPP services before restore and start them afterwards" (checked by default)
4. **Click "Restore Selected"**:
   - If **Incremental** → warning appears: only files inside this archive will be restored. You should restore a Full backup first, then overlay incrementals.
   - Confirmation dialog → "This will overwrite files in: {target}"
   - The `.hjbak` is extracted to `%TEMP%`, files copied to target, temp cleaned up
   - XAMPP Apache + MySQL/MariaDB services are stopped before restore and started after (if enabled)

### 5. Windows Service Management (in GUI)

| Button | Action |
|--------|--------|
| **Install** | Copies service files to `{AppDir}\Service\` and registers with `sc.exe create` |
| **Uninstall** | Stops + unregisters service via `sc.exe delete` |
| **Start** | Starts the registered Windows service |
| **Stop** | Stops the running Windows service |

---

## 🤖 HOW TO USE — Windows Service (Automated Backups)

### Configuration (`appsettings.json` in Service project)

```json
{
  "Backup": {
    "SourcePath": "C:\\xampp",
    "DestinationPath": "C:\\XamppBackups",
    "IntervalMinutes": 60
  }
}
```

### How the Scheduled Backup Cycle Works (`RunScheduledBackupAsync` in `BackupEngine.cs`)

1. Creates a `yyyy-MM-dd` subfolder under the destination
2. Checks existing backups for today:
   - **No Full backup** → Creates a **Full backup**
   - **Full exists, < 24 incrementals** → Creates an **Incremental** (only if ≥ 55 minutes since last backup)
   - **Full exists, ≥ 24 incrementals** → Skips cycle
3. Repeats every `IntervalMinutes`

### Installing the Service

**Option A** — Via the Desktop GUI: Click **Install** button in the Service section

**Option B** — Manually:
1. Publish the `Eldvor Guardian.Service` project
2. Run: `sc.exe create "Eldvor Guardian Backup" binPath= "C:\path\to\published\Eldvor Guardian.Service.exe" start= auto`

---

## 📁 BACKUP ARCHIVE FORMAT

Files are stored as `.hjbak` archives (standard ZIP format). Inside each:

```
manifest.json              → JSON manifest (BackupManifest)
htdocs/index.php           → relative paths preserved
apache/conf/httpd.conf
xampp-control.ini
... (all other files)
```

At the destination root, a **`latest_catalog.json`** is maintained — this is the manifest of the most recent backup, used for incremental comparisons.

---

## ✏️ HOW TO MODIFY / CUSTOMIZE

### 1. Change Skipped Directories
**File**: `BackupEngine.cs` → `CollectFiles()` method
```csharp
CollectRecursive(dir, result, new[] { "tmp", "temp", ".git", "node_modules" });
```
Add/remove folder names as needed.

### 2. Change Incremental Cooldown (55-minute gap)
**File**: `BackupEngine.cs` → `RunScheduledBackupAsync()`
```csharp
if (lastWrite != null && (DateTime.UtcNow - lastWrite.LastWriteTimeUtc).TotalMinutes < 55)
```
Change `55` to your desired minimum interval in minutes.

### 3. Change Max Incrementals Per Day (24 limit)
**File**: `BackupEngine.cs` → `RunScheduledBackupAsync()`
```csharp
if (incCount >= 24)
```
Change `24` to your desired maximum.

### 4. Change Backup Extension (`.hjbak`)
**File**: `BackupEngine.cs`
```csharp
public const string BackupExtension = ".hjbak";
```

### 5. Change XAMPP Service Names (Apache/MySQL detection)
**File**: `XamppServiceController.cs` → arrays at top
```csharp
private static readonly string[] ApacheCandidates = { "Apache2.4", "Apache", "xamppapache" };
private static readonly string[] MysqlCandidates = { "mysql", "MySQL", "mariadb", "xamppmysql" };
```
Add/remove as needed for your XAMPP version.

### 6. Change Service Interval
**File**: `Worker.cs` (in Service project) → or edit `appsettings.json`:
```json
"IntervalMinutes": 60
```

### 7. Change Default Paths
- **UI defaults**: `MainWindow.xaml.cs` constructor:
  ```csharp
  TxtSourcePath.Text = @"C:\xampp";
  TxtDestPath.Text = Path.Combine(..., "XamppBackups");
  ```
- **Service defaults**: `Worker.cs` → `BackupSettings` class:
  ```csharp
  public string SourcePath { get; set; } = @"C:\xampp";
  public string DestinationPath { get; set; } = @"C:\XamppBackups";
  ```

### 8. Change Windows Service Name
**File**: `BackupServiceController.cs`
```csharp
public const string ServiceName = "HJ XAMPP Incremental Backup";
```
**File**: `Program.cs` (Service project)
```csharp
options.ServiceName = "HJ XAMPP Incremental Backup";
```
Both must match.

### 9. Disable VSS (Volume Shadow Copy)
VSS fallback is automatic — if VSS fails, direct file copy is used. To force disable VSS, remove/comment the VSS block in `BackupEngine.cs` → `RunBackupAsync()`.

### 10. Add Custom File Inclusion/Exclusion
**File**: `BackupEngine.cs` → `CollectRecursive()` method — add exclude logic (e.g., file extension filters, path pattern matching).

---

## 🛠️ BUILD & DEVELOPMENT

- **Framework**: .NET 10 (Windows)
- **Platform**: x64 (recommended)
- **Build**: Open `HJ Inc Backup.slnx` in Visual Studio 2022+ → Build Solution
- **Run**: Must be **Run as Administrator** (VSS + service management elevation required)
- **Publish Service**: `dotnet publish` on the Service project, or use the FolderProfile publish profile

---

## 📝 LOGGING & TROUBLESHOOTING

| Component | Where logs appear |
|-----------|------------------|
| **Desktop GUI** | Bottom panel in Main Window (TxtLog TextBox) |
| **Windows Service** | Windows Event Log → Application → source "Eldvor Guardian Backup" |

### Common Issues

| Issue | Cause / Fix |
|-------|-------------|
| "VSS failed" | Falls back to direct copy. Files locked by Apache/MySQL may be skipped. Stop XAMPP services manually first. |
| "Could not find service files" | Publish the Service project first, or place compiled files in `ServicePayload` folder next to the app `.exe` |
| Access Denied | Run the app as **Administrator** |
| Backup service not starting | Check Event Log for errors, verify `appsettings.json` paths exist |
| Incremental warning during restore | Restore Full backup first, then overlay Incrementals |

