// Подключаем необходимые пространства имен для работы с абстракциями, конфигурацией и хо ST-сервисами
using MemoryLeakDetector.Core.Abstractions; // Интерфейсы для координации мониторинга и работы с потоками результатов
using MemoryLeakDetector.Core.Options; // Классы конфигурации
using Microsoft.Extensions.Hosting; // Базовый класс BackgroundService для фоновых задач
using Microsoft.Extensions.Logging; // Логирование
using Microsoft.Extensions.Options; // Паттерн IOptions для конфигурации

namespace MemoryLeakDetector.Service
{
    /// <summary>
    /// Основной фоновый worker, выполняющий циклы мониторинга процессов.
    /// Наследуется от BackgroundService и автоматически запускается при старте приложения.
    /// Выполняет мониторинг с заданным интервалом, публикует результаты и сохраняет историю.
    /// </summary>
    public class Worker : BackgroundService
    {
        // Логгер для записи информации о работе worker'а
        private readonly ILogger<Worker> _logger;
        
        // Координатор мониторинга - оркестрирует сбор метрик, анализ baseline и обнаружение утечек
        private readonly IMonitoringCoordinator _monitoringCoordinator;
        
        // Поток для публикации результатов мониторинга подписчикам (например, UI через Named Pipes)
        private readonly IMonitoringResultStream _resultStream;
        
        // Маппер для преобразования доменных моделей (MonitoringCycleResult) в DTO (MonitoringResultDto)
        // Для передачи через IPC между процессами
        private readonly IMonitoringResultMapper _resultMapper;
        
        // Хранилище истории результатов мониторинга для генерации отчетов
        private readonly IMonitoringHistoryStore _historyStore;
        
        // Конфигурация системы мониторинга (интервалы, пороги и т.д.)
        private readonly MonitoringOptions _options;

        /// <summary>
        /// Конструктор для внедрения зависимостей через Dependency Injection.
        /// Все зависимости предоставляются DI контейнером при создании Worker.
        /// </summary>
        public Worker(
            ILogger<Worker> logger,
            IMonitoringCoordinator monitoringCoordinator,
            IMonitoringResultStream resultStream,
            IMonitoringResultMapper resultMapper,
            IMonitoringHistoryStore historyStore,
            IOptions<MonitoringOptions> options)
        {
            // Сохраняем все зависимости для последующего использования в ExecuteAsync
            _logger = logger;
            _monitoringCoordinator = monitoringCoordinator;
            _resultStream = resultStream;
            _resultMapper = resultMapper;
            _historyStore = historyStore;
            _options = options.Value; // Извлекаем значение из IOptions wrapper
        }

        /// <summary>
        /// Основной метод выполнения фонового worker'а.
        /// Выполняется автоматически при старте приложения и работает до получения сигнала остановки.
        /// Выполняет циклы мониторинга с заданным интервалом, публикует результаты и сохраняет историю.
        /// </summary>
        /// <param name="stoppingToken">Токен отмены для корректной остановки при завершении приложения</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Вычисляем интервал задержки между циклами мониторинга из конфигурации
            // Минимальное значение - 250 мс, чтобы не создавать чрезмерную нагрузку
            var delay = TimeSpan.FromMilliseconds(Math.Max(250, _options.PollingIntervalMilliseconds));

            // Основной цикл работы worker'а - выполняется до получения сигнала остановки
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Запускаем один цикл мониторинга:
                    // 1. Сбор метрик всех процессов
                    // 2. Обновление baseline для каждого процесса
                    // 3. Анализ на наличие утечек
                    var result = await _monitoringCoordinator.RunCycleAsync(stoppingToken);
                    
                    // Преобразуем доменную модель (MonitoringCycleResult) в DTO (MonitoringResultDto)
                    // DTO используется для передачи данных через IPC (Named Pipes) между процессами
                    var dto = _resultMapper.Map(result);
                    
                    // Публикуем результаты в поток для подписчиков (например, UI через Named Pipe)
                    // UI приложение подписывается на этот поток и получает данные в реальном времени
                    await _resultStream.PublishAsync(dto, stoppingToken);
                    
                    // Сохраняем результаты в историю для последующего анализа и генерации отчетов
                    // История используется в UI для создания отчетов за выбранный период
                    _historyStore.Add(dto);

                    // Логируем результат цикла мониторинга для отслеживания работы системы
                    _logger.LogInformation(
                        "Monitoring cycle finished in {Duration} — processes: {Processes}, leaks: {Leaks}, errors: {Errors}",
                        result.Duration,
                        result.ActiveProcessCount,
                        result.LeakSuspicions,
                        result.ErrorCount);
                }
                // Обработка корректной отмены операции (Ctrl+C или остановка службы)
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Выходим из цикла для корректного завершения worker'а
                    break;
                }
                // Обработка неожиданных ошибок в цикле мониторинга
                catch (Exception ex)
                {
                    // Логируем ошибку, но продолжаем работу - отказоустойчивость
                    // Следующий цикл попытается выполниться снова
                    _logger.LogError(ex, "Monitoring cycle failed");
                }

                // Ожидание перед следующим циклом мониторинга
                try
                {
                    // Используем await для асинхронного ожидания, чтобы не блокировать поток
                    await Task.Delay(delay, stoppingToken);
                }
                // Обработка отмены во время ожидания
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Выходим из цикла для корректного завершения
                    break;
                }
            }
            // После выхода из цикла worker корректно завершается
        }
    }
}
