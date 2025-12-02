using System;
using System.Windows;
using System.Windows.Controls;
using MemoryLeakDetector.UI.ViewModels;

namespace MemoryLeakDetector.UI.Views;

public partial class AnalyticsView : UserControl
{
    public AnalyticsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AnalyticsViewModel vm)
        {
            UpdatePlot(vm);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AnalyticsViewModel oldVm)
        {
            oldVm.PlotUpdated -= OnPlotUpdated;
        }

        if (e.NewValue is AnalyticsViewModel newVm)
        {
            newVm.PlotUpdated += OnPlotUpdated;
            if (IsLoaded)
            {
                UpdatePlot(newVm);
            }
        }
    }

    private void OnPlotUpdated(object? sender, EventArgs e)
    {
        if (sender is AnalyticsViewModel vm && IsLoaded)
        {
            UpdatePlot(vm);
        }
    }

    private void UpdatePlot(AnalyticsViewModel vm)
    {
        if (MemoryPlot == null || !IsLoaded)
        {
            return;
        }

        try
        {
            if (vm.TimePoints.Length == 0 || vm.WorkingSetSeries.Length == 0)
            {
                MemoryPlot.Plot.Clear();
                MemoryPlot.Refresh();
                return;
            }

            MemoryPlot.Plot.Clear();

            // Используем индексы как X, подписи времени можно добавить позже через TickGenerator
            var ws = MemoryPlot.Plot.Add.Scatter(vm.TimePoints, vm.WorkingSetSeries);
            ws.LegendText = "Working Set (MB)";

            var vmSeries = MemoryPlot.Plot.Add.Scatter(vm.TimePoints, vm.VirtualMemorySeries);
            vmSeries.LegendText = "Virtual Memory (MB)";

            MemoryPlot.Plot.Legend.IsVisible = true;
            MemoryPlot.Plot.Axes.AutoScale();

            MemoryPlot.Refresh();
        }
        catch (Exception ex)
        {
            // В случае ошибки просто игнорируем - график останется пустым
            System.Diagnostics.Debug.WriteLine($"Error updating plot: {ex.Message}");
        }
    }
}


