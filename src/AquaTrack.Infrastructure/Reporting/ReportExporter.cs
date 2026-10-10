using AquaTrack.Application.Reporting;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AquaTrack.Infrastructure.Reporting;

/// <summary>Renders any <see cref="ReportTable"/> to PDF, Excel or CSV.</summary>
public class ReportExporter : IReportExporter
{
    // QuestPDF 2026.9+ no longer falls back to system fonts (e.g. Calibri) when one is
    // unavailable — it throws instead. "Lato" ships bundled with QuestPDF itself, so it
    // renders identically on any machine, including a deployment box with no fonts installed.
    private const string PdfFont = "Lato";

    public byte[] ToPdf(ReportTable table, string farmName)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(PdfFont));

                page.Header().Element(header => ComposeHeader(header, table, farmName));
                page.Content().PaddingVertical(8).Element(content => ComposeContent(content, table));

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                    text.Span($"Generated {DateTime.Now:dd MMM yyyy HH:mm}  |  Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ReportTable table, string farmName)
    {
        container.Column(column =>
        {
            column.Item().Text(farmName).FontSize(15).SemiBold().FontColor(Colors.Blue.Darken2);
            column.Item().Text(table.Title).FontSize(12).SemiBold();

            if (!string.IsNullOrWhiteSpace(table.Subtitle))
                column.Item().Text(table.Subtitle!).FontSize(9).FontColor(Colors.Grey.Darken1);

            column.Item().Text(table.PeriodLabel).FontSize(9).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void ComposeContent(IContainer container, ReportTable table)
    {
        container.Column(column =>
        {
            if (table.Summary.Count > 0)
            {
                column.Item().PaddingBottom(10).Row(row =>
                {
                    foreach (var (label, value) in table.Summary)
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2)
                            .Background(Colors.Grey.Lighten5).Padding(6).Column(cell =>
                            {
                                cell.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                                cell.Item().Text(value).FontSize(11).SemiBold();
                            });
                        row.ConstantItem(6);
                    }
                });
            }

            if (table.Rows.Count == 0)
            {
                column.Item().PaddingTop(20).AlignCenter()
                    .Text("No data for the selected period.").FontColor(Colors.Grey.Darken1);
                return;
            }

            column.Item().Table(grid =>
            {
                grid.ColumnsDefinition(columns =>
                {
                    foreach (var col in table.Columns)
                        columns.RelativeColumn(col.Width);
                });

                grid.Header(header =>
                {
                    foreach (var col in table.Columns)
                    {
                        header.Cell()
                            .Background(Colors.Blue.Darken2).Padding(5)
                            .Element(cell => Align(cell, col.Align))
                            .Text(col.Header).FontColor(Colors.White).SemiBold().FontSize(9);
                    }
                });

                var striped = false;
                foreach (var row in table.Rows)
                {
                    var background = striped ? Colors.Grey.Lighten5 : Colors.White;
                    striped = !striped;

                    for (var i = 0; i < table.Columns.Count; i++)
                    {
                        var value = i < row.Count ? row[i] : string.Empty;
                        grid.Cell()
                            .Background(background)
                            .BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4)
                            .Element(cell => Align(cell, table.Columns[i].Align))
                            .Text(value).FontSize(8);
                    }
                }

                if (table.TotalsRow is not { } totals) return;

                for (var i = 0; i < table.Columns.Count; i++)
                {
                    var value = i < totals.Count ? totals[i] : string.Empty;
                    grid.Cell()
                        .Background(Colors.Grey.Lighten3).BorderTop(1).BorderColor(Colors.Grey.Darken1).Padding(5)
                        .Element(cell => Align(cell, table.Columns[i].Align))
                        .Text(value).SemiBold().FontSize(9);
                }
            });
        });
    }

    private static IContainer Align(IContainer container, ReportAlign align) => align switch
    {
        ReportAlign.Right => container.AlignRight(),
        ReportAlign.Center => container.AlignCenter(),
        _ => container.AlignLeft(),
    };

    public byte[] ToExcel(ReportTable table, string farmName)
    {
        using var workbook = new XLWorkbook();

        // Excel caps sheet names at 31 characters and rejects several punctuation marks.
        var sheetName = new string(table.Title.Where(c => !"[]*?/\\:".Contains(c)).ToArray());
        var sheet = workbook.Worksheets.Add(sheetName.Length > 31 ? sheetName[..31] : sheetName);

        var columnCount = Math.Max(table.Columns.Count, 1);
        var cursor = 1;

        sheet.Cell(cursor, 1).Value = farmName;
        sheet.Range(cursor, 1, cursor, columnCount).Merge().Style
            .Font.SetBold().Font.SetFontSize(14)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        cursor++;

        sheet.Cell(cursor, 1).Value = table.Title;
        sheet.Range(cursor, 1, cursor, columnCount).Merge().Style
            .Font.SetBold().Font.SetFontSize(11)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        cursor++;

        sheet.Cell(cursor, 1).Value = table.PeriodLabel;
        sheet.Range(cursor, 1, cursor, columnCount).Merge().Style
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Font.SetFontColor(XLColor.Gray);
        cursor += 2;

        foreach (var (label, value) in table.Summary)
        {
            sheet.Cell(cursor, 1).Value = label;
            sheet.Cell(cursor, 1).Style.Font.SetBold();
            sheet.Cell(cursor, 2).Value = value;
            cursor++;
        }

        if (table.Summary.Count > 0) cursor++;

        var headerRow = cursor;
        for (var i = 0; i < table.Columns.Count; i++)
        {
            var cell = sheet.Cell(headerRow, i + 1);
            cell.Value = table.Columns[i].Header;
            cell.Style.Font.SetBold().Font.SetFontColor(XLColor.White)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#1B4F8A"))
                .Alignment.SetHorizontal(ToExcelAlign(table.Columns[i].Align));
        }

        cursor++;

        foreach (var row in table.Rows)
        {
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var value = i < row.Count ? row[i] : string.Empty;
                var cell = sheet.Cell(cursor, i + 1);

                // Numeric-looking values are written as numbers so Excel can sum and sort them.
                if (decimal.TryParse(value.Replace(",", string.Empty), out var number))
                    cell.Value = number;
                else
                    cell.Value = value;

                cell.Style.Alignment.SetHorizontal(ToExcelAlign(table.Columns[i].Align));
            }

            cursor++;
        }

        if (table.TotalsRow is { } totals)
        {
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var value = i < totals.Count ? totals[i] : string.Empty;
                var cell = sheet.Cell(cursor, i + 1);

                if (decimal.TryParse(value.Replace(",", string.Empty), out var number))
                    cell.Value = number;
                else
                    cell.Value = value;

                cell.Style.Font.SetBold()
                    .Fill.SetBackgroundColor(XLColor.FromHtml("#E8EEF5"))
                    .Alignment.SetHorizontal(ToExcelAlign(table.Columns[i].Align));
            }

            cursor++;
        }

        if (table.Rows.Count > 0)
        {
            sheet.Range(headerRow, 1, cursor - 1, columnCount).SetAutoFilter();
            sheet.SheetView.FreezeRows(headerRow);
        }

        sheet.Columns().AdjustToContents(8d, 42d);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static XLAlignmentHorizontalValues ToExcelAlign(ReportAlign align) => align switch
    {
        ReportAlign.Right => XLAlignmentHorizontalValues.Right,
        ReportAlign.Center => XLAlignmentHorizontalValues.Center,
        _ => XLAlignmentHorizontalValues.Left,
    };

    public byte[] ToCsv(ReportTable table)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(string.Join(",", table.Columns.Select(c => Escape(c.Header))));

        foreach (var row in table.Rows)
            builder.AppendLine(string.Join(",", row.Select(Escape)));

        if (table.TotalsRow is { } totals)
            builder.AppendLine(string.Join(",", totals.Select(Escape)));

        // The BOM makes Excel open the file as UTF-8, which keeps Bangla text readable.
        return System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString()))
            .ToArray();
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
