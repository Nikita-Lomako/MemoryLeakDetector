using MemoryLeakDetector.API.Models;
using MemoryLeakDetector.Core.Contracts;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MemoryLeakDetector.API.Services.Reporting;

public sealed class MonitoringReportDocument : IDocument
{
    private readonly MonitoringReportViewModel _model;

    public MonitoringReportDocument(MonitoringReportViewModel model)
    {
        _model = model;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(30);
            page.Size(PageSizes.A4);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(11));

            page.Content().PaddingVertical(10).Column(col =>
            {
                col.Spacing(10);

                col.Item().Text("MemoryLeak Detector - Отчёт по мониторингу памяти")
                    .Bold().FontSize(16);

                col.Item().Text(text =>
                {
                    text.Span("Сгенерирован: ").SemiBold();
                    text.Span(_model.GeneratedAtUtc.ToLocalTime().ToString("g"));
                    text.Line("");
                    text.Span("Период: ").SemiBold();

                    if (_model.From is null && _model.To is null)
                        text.Span("все доступные данные");
                    else
                        text.Span($"{_model.From?.ToLocalTime():g} — {_model.To?.ToLocalTime():g}");
                });

                col.Item().Row(row =>
                {
                    var last = _model.Results.LastOrDefault();
                    var totalCycles = _model.Results.Count;
                    var totalProcesses = last?.Processes.Count ?? 0;
                    var totalLeaks = last?.Insights.Count(i => i.IsLeakSuspected) ?? 0;
                    var totalErrors = _model.Results.Sum(r => r.ErrorCount);

                    SummaryCard(row, "Всего циклов", totalCycles.ToString());
                    SummaryCard(row, "Процессов (последний цикл)", totalProcesses.ToString());
                    SummaryCard(row, "Подозрений на утечки (последний цикл)", totalLeaks.ToString());
                    SummaryCard(row, "Ошибки (суммарно)", totalErrors.ToString());
                });

                var latest = _model.Results.LastOrDefault();
                if (latest is not null)
                {
                    col.Item().Text($"Последний цикл мониторинга: {latest.StartedUtc.ToLocalTime():g}")
                        .FontSize(13).SemiBold();
                    col.Item().Text($"Длительность: {latest.Duration}");

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(c => HeaderCell(c)).Text("Процесс");
                            header.Cell().Element(c => HeaderCell(c)).Text("PID");
                            header.Cell().Element(c => HeaderCell(c)).Text("Working Set (MB)");
                            header.Cell().Element(c => HeaderCell(c)).Text("Virtual (MB)");
                            header.Cell().Element(c => HeaderCell(c)).Text("Handles");
                            header.Cell().Element(c => HeaderCell(c)).Text("CPU %");
                            header.Cell().Element(c => HeaderCell(c)).Text("Leak?");
                        });

                        var insightsByPid = latest.Insights.ToDictionary(i => i.ProcessId, i => i);
                        foreach (var p in latest.Processes.OrderByDescending(p => p.WorkingSetMb).Take(40))
                        {
                            insightsByPid.TryGetValue(p.ProcessId, out var insight);
                            var isLeak = insight?.IsLeakSuspected == true;

                            table.Cell().Element(c => Cell(c)).Text(p.ProcessName);
                            table.Cell().Element(c => Cell(c)).Text(p.ProcessId.ToString());
                            table.Cell().Element(c => Cell(c)).Text($"{p.WorkingSetMb:F0}");
                            table.Cell().Element(c => Cell(c)).Text($"{p.VirtualMemoryMb:F0}");
                            table.Cell().Element(c => Cell(c)).Text(p.HandleCount.ToString());
                            table.Cell().Element(c => Cell(c)).Text(p.CpuUsagePercent?.ToString("F1") ?? "-");
                            table.Cell().Element(c => Cell(c, isLeak))
                                .Text(isLeak ? "LEAK" : "OK");
                        }
                    });
                }
                else
                {
                    col.Item().Text("Данные мониторинга отсутствуют.").Italic();
                }
            });
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.DefaultTextStyle(x => x.SemiBold())
            .PaddingVertical(4)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2);

    private static IContainer Cell(IContainer container, bool isLeak = false) =>
        container.PaddingVertical(2)
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten3)
            .Background(isLeak ? Colors.Red.Lighten5 : Colors.White);

    private static void SummaryCard(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(8).Column(col =>
            {
                col.Item().Text(label).FontSize(9).SemiBold();
                col.Item().Text(value).FontSize(14);
            });
    }
}


