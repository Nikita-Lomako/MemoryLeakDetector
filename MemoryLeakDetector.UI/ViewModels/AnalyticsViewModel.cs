using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MemoryLeakDetector.UI.Models;
using MemoryLeakDetector.UI.Services.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MemoryLeakDetector.UI.ViewModels;

public sealed partial class AnalyticsViewModel : ObservableObject, IDisposable
{
    private readonly IProcessDataProvider _dataProvider;
    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _updateThrottle = new(1, 1);
    private DateTime _lastUpdateTime = DateTime.MinValue;
    private const int MinUpdateIntervalMs = 200; // Throttling: минимум 200мс между обновлениями
    private bool _disposed;

    [ObservableProperty]
    private DateTime _lastUpdated;

    // Данные для графика по процессам
    public Dictionary<int, (double[] TimePoints, double[] WorkingSetSeries, string ProcessName)> ProcessSeries { get; private set; } = new();

    public ObservableCollection<ProcessSnapshot> LeakProcesses { get; }

    // Рекомендации по исправлению
    public ObservableCollection<string> Recommendations { get; }

    // Событие обновления графика
    public event EventHandler? PlotUpdated;

    public IAsyncRelayCommand RefreshCommand { get; }

    public AnalyticsViewModel(IProcessDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        LeakProcesses = new ObservableCollection<ProcessSnapshot>();
        Recommendations = new ObservableCollection<string>();

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        _dataProvider.ProcessesUpdated += OnProcessesUpdated;

        _ = RefreshAsync();
    }
    
    private Task RefreshAsync()
    {
        RefreshFromProvider();
        return Task.CompletedTask;
    }

    private void OnProcessesUpdated(object? sender, EventArgs e)
    {
        // Throttling: ограничиваем частоту обновлений
        var now = DateTime.Now;
        var timeSinceLastUpdate = (now - _lastUpdateTime).TotalMilliseconds;
        
        if (timeSinceLastUpdate < MinUpdateIntervalMs)
        {
            // Пропускаем обновление, если прошло мало времени
            return;
        }
        
        _lastUpdateTime = now;
        RefreshFromProvider();
    }

    private void RefreshFromProvider()
    {
        // Проверяем throttling асинхронно (fire-and-forget)
        _ = Task.Run(async () =>
        {
            if (!await _updateThrottle.WaitAsync(0).ConfigureAwait(false))
            {
                return; // Уже идет обновление
            }

            try
            {
                var snapshots = _dataProvider.GetProcesses();

                // Используем BeginInvoke вместо Invoke для неблокирующего обновления UI
                if (!_dispatcher.CheckAccess())
                {
                    _dispatcher.BeginInvoke(() => ApplySnapshots(snapshots), System.Windows.Threading.DispatcherPriority.Background);
                }
                else
                {
                    ApplySnapshots(snapshots);
                }
            }
            finally
            {
                _updateThrottle.Release();
            }
        });
    }

    private void ApplySnapshots(IReadOnlyCollection<ProcessSnapshot> snapshots)
    {
        try
        {
            LastUpdated = DateTime.Now;

            // Ограничиваем количество процессов на графике для производительности
            // Берем топ-20 процессов по текущему Working Set
            const int maxProcessesOnPlot = 20;
            var topProcesses = snapshots
                .Where(s => s.Trend.Count > 0)
                .OrderByDescending(s => s.WorkingSetMb)
                .Take(maxProcessesOnPlot)
                .ToList();

            // Строим данные для графика по каждому процессу отдельно
            // Очищаем старые данные перед созданием новых для предотвращения утечки памяти
            var oldProcessSeries = ProcessSeries;
            var processSeries = new Dictionary<int, (double[] TimePoints, double[] WorkingSetSeries, string ProcessName)>();
            
            foreach (var snapshot in topProcesses)
            {
                // Trend уже ограничен до 30 точек в StreamProcessDataProvider
                var trend = snapshot.Trend;
                
                if (trend.Count == 0)
                    continue;
                
                // Используем уже отсортированный тренд (он должен быть отсортирован по времени)
                var timePoints = new double[trend.Count];
                var workingSetSeries = new double[trend.Count];
                
                for (int i = 0; i < trend.Count; i++)
                {
                    timePoints[i] = i;
                    workingSetSeries[i] = trend[i].WorkingSetMb;
                }
                
                processSeries[snapshot.ProcessId] = (timePoints, workingSetSeries, snapshot.Name);
            }

            ProcessSeries = processSeries;
            
            // Явно очищаем старые данные для освобождения памяти
            if (oldProcessSeries != null)
            {
                foreach (var (_, (timePoints, workingSetSeries, _)) in oldProcessSeries)
                {
                    // Массивы будут освобождены GC, но мы явно обнуляем ссылки
                    Array.Clear(timePoints, 0, timePoints.Length);
                    Array.Clear(workingSetSeries, 0, workingSetSeries.Length);
                }
                oldProcessSeries.Clear();
            }
            
            try
            {
                PlotUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // Игнорируем ошибки в обработчиках событий
            }

            // подозрительные процессы с причинами
            LeakProcesses.Clear();
            foreach (var leak in snapshots
                         .Where(p => p.IsLeakSuspected)
                         .OrderByDescending(p => p.WorkingSetMb)
                         .Take(20))
            {
                LeakProcesses.Add(leak);
            }

            // простые рекомендации (заглушка)
            Recommendations.Clear();
            foreach (var leak in LeakProcesses)
            {
                if (!string.IsNullOrWhiteSpace(leak.LeakReason))
                {
                    Recommendations.Add(
                        $"Процесс {leak.Name} (PID {leak.ProcessId}): {leak.LeakReason}. " +
                        "Рекомендуется проверить области кода, связанные с длительными операциями и кэшированием.");
                }
            }

            if (Recommendations.Count == 0)
            {
                Recommendations.Add("На текущий момент явных утечек не обнаружено. " +
                                    "Продолжайте мониторинг для накопления статистики baseline.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ApplySnapshots: {ex.Message}");
            // Не пробрасываем исключение, чтобы UI не завис
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _dataProvider.ProcessesUpdated -= OnProcessesUpdated;
        _updateThrottle?.Dispose();
        _disposed = true;
    }
}


