using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MemoryLeakDetector.UI.Models;
using MemoryLeakDetector.UI.Services.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace MemoryLeakDetector.UI.ViewModels;

public sealed partial class AnalyticsViewModel : ObservableObject
{
    private readonly IProcessDataProvider _dataProvider;
    private readonly Dispatcher _dispatcher;

    [ObservableProperty]
    private DateTime _lastUpdated;

    /// <summary>
    /// X-координаты точек (индексы или временные метки).
    /// </summary>
    public double[] TimePoints { get; private set; } = Array.Empty<double>();

    /// <summary>
    /// Значения среднего Working Set по времени.
    /// </summary>
    public double[] WorkingSetSeries { get; private set; } = Array.Empty<double>();

    /// <summary>
    /// Значения средней виртуальной памяти по времени.
    /// </summary>
    public double[] VirtualMemorySeries { get; private set; } = Array.Empty<double>();

    public ObservableCollection<ProcessSnapshot> LeakProcesses { get; }

    /// <summary>
    /// Простые текстовые рекомендации (заглушка под будущий анализ кода).
    /// </summary>
    public ObservableCollection<string> Recommendations { get; }

    /// <summary>
    /// Событие, которое дергается при обновлении данных для графика.
    /// </summary>
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
    
    // Отписка от событий для предотвращения утечек
    ~AnalyticsViewModel()
    {
        if (_dataProvider != null)
        {
            _dataProvider.ProcessesUpdated -= OnProcessesUpdated;
        }
    }

    private Task RefreshAsync()
    {
        RefreshFromProvider();
        return Task.CompletedTask;
    }

    private void OnProcessesUpdated(object? sender, EventArgs e) => RefreshFromProvider();

    private void RefreshFromProvider()
    {
        var snapshots = _dataProvider.GetProcesses();

        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(() => ApplySnapshots(snapshots));
        }
        else
        {
            ApplySnapshots(snapshots);
        }
    }

    private void ApplySnapshots(IReadOnlyCollection<ProcessSnapshot> snapshots)
    {
        LastUpdated = DateTime.Now;

        // агрегированный тренд по всем процессам
        var trend = snapshots
            .SelectMany(snapshot => snapshot.Trend)
            .OrderBy(point => point.Timestamp)
            .GroupBy(point => point.Timestamp)
            .Select(group => new TrendPoint(
                group.Key,
                group.Average(pt => pt.WorkingSetMb),
                group.Average(pt => pt.VirtualMemoryMb),
                (int)group.Average(pt => pt.Handles)))
            .ToList();

        TimePoints = trend.Select((_, index) => (double)index).ToArray();
        WorkingSetSeries = trend.Select(p => p.WorkingSetMb).ToArray();
        VirtualMemorySeries = trend.Select(p => p.VirtualMemoryMb).ToArray();

        PlotUpdated?.Invoke(this, EventArgs.Empty);

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
}


