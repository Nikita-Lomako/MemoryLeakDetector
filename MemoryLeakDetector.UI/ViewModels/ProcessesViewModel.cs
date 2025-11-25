using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MemoryLeakDetector.UI.Models;
using MemoryLeakDetector.UI.Services.Data;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace MemoryLeakDetector.UI.ViewModels
{
    public sealed partial class ProcessesViewModel : ObservableObject
    {
        private readonly IProcessDataProvider _dataProvider;
        private readonly ObservableCollection<ProcessSnapshot> _processes;

        [ObservableProperty]
        private DateTime _lastUpdated;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public ProcessesViewModel(IProcessDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
            _processes = new ObservableCollection<ProcessSnapshot>();

            ProcessesView = CollectionViewSource.GetDefaultView(_processes);
            ProcessesView.Filter = OnFilterProcess;

            RefreshCommand = new RelayCommand(Refresh);

            Refresh();
        }

        public ICollectionView ProcessesView { get; }

        public IRelayCommand RefreshCommand { get; }

        partial void OnSearchTextChanged(string value)
        {
            ProcessesView.Refresh();
        }

        private void Refresh()
        {
            _processes.Clear();

            foreach (var snapshot in _dataProvider.GetProcesses())
            {
                _processes.Add(snapshot);
            }

            LastUpdated = DateTime.Now;
            ProcessesView.Refresh();
        }

        private bool OnFilterProcess(object obj)
        {
            if (obj is not ProcessSnapshot process)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            return process.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                   || process.ProcessId.ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }
    }
}

