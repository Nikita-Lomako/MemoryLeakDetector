namespace MemoryLeakDetector.Core.Options;

public sealed class MonitoringOptions
{
    /// <summary>
    /// Максимальное количество процессов для отслеживания. 
    /// Если null или 0, отслеживаются все доступные процессы.
    /// </summary>
    public int? MaxProcesses { get; set; }
    public int PollingIntervalMilliseconds { get; set; } = 2000;
    public int BaselineWindow { get; set; } = 120;
    public double WorkingSetLeakThresholdPercent { get; set; } = 25.0;
    public double WorkingSetLeakThresholdMb { get; set; } = 150.0;
    public double VirtualMemoryLeakThresholdPercent { get; set; } = 30.0;
    public double HandleLeakThresholdPercent { get; set; } = 40.0;
    public int MinSamplesForLeakDetection { get; set; } = 5;
    public bool IncludeSystemProcesses { get; set; } = false;
    
    /// <summary>
    /// Количество последовательных циклов с превышением порога для фиксации утечки.
    /// Помогает избежать false positives при временных всплесках.
    /// </summary>
    public int LeakConfirmationCycles { get; set; } = 3;
    
    /// <summary>
    /// Использовать медиану вместо среднего для baseline.
    /// Медиана более устойчива к выбросам.
    /// </summary>
    public bool UseMedianForBaseline { get; set; } = true;
    
    /// <summary>
    /// Включить трендовый анализ для детекции постепенных утечек.
    /// </summary>
    public bool EnableTrendAnalysis { get; set; } = true;
    
    /// <summary>
    /// Минимальный интервал между созданием dump файлов для одного процесса (в секундах).
    /// Помогает предотвратить блокировку системы при частых утечках.
    /// 0 = без ограничений, -1 = отключить создание dump файлов.
    /// </summary>
    public int DumpCreationMinIntervalSeconds { get; set; } = 60;
    
    /// <summary>
    /// Создавать dump файлы асинхронно (не блокировать основной поток мониторинга).
    /// </summary>
    public bool CreateDumpsAsync { get; set; } = true;
}

