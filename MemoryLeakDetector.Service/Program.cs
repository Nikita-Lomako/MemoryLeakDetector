using MemoryLeakDetector.Core.Extensions;
using System;
using MemoryLeakDetector.Core.Options;
using MemoryLeakDetector.Service;
using MemoryLeakDetector.Service.Services;
using System.Runtime.Versioning;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection("Monitoring"));
builder.Services.Configure<MonitoringPipeOptions>(builder.Configuration.GetSection("MonitoringPipe"));
builder.Services.AddMemoryLeakDetectorCore();
builder.Services.AddHostedService<Worker>();
if (OperatingSystem.IsWindows())
{
    builder.Services.AddHostedService<NamedPipeMonitoringPublisher>();
}

var host = builder.Build();
host.Run();

[SupportedOSPlatform("windows")]
public partial class Program { }