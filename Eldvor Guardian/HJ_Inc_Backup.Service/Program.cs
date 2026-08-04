using HJ_Inc_Backup.Service;
using HJ_Inc_Backup.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = BackupServiceController.ServiceName;
});

builder.Services.AddHostedService<BackupWorker>();

var host = builder.Build();
host.Run();