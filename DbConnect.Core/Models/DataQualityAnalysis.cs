namespace DbConnect.Core.Models;

public class DataQualityAnalysis
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProfileId { get; set; }
    public string TableName { get; set; } = "";
    public string Schema { get; set; } = "";
    public string Provider { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Status { get; set; } = ""; // "running", "completed", "error"
    public string? ErrorMessage { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public ConnectionProfile Profile { get; set; } = null!;
    public List<DataQualityResult> Results { get; set; } = new();
}

public class DataQualityResult
{
    public int Id { get; set; }
    public int AnalysisId { get; set; }
    public string RuleId { get; set; } = "";
    public string RuleName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Dimension { get; set; } = "";
    public string Column { get; set; } = "";
    public string SqlCondition { get; set; } = "";
    public string Severity { get; set; } = "";
    public double ExpectedPassRate { get; set; }
    public string Status { get; set; } = ""; // "pass", "fail", "error"
    public double ActualPassRate { get; set; }
    public long TotalRecords { get; set; }
    public long ValidRecords { get; set; }
    public long InvalidRecords { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
    
    // Navigation property
    public DataQualityAnalysis Analysis { get; set; } = null!;
}