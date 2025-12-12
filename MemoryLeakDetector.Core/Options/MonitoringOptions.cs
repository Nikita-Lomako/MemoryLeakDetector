namespace MemoryLeakDetector.Core.Options;

// Настройки мониторинга утечек памяти
public sealed class MonitoringOptions
{
    // Макс. количество процессов (null = все)
    public int? MaxProcesses { get; set; }
    
    // Интервал опроса в мс
    public int PollingIntervalMilliseconds { get; set; } = 1000;
    
    // Размер окна baseline (кол-во последних измерений)
    public int BaselineWindow { get; set; } = 120;
    
    // Пороги для Working Set
    public double WorkingSetLeakThresholdPercent { get; set; } = 14.0;
    public double WorkingSetLeakThresholdMb { get; set; } = 50.0;
    
    // Пороги для Virtual Memory и Handles
    public double VirtualMemoryLeakThresholdPercent { get; set; } = 24.0;
    public double HandleLeakThresholdPercent { get; set; } = 20.0;
    
    // Минимум сэмплов для анализа
    public int MinSamplesForLeakDetection { get; set; } = 8;
    
    // Включать системные процессы
    public bool IncludeSystemProcesses { get; set; } = false;
    
    // Кол-во циклов подряд для подтверждения утечки
    public int LeakConfirmationCycles { get; set; } = 3;
    
    // Использовать медиану вместо среднего (устойчивее к выбросам)
    public bool UseMedianForBaseline { get; set; } = true;
    
    // Включить анализ тренда
    public bool EnableTrendAnalysis { get; set; } = true;
}
