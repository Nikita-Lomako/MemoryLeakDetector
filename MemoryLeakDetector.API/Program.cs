using System.Reflection;
using MemoryLeakDetector.API.Services;
using MemoryLeakDetector.Core.Extensions;
using MemoryLeakDetector.Core.Options;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;
QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

var builder = WebApplication.CreateBuilder(args);

// Настройка опций мониторинга
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection("Monitoring"));

// Регистрация ядра MemoryLeakDetector (доступно только на Windows)
if (OperatingSystem.IsWindows())
{
    builder.Services.AddMemoryLeakDetectorCore();
}

// Хранилище истории и фоновый мониторинг
builder.Services.AddSingleton<IMonitoringHistoryStore>(_ => new InMemoryMonitoringHistoryStore());
builder.Services.AddHostedService<MonitoringBackgroundService>();

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "MemoryLeak Detector API",
        Version = "v1",
        Description = "API для мониторинга процессов и получения отчетов о возможных утечках памяти."
    });

    // подключаем XML-комментарии для красивой документации
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
