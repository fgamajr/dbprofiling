namespace DbConnect.Core.Models;

public sealed class AnalysisReport
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public required string InputSignature { get; set; }
    public required string StoragePath { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
