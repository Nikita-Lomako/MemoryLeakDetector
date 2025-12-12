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

namespace MemoryLeakDetector.UI.ViewModels
{
    public sealed partial class DashboardViewModel : ObservableObject
    {
        private readonly IProcessDataProvider _dataProvider;
        private readonly Dispatcher _dispatcher;

        [ObservableProperty]
        private int _totalProcesses;

        [ObservableProperty]
        private int _trackedProcesses;

        [ObservableProperty]
        private int _activeAlerts;

        [ObservableProperty]
        private DateTime _lastUpdated;

        public DashboardViewModel(IProcessDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            MemoryTrend = new ObservableCollection<TrendPoint>();
            TopConsumers = new ObservableCollection<ProcessSnapshot>();
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            _dataProvider.ProcessesUpdated += OnProcessesUpdated;

            _ = RefreshAsync();
        }

        public ObservableCollection<TrendPoint> MemoryTrend { get; }

        public ObservableCollection<ProcessSnapshot> TopConsumers { get; }

        public IAsyncRelayCommand RefreshCommand { get; }

        private Task RefreshAsync()
        {
            RefreshFromProvider();
            return Task.CompletedTask;
        }

        private void OnProcessesUpdated(object? sender, EventArgs e) => RefreshFromProvider();

        private void RefreshFromProvider()
        {
            var snapshots = _dataProvider.GetProcesses();

            // Используем BeginInvoke для неблокирующего обновления
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.BeginInvoke(() => ApplySnapshots(snapshots), System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                ApplySnapshots(snapshots);
            }
        }

        private void ApplySnapshots(IReadOnlyCollection<ProcessSnapshot> snapshots)
        {
            try
            {
                TotalProcesses = snapshots.Count;
                TrackedProcesses = snapshots.Count;
                
                // Оптимизация: считаем утечки без LINQ для производительности
                var leakCount = 0;
                foreach (var snapshot in snapshots)
                {
                    if (snapshot.IsLeakSuspected)
                        leakCount++;
                }
                ActiveAlerts = leakCount;
                LastUpdated = DateTime.Now;

                // Оптимизация: ограничиваем количество точек тренда для производительности
                MemoryTrend.Clear();
                const int maxTrendPoints = 50; // Ограничиваем до 50 точек
                var trendDict = new Dictionary<DateTime, (double totalWs, double totalVm, int totalHandles, int count)>();
                
                foreach (var snapshot in snapshots)
                {
                    // Берем только последние точки тренда
                    var trendPoints = snapshot.Trend.Count > 10 
                        ? snapshot.Trend.Skip(snapshot.Trend.Count - 10).ToList() 
                        : snapshot.Trend;
                        
                    foreach (var point in trendPoints)
                    {
                        if (trendDict.TryGetValue(point.Timestamp, out var existing))
                        {
                            trendDict[point.Timestamp] = (
                                existing.totalWs + point.WorkingSetMb,
                                existing.totalVm + point.VirtualMemoryMb,
                                existing.totalHandles + point.Handles,
                                existing.count + 1);
                        }
                        else
                        {
                            trendDict[point.Timestamp] = (point.WorkingSetMb, point.VirtualMemoryMb, point.Handles, 1);
                        }
                    }
                }
                
                // Сортируем и ограничиваем
                var sortedPoints = trendDict
                    .OrderBy(kvp => kvp.Key)
                    .Take(maxTrendPoints)
                    .Select(kvp => new TrendPoint(
                        kvp.Key,
                        kvp.Value.totalWs / kvp.Value.count,
                        kvp.Value.totalVm / kvp.Value.count,
                        kvp.Value.totalHandles / kvp.Value.count));
                
                foreach (var point in sortedPoints)
                {
                    MemoryTrend.Add(point);
                }

                TopConsumers.Clear();
                // Оптимизация: используем простую сортировку вместо OrderByDescending
                var topProcesses = new List<ProcessSnapshot>(snapshots);
                topProcesses.Sort((a, b) => b.WorkingSetMb.CompareTo(a.WorkingSetMb));
                
                foreach (var process in topProcesses.Take(4))
                {
                    TopConsumers.Add(process);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DashboardViewModel.ApplySnapshots: {ex.Message}");
                // Не пробрасываем исключение, чтобы UI не завис
            }
        }
    }
}

