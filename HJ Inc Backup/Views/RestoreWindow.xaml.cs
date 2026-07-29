using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Windows;
using HJ_Inc_Backup.Models;
using HJ_Inc_Backup.Services;
using Ookii.Dialogs.Wpf;

namespace HJ_Inc_Backup
{
    public partial class RestoreWindow : Window
    {
        private readonly string _backupRoot;
        private readonly string _defaultTarget;

        public string? SelectedBackupFolder { get; private set; }   // actually the .hjbak path
        public string? SelectedTargetPath { get; private set; }
        public bool StopServices { get; private set; } = true;

        public RestoreWindow(string backupRoot, string defaultTarget)
        {
            InitializeComponent();
            _backupRoot = backupRoot;
            _defaultTarget = defaultTarget;
            TxtTargetPath.Text = defaultTarget;
            LoadBackups();
        }

        private void LoadBackups()
        {
            var items = new List<BackupListItem>();

            if (!Directory.Exists(_backupRoot))
            {
                LstBackups.ItemsSource = items;
                return;
            }

            // Scan day folders + any loose files at root (old style)
            var archives = new List<string>();

            archives.AddRange(Directory.GetFiles(_backupRoot, "*" + BackupEngine.BackupExtension));

            foreach (var dayDir in Directory.GetDirectories(_backupRoot))
            {
                archives.AddRange(Directory.GetFiles(dayDir, "*" + BackupEngine.BackupExtension));
            }

            foreach (var archive in archives.OrderByDescending(f => f))
            {
                try
                {
                    using var zip = ZipFile.OpenRead(archive);
                    var entry = zip.GetEntry("manifest.json");
                    if (entry == null) continue;

                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    string json = reader.ReadToEnd();

                    var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
                    if (manifest == null) continue;

                    items.Add(new BackupListItem
                    {
                        FolderPath = archive,
                        FolderName = Path.GetFileName(archive) ?? "",
                        DisplayDate = manifest.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                        Type = manifest.IsFull ? "Full" : "Incremental",
                        FileCount = manifest.Files?.Count ?? 0,
                        CreatedUtc = manifest.CreatedUtc
                    });
                }
                catch { }
            }

            LstBackups.ItemsSource = items.OrderByDescending(i => i.CreatedUtc).ToList();
            if (items.Count > 0)
                LstBackups.SelectedIndex = 0;
        }

        private void BtnBrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = "Select target XAMPP root folder",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog(this) == true)
                TxtTargetPath.Text = dialog.SelectedPath;
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (LstBackups.SelectedItem is not BackupListItem selected)
            {
                MessageBox.Show("Please select a backup.", "No selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string target = TxtTargetPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                MessageBox.Show("Please set the target XAMPP path.", "Missing path",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!selected.Type.Equals("Full", StringComparison.OrdinalIgnoreCase))
            {
                var warn = MessageBox.Show(
                    "You selected an Incremental backup.\n\n" +
                    "Only the files inside this archive will be restored.\n" +
                    "For a complete restore, restore the last Full backup first.\n\n" +
                    "Continue anyway?",
                    "Incremental Backup Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (warn != MessageBoxResult.Yes)
                    return;
            }

            SelectedBackupFolder = selected.FolderPath;
            SelectedTargetPath = target;
            StopServices = ChkStopServices.IsChecked == true;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class BackupListItem
    {
        public string FolderPath { get; set; } = "";
        public string FolderName { get; set; } = "";
        public string DisplayDate { get; set; } = "";
        public string Type { get; set; } = "";
        public int FileCount { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}