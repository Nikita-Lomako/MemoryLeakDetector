using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using MemoryLeakDetector.UI.Services.Reporting;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MemoryLeakDetector.UI.ViewModels;

public sealed partial class ReportsViewModel : ObservableObject
{
    private readonly InMemoryHistoryProvider _historyProvider;
    private readonly IReportGenerator _reportGenerator;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private int _totalResultsCount;

    public ReportsViewModel(InMemoryHistoryProvider historyProvider, IReportGenerator reportGenerator)
    {
        _historyProvider = historyProvider;
        _reportGenerator = reportGenerator;
        FromDate = DateTime.Today.AddDays(-7);
        ToDate = DateTime.Now;
        GenerateJsonCommand = new AsyncRelayCommand(GenerateJsonAsync, () => !IsGenerating);
        GenerateHtmlCommand = new AsyncRelayCommand(GenerateHtmlAsync, () => !IsGenerating);
        GeneratePdfCommand = new AsyncRelayCommand(GeneratePdfAsync, () => !IsGenerating);
    }

    public IAsyncRelayCommand GenerateJsonCommand { get; }
    public IAsyncRelayCommand GenerateHtmlCommand { get; }
    public IAsyncRelayCommand GeneratePdfCommand { get; }

    partial void OnIsGeneratingChanged(bool value)
    {
        GenerateJsonCommand.NotifyCanExecuteChanged();
        GenerateHtmlCommand.NotifyCanExecuteChanged();
        GeneratePdfCommand.NotifyCanExecuteChanged();
    }

    private void UpdateStatus()
    {
        var range = _historyProvider.GetRange(
            FromDate.HasValue ? FromDate.Value.ToUniversalTime() : null,
            ToDate.HasValue ? ToDate.Value.ToUniversalTime() : null);
        
        TotalResultsCount = range.Count;
        
        if (range.Count == 0)
        {
            StatusMessage = "Данные за выбранный период отсутствуют";
        }
        else
        {
            StatusMessage = $"Доступно {range.Count} записей мониторинга";
        }
    }

    partial void OnFromDateChanged(DateTime? value)
    {
        UpdateStatus();
    }

    partial void OnToDateChanged(DateTime? value)
    {
        UpdateStatus();
    }

    private MonitoringReportModel CreateReportModel()
    {
        var range = _historyProvider.GetRange(
            FromDate.HasValue ? FromDate.Value.ToUniversalTime() : null,
            ToDate.HasValue ? ToDate.Value.ToUniversalTime() : null);

        return new MonitoringReportModel
        {
            From = FromDate.HasValue ? FromDate.Value.ToUniversalTime() : null,
            To = ToDate.HasValue ? ToDate.Value.ToUniversalTime() : null,
            Results = range
        };
    }

    private async Task GenerateJsonAsync()
    {
        IsGenerating = true;
        StatusMessage = "Генерация JSON отчета...";

        try
        {
            var model = CreateReportModel();
            if (model.Results.Count == 0)
            {
                MessageBox.Show("Нет данных за выбранный период для генерации отчета.", 
                    "Нет данных", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var json = await Task.Run(() => _reportGenerator.GenerateJson(model));

            var dialog = new SaveFileDialog
            {
                Filter = "JSON файлы (*.json)|*.json|Все файлы (*.*)|*.*",
                FileName = $"memory-report-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                await File.WriteAllTextAsync(dialog.FileName, json);
                StatusMessage = $"JSON отчет сохранен: {Path.GetFileName(dialog.FileName)}";
                MessageBox.Show($"Отчет успешно сохранен:\n{dialog.FileName}", 
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка при генерации JSON отчета";
            MessageBox.Show($"Ошибка при генерации отчета:\n{ex.Message}", 
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private async Task GenerateHtmlAsync()
    {
        IsGenerating = true;
        StatusMessage = "Генерация HTML отчета...";

        try
        {
            var model = CreateReportModel();
            if (model.Results.Count == 0)
            {
                MessageBox.Show("Нет данных за выбранный период для генерации отчета.", 
                    "Нет данных", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var html = await Task.Run(() => _reportGenerator.GenerateHtml(model));

            var dialog = new SaveFileDialog
            {
                Filter = "HTML файлы (*.html)|*.html|Все файлы (*.*)|*.*",
                FileName = $"memory-report-{DateTime.Now:yyyyMMdd-HHmmss}.html",
                DefaultExt = ".html"
            };

            if (dialog.ShowDialog() == true)
            {
                await File.WriteAllTextAsync(dialog.FileName, html);
                StatusMessage = $"HTML отчет сохранен: {Path.GetFileName(dialog.FileName)}";
                MessageBox.Show($"Отчет успешно сохранен:\n{dialog.FileName}\n\nОткройте файл в браузере для просмотра.", 
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка при генерации HTML отчета";
            MessageBox.Show($"Ошибка при генерации отчета:\n{ex.Message}", 
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private async Task GeneratePdfAsync()
    {
        IsGenerating = true;
        StatusMessage = "Генерация PDF отчета...";

        try
        {
            var model = CreateReportModel();
            if (model.Results.Count == 0)
            {
                MessageBox.Show("Нет данных за выбранный период для генерации отчета.", 
                    "Нет данных", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var pdfBytes = await Task.Run(() => _reportGenerator.GeneratePdf(model));

            var dialog = new SaveFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf|Все файлы (*.*)|*.*",
                FileName = $"memory-report-{DateTime.Now:yyyyMMdd-HHmmss}.pdf",
                DefaultExt = ".pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                await File.WriteAllBytesAsync(dialog.FileName, pdfBytes);
                StatusMessage = $"PDF отчет сохранен: {Path.GetFileName(dialog.FileName)}";
                MessageBox.Show($"Отчет успешно сохранен:\n{dialog.FileName}", 
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка при генерации PDF отчета";
            MessageBox.Show($"Ошибка при генерации отчета:\n{ex.Message}", 
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGenerating = false;
        }
    }
}

