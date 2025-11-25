using CommunityToolkit.Mvvm.ComponentModel;

namespace MemoryLeakDetector.UI.Models
{
    public sealed class NavigationItem
    {
        public NavigationItem(string title, string description, ObservableObject viewModel)
        {
            Title = title;
            Description = description;
            ViewModel = viewModel;
        }

        public string Title { get; }
        public string Description { get; }
        public ObservableObject ViewModel { get; }
    }
}

