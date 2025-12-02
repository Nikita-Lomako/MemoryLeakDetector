using CommunityToolkit.Mvvm.ComponentModel;
using MemoryLeakDetector.UI.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace MemoryLeakDetector.UI.ViewModels
{
    public sealed partial class ShellViewModel : ObservableObject
    {
        private NavigationItem? _selectedNavigation;
        private ObservableObject? _currentViewModel;

        public ShellViewModel(
            DashboardViewModel dashboardViewModel,
            ProcessesViewModel processesViewModel,
            AnalyticsViewModel analyticsViewModel)
        {
            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new("Dashboard", "Общий статус мониторинга", dashboardViewModel),
                new("Processes", "Активные процессы и метрики", processesViewModel),
                new("Analytics", "Графики, утечки и рекомендации", analyticsViewModel)
            };

            SelectedNavigation = NavigationItems.FirstOrDefault();
        }

        public ObservableCollection<NavigationItem> NavigationItems { get; }

        public NavigationItem? SelectedNavigation
        {
            get => _selectedNavigation;
            set
            {
                if (SetProperty(ref _selectedNavigation, value) && value is not null)
                {
                    CurrentViewModel = value.ViewModel;
                }
            }
        }

        public ObservableObject? CurrentViewModel
        {
            get => _currentViewModel;
            private set => SetProperty(ref _currentViewModel, value);
        }
    }
}

