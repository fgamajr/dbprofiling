namespace DbConnect.Core.Models;

// Modelos para o JSON de resposta da LLM (pré-voo)
public class PreflightResponse
{
    public List<PreflightTest> PreflightTests { get; set; } = new();
    public List<SanityQuery> SanityQueries { get; set; } = new();
    public List<RuleCandidateDto> RuleCandidates { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
}

public class PreflightTest
{
    public string Name { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public string Expectation { get; set; } = string.Empty;
}

public class SanityQuery
{
    public string Name { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
    public string Expectation { get; set; } = string.Empty;
}

public class RuleCandidateDto
{
    public string Dimension { get; set; } = string.Empty;     // completude, consistencia, conformidade, precisao
    public string Table { get; set; } = string.Empty;
    public string? Column { get; set; }
    public string CheckSql { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Severity { get; set; } = "medium";
}

// DTOs para APIs
public class TableMetricsDto
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;

    // Métricas de volume
    public long? RowCount { get; set; }
    public decimal? TableSizeMb { get; set; }
    public decimal? IndexSizeMb { get; set; }

    // Timestamp da coleta
    public DateTime CollectedAt { get; set; }
}

public class ColumnMetricsDto
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;

    // Métricas básicas
    public decimal? NullRate { get; set; }
    public long? DistinctCount { get; set; }
    public long? DuplicateCount { get; set; }

    // Métricas de texto (quando aplicável)
    public decimal? AvgLength { get; set; }
    public decimal? StdLength { get; set; }

    // Métricas temporais (quando aplicável)
    public DateTime? MinDate { get; set; }
    public DateTime? MaxDate { get; set; }
    public decimal? PctFutureDates { get; set; }

    public DateTime CollectedAt { get; set; }
}

public class DataQualityDashboardDto
{
    public TableMetricsDto TableMetrics { get; set; } = new();
    public List<ColumnMetricsDto> ColumnMetrics { get; set; } = new();
    public List<RuleCandidate> RuleCandidates { get; set; } = new();
    public List<RuleExecution> RecentExecutions { get; set; } = new();
}