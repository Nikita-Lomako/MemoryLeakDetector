using MemoryLeakDetector.Core.Extensions;
using MemoryLeakDetector.Core.Options;
using MemoryLeakDetector.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection("Monitoring"));
builder.Services.AddMemoryLeakDetectorCore();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
