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
            TotalProcesses = snapshots.Count;
            TrackedProcesses = snapshots.Count;
            ActiveAlerts = snapshots.Count(snapshot => snapshot.IsLeakSuspected);
            LastUpdated = DateTime.Now;

            MemoryTrend.Clear();
            foreach (var point in snapshots
                         .SelectMany(snapshot => snapshot.Trend)
                         .OrderBy(point => point.Timestamp)
                         .GroupBy(point => point.Timestamp)
                         .Select(group => new TrendPoint(group.Key,
                             group.Average(pt => pt.WorkingSetMb),
                             group.Average(pt => pt.VirtualMemoryMb),
                             (int)group.Average(pt => pt.Handles))))
            {
                MemoryTrend.Add(point);
            }

            TopConsumers.Clear();
            foreach (var process in snapshots
                         .OrderByDescending(snapshot => snapshot.WorkingSetMb)
                         .Take(4))
            {
                TopConsumers.Add(process);
            }
        }
    }
}

