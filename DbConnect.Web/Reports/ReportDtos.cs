namespace DbConnect.Web.Reports;

public sealed record CreateReportDto(string Name, string Kind, string InputSignature, string ContentBase64);
public sealed record ReportDto(int Id, string Name, string Kind, string InputSignature, DateTime CreatedAtUtc);
