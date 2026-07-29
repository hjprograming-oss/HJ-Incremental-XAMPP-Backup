using HJ_Inc_Backup.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "HJ XAMPP Incremental Backup";
});

builder.Services.AddHostedService<BackupWorker>();

var host = builder.Build();
host.Run();