using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace MemoryLeakDetector.UI.Models
{
    public sealed class ProcessSnapshot : INotifyPropertyChanged
    {
        private bool _isLeakSuspected;

        public ProcessSnapshot(
            string name,
            int processId,
            double workingSetMb,
            double virtualMemoryMb,
            int handles,
            double baselineMb,
            bool isLeakSuspected,
            IReadOnlyList<TrendPoint> trend)
        {
            Name = name;
            ProcessId = processId;
            WorkingSetMb = workingSetMb;
            VirtualMemoryMb = virtualMemoryMb;
            Handles = handles;
            BaselineMb = baselineMb;
            _isLeakSuspected = isLeakSuspected;
            Trend = trend;
            UpdatedAt = DateTime.Now;
        }

        public string Name { get; }
        public int ProcessId { get; }
        public double WorkingSetMb { get; }
        public double VirtualMemoryMb { get; }
        public int Handles { get; }
        public double BaselineMb { get; }

        public bool IsLeakSuspected
        {
            get => _isLeakSuspected;
            set
            {
                if (_isLeakSuspected != value)
                {
                    _isLeakSuspected = value;
                    OnPropertyChanged(nameof(IsLeakSuspected));
                }
            }
        }

        public IReadOnlyList<TrendPoint> Trend { get; }
        public DateTime UpdatedAt { get; }

        public double GrowthPercentage =>
            BaselineMb <= 0 ? 0 : Math.Round(((WorkingSetMb - BaselineMb) / BaselineMb) * 100, 2);

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}