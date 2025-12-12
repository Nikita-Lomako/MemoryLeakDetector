// Подключаем необходимые пространства имен для работы с Core, конфигурацией и сервисами
using MemoryLeakDetector.Core.Extensions; // Расширения для регистрации сервисов Core
using System; // Базовые типы .NET
using MemoryLeakDetector.Core.Options; // Классы конфигурации (MonitoringOptions, MonitoringPipeOptions)
using MemoryLeakDetector.Service; // Классы сервиса (Worker)
using MemoryLeakDetector.Service.Services; // Сервисы для IPC (NamedPipeMonitoringPublisher)
using System.Runtime.Versioning; // Атрибуты для указания поддерживаемых платформ

// Создаем builder для настройки приложения-хоста (Worker Service)
// Host.CreateApplicationBuilder автоматически загружает конфигурацию из appsettings.json
var builder = Host.CreateApplicationBuilder(args);

// Регистрируем конфигурацию из appsettings.json в DI контейнер
// MonitoringOptions - параметры мониторинга (интервалы, пороги утечек и т.д.)
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection("Monitoring"));
// MonitoringPipeOptions - параметры Named Pipe для связи с UI (имя канала, таймауты)
builder.Services.Configure<MonitoringPipeOptions>(builder.Configuration.GetSection("MonitoringPipe"));

// Регистрируем все сервисы из Core проекта (коллекторы метрик, baseline, стратегии обнаружения и т.d.)
// Этот метод расширения добавляет все необходимые зависимости для работы системы мониторинга
builder.Services.AddMemoryLeakDetectorCore();

// Регистрируем основной фоновый worker, который выполняет циклы мониторинга
// HostedService запускается автоматически при старте приложения и работает в фоне
builder.Services.AddHostedService<Worker>();

// Named Pipe сервер нужен только на Windows (механизм IPC специфичен для Windows)
if (OperatingSystem.IsWindows())
{
    // Регистрируем фоновый сервис для публикации результатов мониторинга через Named Pipes
    // UI приложение подключается к этому серверу для получения данных в реальном времени
    builder.Services.AddHostedService<NamedPipeMonitoringPublisher>();
}

// Собираем host приложения со всеми зарегистрированными сервисами
var host = builder.Build();

// Запускаем host - это запустит все HostedService (Worker и NamedPipeMonitoringPublisher)
// Приложение будет работать до получения сигнала остановки (Ctrl+C или остановка службы)
host.Run();

/// <summary>
/// Маркерный класс для указания поддерживаемой платформы.
/// Атрибут SupportedOSPlatform указывает, что приложение работает только на Windows
/// (из-за использования Named Pipes и Performance Counters, специфичных для Windows).
/// </summary>
[SupportedOSPlatform("windows")]
public partial class Program { }