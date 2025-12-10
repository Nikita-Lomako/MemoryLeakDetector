using System.Text;
using System.Text.Json;
using MemoryLeakDetector.Core.Abstractions;
using MemoryLeakDetector.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MemoryLeakDetector.UI.Services.Reporting;

/// <summary>
/// Реализация генератора отчетов для UI приложения.
/// </summary>
public sealed class MonitoringReportGenerator : IReportGenerator
{
    static MonitoringReportGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public string GenerateJson(MonitoringReportModel model, int limit = 100)
    {
        var results = model.Results.Count > limit
            ? model.Results.Skip(model.Results.Count - limit).ToList()
            : model.Results;

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(results, options);
    }

    public string GenerateHtml(MonitoringReportModel model)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"ru\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"utf-8\" />");
        html.AppendLine("    <title>Отчёт по мониторингу памяти</title>");
        html.AppendLine("    <style>");
        html.AppendLine("        body { font-family: -apple-system, BlinkMacSystemFont, \"Segoe UI\", sans-serif; background: #0b1120; color: #f9fafb; margin: 0; padding: 24px; }");
        html.AppendLine("        h1, h2 { color: #f9fafb; margin-bottom: 8px; }");
        html.AppendLine("        .subtitle { opacity: 0.7; margin-bottom: 24px; }");
        html.AppendLine("        .card { background: #020617; border-radius: 12px; padding: 16px 20px; margin-bottom: 16px; border: 1px solid #1e293b; }");
        html.AppendLine("        .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 12px; }");
        html.AppendLine("        .label { font-size: 12px; text-transform: uppercase; letter-spacing: .08em; opacity: .7; }");
        html.AppendLine("        .value { font-size: 20px; font-weight: 600; }");
        html.AppendLine("        table { width: 100%; border-collapse: collapse; margin-top: 8px; font-size: 13px; }");
        html.AppendLine("        th, td { padding: 8px 10px; border-bottom: 1px solid #1f2937; text-align: left; }");
        html.AppendLine("        th { background: #020617; font-weight: 600; }");
        html.AppendLine("        tr:nth-child(even) td { background: #020617; }");
        html.AppendLine("        .chip { display: inline-block; padding: 2px 8px; border-radius: 999px; font-size: 11px; }");
        html.AppendLine("        .chip-leak { background: #7f1d1d; color: #fecaca; }");
        html.AppendLine("        .chip-ok { background: #064e3b; color: #bbf7d0; }");
        html.AppendLine("        .muted { opacity: 0.7; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <h1>Отчёт по мониторингу памяти</h1>");
        html.AppendLine("    <div class=\"subtitle\">");
        html.AppendLine($"        Сгенерирован: {model.GeneratedAtUtc.ToLocalTime():g}<br />");
        html.AppendLine("        Период: ");
        if (model.From is null && model.To is null)
        {
            html.AppendLine("            <span class=\"muted\">все доступные данные</span>");
        }
        else
        {
            html.AppendLine($"            <span class=\"muted\">{model.From?.ToLocalTime():g} — {model.To?.ToLocalTime():g}</span>");
        }
        html.AppendLine("    </div>");

        var summaryLast = model.Results.LastOrDefault();
        var summaryTotalCycles = model.Results.Count;
        var summaryTotalProcesses = summaryLast?.Processes.Count ?? 0;
        var summaryTotalLeaks = summaryLast?.Insights.Count(i => i.IsLeakSuspected) ?? 0;
        var summaryTotalErrors = model.Results.Sum(r => r.ErrorCount);
        
        // Подсчет уникальных утечек за весь период (по ProcessId)
        // Берем последнюю (самую свежую) утечку для каждого процесса
        var allLeaks = model.Results
            .SelectMany(r => r.Insights.Where(i => i.IsLeakSuspected).Select(i => new { Insight = i, CycleTime = r.StartedUtc }))
            .GroupBy(x => x.Insight.ProcessId)
            .Select(g => g.OrderByDescending(x => x.CycleTime).First().Insight)
            .ToList();
        var uniqueLeaksCount = allLeaks.Count;

        html.AppendLine("    <div class=\"card\">");
        html.AppendLine("        <div class=\"grid\">");
        html.AppendLine("            <div>");
        html.AppendLine("                <div class=\"label\">Всего циклов</div>");
        html.AppendLine($"                <div class=\"value\">{summaryTotalCycles}</div>");
        html.AppendLine("            </div>");
        html.AppendLine("            <div>");
        html.AppendLine("                <div class=\"label\">Всего процессов (последний цикл)</div>");
        html.AppendLine($"                <div class=\"value\">{summaryTotalProcesses}</div>");
        html.AppendLine("            </div>");
        html.AppendLine("            <div>");
        html.AppendLine("                <div class=\"label\">Подозрения на утечки (последний цикл)</div>");
        html.AppendLine($"                <div class=\"value\">{summaryTotalLeaks}</div>");
        html.AppendLine("            </div>");
        html.AppendLine("            <div>");
        html.AppendLine("                <div class=\"label\">Уникальных утечек (за весь период)</div>");
        html.AppendLine($"                <div class=\"value\">{uniqueLeaksCount}</div>");
        html.AppendLine("            </div>");
        html.AppendLine("            <div>");
        html.AppendLine("                <div class=\"label\">Ошибки (суммарно)</div>");
        html.AppendLine($"                <div class=\"value\">{summaryTotalErrors}</div>");
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        if (model.Results.Any())
        {
            var latest = model.Results.Last();
            var processesWithLeaks = latest.Insights
                .Where(i => i.IsLeakSuspected)
                .ToList();

            if (processesWithLeaks.Any())
            {
                html.AppendLine("    <div class=\"card\">");
                html.AppendLine($"        <h2>Процессы с обнаруженными утечками (последний цикл: {latest.StartedUtc.ToLocalTime():g})</h2>");
                html.AppendLine($"        <div class=\"muted\">Длительность цикла: {latest.Duration}</div>");
                html.AppendLine("        <table>");
                html.AppendLine("            <thead>");
                html.AppendLine("                <tr>");
                html.AppendLine("                    <th>Процесс</th>");
                html.AppendLine("                    <th>PID</th>");
                html.AppendLine("                    <th>Working Set (MB)</th>");
                html.AppendLine("                    <th>Virtual (MB)</th>");
                html.AppendLine("                    <th>Handles</th>");
                html.AppendLine("                    <th>CPU %</th>");
                html.AppendLine("                    <th>Причина утечки</th>");
                html.AppendLine("                </tr>");
                html.AppendLine("            </thead>");
                html.AppendLine("            <tbody>");

                var processesDict = latest.Processes.ToDictionary(p => p.ProcessId);
                foreach (var insight in processesWithLeaks.OrderByDescending(i => i.WorkingSetDeltaMb))
                {
                    if (processesDict.TryGetValue(insight.ProcessId, out var process))
                    {
                        html.AppendLine("                <tr>");
                        html.AppendLine($"                    <td>{EscapeHtml(insight.ProcessName)}</td>");
                        html.AppendLine($"                    <td>{insight.ProcessId}</td>");
                        html.AppendLine($"                    <td>{process.WorkingSetMb:F0}</td>");
                        html.AppendLine($"                    <td>{process.VirtualMemoryMb:F0}</td>");
                        html.AppendLine($"                    <td>{process.HandleCount}</td>");
                        html.AppendLine($"                    <td>{(process.CpuUsagePercent?.ToString("F1") ?? "-")}</td>");
                        html.AppendLine($"                    <td class=\"muted\">{EscapeHtml(insight.Reason)}</td>");
                        html.AppendLine("                </tr>");
                        
                        // Добавляем информацию о dump-файле, если он был создан
                        if (!string.IsNullOrWhiteSpace(insight.StackTrace))
                        {
                            html.AppendLine("                <tr class=\"dump-info\">");
                            html.AppendLine("                    <td colspan=\"7\" style=\"padding-left: 40px; padding-top: 8px; padding-bottom: 8px; background-color: #f5f5f5; font-size: 11px;\">");
                            html.AppendLine("                        <strong>📁 Dump-файл создан:</strong><br />");
                            html.AppendLine($"                        <pre style=\"margin: 4px 0; white-space: pre-wrap; font-family: Consolas, monospace;\">{EscapeHtml(insight.StackTrace)}</pre>");
                            html.AppendLine("                    </td>");
                            html.AppendLine("                </tr>");
                        }
                    }
                }

                html.AppendLine("            </tbody>");
                html.AppendLine("        </table>");
                html.AppendLine("    </div>");
            }
            else
            {
                html.AppendLine("    <div class=\"card\">");
                html.AppendLine($"        <h2>Последний цикл мониторинга ({latest.StartedUtc.ToLocalTime():g})</h2>");
                html.AppendLine($"        <div class=\"muted\">Длительность: {latest.Duration}</div>");
                html.AppendLine("        <p class=\"muted\">Утечки не обнаружены в последнем цикле.</p>");
                html.AppendLine("    </div>");
            }
            
            // Секция с историей утечек за весь период
            if (allLeaks.Any())
            {
                html.AppendLine("    <div class=\"card\">");
                html.AppendLine($"        <h2>История обнаруженных утечек (всего: {uniqueLeaksCount})</h2>");
                html.AppendLine("        <table>");
                html.AppendLine("            <thead>");
                html.AppendLine("                <tr>");
                html.AppendLine("                    <th>Процесс</th>");
                html.AppendLine("                    <th>PID</th>");
                html.AppendLine("                    <th>Причина</th>");
                html.AppendLine("                    <th>Рост Working Set</th>");
                html.AppendLine("                    <th>Рост Virtual Memory</th>");
                html.AppendLine("                    <th>Рост Handles</th>");
                html.AppendLine("                </tr>");
                html.AppendLine("            </thead>");
                html.AppendLine("            <tbody>");
                
                foreach (var leak in allLeaks.OrderByDescending(l => l.WorkingSetGrowthPercent))
                {
                    // Вычисляем дельты на основе baseline и процентов роста
                    var virtualDeltaMb = leak.BaselineVirtualMemoryMb * leak.VirtualMemoryGrowthPercent / 100.0;
                    var handleDelta = (int)(leak.BaselineHandleCount * leak.HandleGrowthPercent / 100.0);
                    
                    html.AppendLine("                <tr>");
                    html.AppendLine($"                    <td>{EscapeHtml(leak.ProcessName ?? "Unknown")}</td>");
                    html.AppendLine($"                    <td>{leak.ProcessId}</td>");
                    html.AppendLine($"                    <td class=\"muted\">{EscapeHtml(leak.Reason ?? "")}</td>");
                    html.AppendLine($"                    <td>+{leak.WorkingSetDeltaMb:F1} MB ({leak.WorkingSetGrowthPercent:F1}%)</td>");
                    html.AppendLine($"                    <td>+{virtualDeltaMb:F1} MB ({leak.VirtualMemoryGrowthPercent:F1}%)</td>");
                    html.AppendLine($"                    <td>+{handleDelta} ({leak.HandleGrowthPercent:F1}%)</td>");
                    html.AppendLine("                </tr>");
                    
                    // Добавляем информацию о dump-файле, если он был создан
                    if (!string.IsNullOrWhiteSpace(leak.StackTrace))
                    {
                        html.AppendLine("                <tr class=\"dump-info\">");
                        html.AppendLine("                    <td colspan=\"6\" style=\"padding-left: 40px; padding-top: 8px; padding-bottom: 8px; background-color: #f5f5f5; font-size: 11px;\">");
                        html.AppendLine("                        <strong>📁 Dump-файл создан:</strong><br />");
                        html.AppendLine($"                        <pre style=\"margin: 4px 0; white-space: pre-wrap; font-family: Consolas, monospace;\">{EscapeHtml(leak.StackTrace)}</pre>");
                        html.AppendLine("                    </td>");
                        html.AppendLine("                </tr>");
                    }
                }
                
                html.AppendLine("            </tbody>");
                html.AppendLine("        </table>");
                html.AppendLine("    </div>");
            }
        }
        else
        {
            html.AppendLine("    <div class=\"card\">");
            html.AppendLine("        <span class=\"muted\">Данные мониторинга отсутствуют.</span>");
            html.AppendLine("    </div>");
        }

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    public byte[] GeneratePdf(MonitoringReportModel model, int limit = 100)
    {
        var results = model.Results.Count > limit
            ? model.Results.Skip(model.Results.Count - limit).ToList()
            : model.Results;

        var limitedModel = new MonitoringReportModel
        {
            GeneratedAtUtc = model.GeneratedAtUtc,
            From = model.From,
            To = model.To,
            Results = results
        };

        var document = new MonitoringReportDocument(limitedModel);
        return document.GeneratePdf();
    }

    private static string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    private sealed class MonitoringReportDocument : IDocument
    {
        private readonly MonitoringReportModel _model;

        public MonitoringReportDocument(MonitoringReportModel model)
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
                        
                        // Подсчет уникальных утечек за весь период
                        var uniqueLeaks = _model.Results
                            .SelectMany(r => r.Insights.Where(i => i.IsLeakSuspected))
                            .GroupBy(i => i.ProcessId)
                            .Count();

                        SummaryCard(row, "Всего циклов", totalCycles.ToString());
                        SummaryCard(row, "Процессов (последний цикл)", totalProcesses.ToString());
                        SummaryCard(row, "Подозрений (последний цикл)", totalLeaks.ToString());
                        SummaryCard(row, "Уникальных утечек", uniqueLeaks.ToString());
                        SummaryCard(row, "Ошибки (суммарно)", totalErrors.ToString());
                    });

                    var latest = _model.Results.LastOrDefault();
                    if (latest is not null)
                    {
                        var processesWithLeaks = latest.Insights
                            .Where(i => i.IsLeakSuspected)
                            .ToList();

                        if (processesWithLeaks.Any())
                        {
                            col.Item().Text($"Процессы с обнаруженными утечками (последний цикл: {latest.StartedUtc.ToLocalTime():g})")
                                .FontSize(13).SemiBold();
                            col.Item().Text($"Длительность цикла: {latest.Duration}");

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
                                    cols.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(c => HeaderCell(c)).Text("Процесс");
                                    header.Cell().Element(c => HeaderCell(c)).Text("PID");
                                    header.Cell().Element(c => HeaderCell(c)).Text("Working Set (MB)");
                                    header.Cell().Element(c => HeaderCell(c)).Text("Virtual (MB)");
                                    header.Cell().Element(c => HeaderCell(c)).Text("Handles");
                                    header.Cell().Element(c => HeaderCell(c)).Text("CPU %");
                                    header.Cell().Element(c => HeaderCell(c)).Text("Причина утечки");
                                });

                                var processesDict = latest.Processes.ToDictionary(p => p.ProcessId);
                                foreach (var insight in processesWithLeaks.OrderByDescending(i => i.WorkingSetDeltaMb))
                                {
                                    if (processesDict.TryGetValue(insight.ProcessId, out var process))
                                    {
                                        table.Cell().Element(c => Cell(c)).Text(insight.ProcessName);
                                        table.Cell().Element(c => Cell(c)).Text(insight.ProcessId.ToString());
                                        table.Cell().Element(c => Cell(c)).Text($"{process.WorkingSetMb:F0}");
                                        table.Cell().Element(c => Cell(c)).Text($"{process.VirtualMemoryMb:F0}");
                                        table.Cell().Element(c => Cell(c)).Text(process.HandleCount.ToString());
                                        table.Cell().Element(c => Cell(c)).Text(process.CpuUsagePercent?.ToString("F1") ?? "-");
                                        table.Cell().Element(c => Cell(c, true)).Text(insight.Reason);
                                        
                                        // Добавляем информацию о dump-файле, если он был создан
                                        if (!string.IsNullOrWhiteSpace(insight.StackTrace))
                                        {
                                            // Добавляем дополнительные ячейки для dump-файла (span через все колонки)
                                            var dumpText = $"📁 Dump-файл создан:\n{insight.StackTrace}";
                                            table.Cell().ColumnSpan(7).Element(c => c
                                                .PaddingVertical(4)
                                                .PaddingHorizontal(8)
                                                .Background(Colors.Grey.Lighten3))
                                                .Text(dumpText)
                                                .FontSize(9);
                                        }
                                    }
                                }
                            });
                        }
                        else
                        {
                            col.Item().Text($"Последний цикл мониторинга: {latest.StartedUtc.ToLocalTime():g}")
                                .FontSize(13).SemiBold();
                            col.Item().Text($"Длительность: {latest.Duration}");
                            col.Item().Text("Утечки не обнаружены в последнем цикле.").Italic();
                        }
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
}

