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
        html.AppendLine("                <div class=\"label\">Ошибки (суммарно)</div>");
        html.AppendLine($"                <div class=\"value\">{summaryTotalErrors}</div>");
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        if (model.Results.Any())
        {
            var latest = model.Results.Last();
            html.AppendLine("    <div class=\"card\">");
            html.AppendLine($"        <h2>Последний цикл мониторинга ({latest.StartedUtc.ToLocalTime():g})</h2>");
            html.AppendLine($"        <div class=\"muted\">Длительность: {latest.Duration}</div>");
            html.AppendLine("        <table>");
            html.AppendLine("            <thead>");
            html.AppendLine("                <tr>");
            html.AppendLine("                    <th>Процесс</th>");
            html.AppendLine("                    <th>PID</th>");
            html.AppendLine("                    <th>Working Set (MB)</th>");
            html.AppendLine("                    <th>Virtual (MB)</th>");
            html.AppendLine("                    <th>Handles</th>");
            html.AppendLine("                    <th>CPU %</th>");
            html.AppendLine("                    <th>Leak?</th>");
            html.AppendLine("                    <th>Причина</th>");
            html.AppendLine("                </tr>");
            html.AppendLine("            </thead>");
            html.AppendLine("            <tbody>");

            var insightsByPid = latest.Insights.ToDictionary(i => i.ProcessId, i => i);
            foreach (var p in latest.Processes.OrderByDescending(p => p.WorkingSetMb).Take(50))
            {
                insightsByPid.TryGetValue(p.ProcessId, out var insight);
                var isLeak = insight?.IsLeakSuspected == true;

                html.AppendLine("                <tr>");
                html.AppendLine($"                    <td>{EscapeHtml(p.ProcessName)}</td>");
                html.AppendLine($"                    <td>{p.ProcessId}</td>");
                html.AppendLine($"                    <td>{p.WorkingSetMb:F0}</td>");
                html.AppendLine($"                    <td>{p.VirtualMemoryMb:F0}</td>");
                html.AppendLine($"                    <td>{p.HandleCount}</td>");
                html.AppendLine($"                    <td>{(p.CpuUsagePercent?.ToString("F1") ?? "-")}</td>");
                html.AppendLine("                    <td>");
                if (isLeak)
                {
                    html.AppendLine("                        <span class=\"chip chip-leak\">Leak</span>");
                }
                else
                {
                    html.AppendLine("                        <span class=\"chip chip-ok\">OK</span>");
                }
                html.AppendLine("                    </td>");
                html.AppendLine($"                    <td class=\"muted\">{EscapeHtml(insight?.Reason ?? "")}</td>");
                html.AppendLine("                </tr>");
            }

            html.AppendLine("            </tbody>");
            html.AppendLine("        </table>");
            html.AppendLine("    </div>");
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
}

