namespace AquaTrack.Application.Reporting;

/// <summary>
/// Renders a finished <see cref="ReportTable"/> into a downloadable document. The application layer
/// decides what goes into a report; infrastructure decides how PDF and Excel bytes are produced.
/// </summary>
public interface IReportExporter
{
    byte[] ToPdf(ReportTable table, string farmName);
    byte[] ToExcel(ReportTable table, string farmName);
    byte[] ToCsv(ReportTable table);
}

/// <summary>The formats a report can be downloaded in.</summary>
public enum ReportFormat
{
    Pdf = 1,
    Excel = 2,
    Csv = 3,
}

public static class ReportFormatExtensions
{
    public static string ContentType(this ReportFormat format) => format switch
    {
        ReportFormat.Pdf => "application/pdf",
        ReportFormat.Excel => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "text/csv",
    };

    public static string Extension(this ReportFormat format) => format switch
    {
        ReportFormat.Pdf => "pdf",
        ReportFormat.Excel => "xlsx",
        _ => "csv",
    };
}
