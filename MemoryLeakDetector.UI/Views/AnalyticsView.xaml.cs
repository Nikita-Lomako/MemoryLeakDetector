using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MemoryLeakDetector.UI.ViewModels;

namespace MemoryLeakDetector.UI.Views;

public partial class AnalyticsView : UserControl
{
    private CancellationTokenSource? _updateCancellation;
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private DateTime _lastUpdateTime = DateTime.MinValue;
    private const int MinUpdateIntervalMs = 500; // Минимальный интервал между обновлениями (debouncing)

    public AnalyticsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _updateCancellation?.Cancel();
        _updateCancellation?.Dispose();
        
        // Очищаем график для освобождения памяти
        if (MemoryPlot != null)
        {
            MemoryPlot.Plot.Clear();
            MemoryPlot.Refresh();
        }
        
        // Освобождаем семафор
        _updateLock?.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AnalyticsViewModel vm)
        {
            UpdatePlotAsync(vm);
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
                UpdatePlotAsync(newVm);
            }
        }
    }

    private void OnPlotUpdated(object? sender, EventArgs e)
    {
        try
        {
            if (sender is AnalyticsViewModel vm && IsLoaded)
            {
                // Debouncing: обновляем график не чаще чем раз в MinUpdateIntervalMs
                var now = DateTime.Now;
                var timeSinceLastUpdate = (now - _lastUpdateTime).TotalMilliseconds;
                
                if (timeSinceLastUpdate < MinUpdateIntervalMs)
                {
                    // Отменяем предыдущее обновление и планируем новое
                    _updateCancellation?.Cancel();
                    _updateCancellation?.Dispose();
                    _updateCancellation = new CancellationTokenSource();
                    
                    var delay = MinUpdateIntervalMs - timeSinceLastUpdate;
                    _ = Task.Delay((int)delay, _updateCancellation.Token)
                        .ContinueWith(t =>
                        {
                            try
                            {
                                if (!t.IsCanceled && IsLoaded)
                                {
                                    Dispatcher.BeginInvoke(() => UpdatePlotAsync(vm));
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error in delayed plot update: {ex.Message}");
                            }
                        }, TaskScheduler.Default);
                    return;
                }
                
                _lastUpdateTime = now;
                UpdatePlotAsync(vm);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnPlotUpdated: {ex.Message}");
        }
    }

    private void UpdatePlotAsync(AnalyticsViewModel vm)
    {
        try
        {
            if (MemoryPlot == null || !IsLoaded)
            {
                return;
            }

            // Используем асинхронное обновление для неблокирующей отрисовки
            _ = Task.Run(async () =>
            {
                // Ждем блокировку (если уже идет обновление, пропускаем)
                if (!await _updateLock.WaitAsync(0).ConfigureAwait(false))
                {
                    return; // Уже идет обновление, пропускаем это
                }

                try
                {
                    // Подготавливаем данные в фоновом потоке (копируем для thread-safety)
                    Dictionary<int, (double[] TimePoints, double[] WorkingSetSeries, string ProcessName)> processSeriesSnapshot;
                    try
                    {
                        processSeriesSnapshot = new Dictionary<int, (double[] TimePoints, double[] WorkingSetSeries, string ProcessName)>(vm.ProcessSeries);
                    }
                    catch (Exception)
                    {
                        return; // ProcessSeries мог измениться между проверкой и копированием
                    }
                    
                    var totalProcessCount = processSeriesSnapshot.Count;
                    var plotData = PreparePlotData(processSeriesSnapshot);
                    
                    // Обновляем UI в UI потоке с низким приоритетом (не блокирует UI)
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (MemoryPlot == null || !IsLoaded)
                            return;

                        try
                        {
                            if (plotData.Count == 0)
                            {
                                MemoryPlot.Plot.Clear();
                                MemoryPlot.Refresh();
                                return;
                            }

                            MemoryPlot.Plot.Clear();

                            // Генерируем цвета один раз
                            var colors = GenerateColors(plotData.Count);
                            var colorIndex = 0;

                            // Рисуем линии для процессов
                            foreach (var (processName, timePoints, workingSetSeries) in plotData)
                            {
                                if (timePoints.Length == 0 || workingSetSeries.Length == 0)
                                    continue;

                                var scatter = MemoryPlot.Plot.Add.Scatter(timePoints, workingSetSeries);
                                scatter.LegendText = processName;
                                scatter.Color = colors[colorIndex % colors.Length];
                                scatter.LineWidth = 1.5f; // Уменьшаем толщину линии для производительности
                                
                                colorIndex++;
                            }

                            MemoryPlot.Plot.Legend.IsVisible = plotData.Count <= 15; // Показываем легенду только если процессов немного
                            MemoryPlot.Plot.Axes.AutoScale();
                            
                            // Показываем информацию о количестве процессов
                            var title = totalProcessCount > plotData.Count
                                ? $"Working Set по процессам (MB) - показано {plotData.Count} из {totalProcessCount}"
                                : $"Working Set по процессам (MB) - {plotData.Count} процессов";
                            MemoryPlot.Plot.Title(title);
                            MemoryPlot.Plot.XLabel("Время (циклы)");
                            MemoryPlot.Plot.YLabel("Working Set (MB)");

                            // Используем более легковесное обновление с низким приоритетом
                            // Refresh выполняется асинхронно и не блокирует UI
                            MemoryPlot.Refresh();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error updating plot UI: {ex.Message}");
                        }
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in UpdatePlotAsync background: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        _updateLock.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Семафор был освобожден при закрытии
                    }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error starting UpdatePlotAsync: {ex.Message}");
        }
    }

    // Подготавливает данные для графика
    private static List<(string ProcessName, double[] TimePoints, double[] WorkingSetSeries)> PreparePlotData(
        Dictionary<int, (double[] TimePoints, double[] WorkingSetSeries, string ProcessName)> processSeries)
    {
        var result = new List<(string, double[], double[])>();
        
        foreach (var (_, (timePoints, workingSetSeries, processName)) in processSeries)
        {
            if (timePoints.Length > 0 && workingSetSeries.Length > 0)
            {
                result.Add((processName, timePoints, workingSetSeries));
            }
        }
        
        return result;
    }

    private static ScottPlot.Color[] GenerateColors(int count)
    {
        // Генерируем различные цвета для процессов
        var colors = new List<ScottPlot.Color>();
        var baseColors = new[]
        {
            ScottPlot.Colors.Blue,
            ScottPlot.Colors.Red,
            ScottPlot.Colors.Green,
            ScottPlot.Colors.Orange,
            ScottPlot.Colors.Purple,
            ScottPlot.Colors.Brown,
            ScottPlot.Colors.Pink,
            ScottPlot.Colors.Gray,
            ScottPlot.Colors.Cyan,
            ScottPlot.Colors.Yellow
        };

        for (int i = 0; i < count; i++)
        {
            if (i < baseColors.Length)
            {
                colors.Add(baseColors[i]);
            }
            else
            {
                // Генерируем случайный цвет для дополнительных процессов
                var random = new Random(i);
                colors.Add(new ScottPlot.Color(
                    (byte)random.Next(50, 255),
                    (byte)random.Next(50, 255),
                    (byte)random.Next(50, 255)));
            }
        }

        return colors.ToArray();
    }
}


