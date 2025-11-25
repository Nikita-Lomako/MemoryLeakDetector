using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MemoryLeakDetector.UI.Models;
using MemoryLeakDetector.UI.Services.Data;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace MemoryLeakDetector.UI.ViewModels
{
    public sealed partial class ProcessesViewModel : ObservableObject
    {
        private readonly IProcessDataProvider _dataProvider;
        private readonly ObservableCollection<ProcessSnapshot> _processes;
        private readonly Dispatcher _dispatcher;

        [ObservableProperty]
        private DateTime _lastUpdated;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public ProcessesViewModel(IProcessDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
            _processes = new ObservableCollection<ProcessSnapshot>();
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            ProcessesView = CollectionViewSource.GetDefaultView(_processes);
            ProcessesView.Filter = OnFilterProcess;

            RefreshCommand = new RelayCommand(Refresh);
            _dataProvider.ProcessesUpdated += (_, _) => Refresh();

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
            var snapshots = _dataProvider.GetProcesses();

            void Apply()
            {
                _processes.Clear();
                foreach (var snapshot in snapshots)
                {
                    _processes.Add(snapshot);
                }

                LastUpdated = DateTime.Now;
                ProcessesView.Refresh();
            }

            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(Apply);
            }
            else
            {
                Apply();
            }
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

