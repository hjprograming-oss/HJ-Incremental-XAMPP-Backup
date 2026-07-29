using HJ_Inc_Backup.Services;

namespace HJ_Inc_Backup.Service
{
    public class BackupSettings
    {
        public string SourcePath { get; set; } = @"C:\xampp";
        public string DestinationPath { get; set; } = @"C:\XamppBackups";
        public int IntervalMinutes { get; set; } = 60;
    }

    public class BackupWorker : BackgroundService
    {
        private readonly ILogger<BackupWorker> _logger;
        private readonly BackupSettings _settings;

        public BackupWorker(ILogger<BackupWorker> logger, IConfiguration config)
        {
            _logger = logger;
            _settings = new BackupSettings();
            config.GetSection("Backup").Bind(_settings);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("HJ XAMPP Backup Service started.");
            _logger.LogInformation("Source : {Source}", _settings.SourcePath);
            _logger.LogInformation("Dest   : {Dest}", _settings.DestinationPath);
            _logger.LogInformation("Interval: every {Min} minutes", _settings.IntervalMinutes);

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOneCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Backup cycle failed.");
                }

                await Task.Delay(TimeSpan.FromMinutes(_settings.IntervalMinutes), stoppingToken);
            }

            _logger.LogInformation("HJ XAMPP Backup Service stopped.");
        }

        private async Task RunOneCycleAsync(CancellationToken ct)
        {
            _logger.LogInformation("=== Scheduled backup cycle starting ===");

            var engine = new BackupEngine();
            engine.LogMessage += msg => _logger.LogInformation("{Msg}", msg);

            string? result = await engine.RunScheduledBackupAsync(
                _settings.SourcePath,
                _settings.DestinationPath,
                ct);

            if (result == null)
                _logger.LogInformation("No backup needed this cycle.");
            else
                _logger.LogInformation("Backup created: {Path}", result);
        }
    }
}