namespace DbConnect.Core.Models;

public class AdvancedColumnMetrics
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;

    // Análise de Padrões (Regex)
    public List<PatternAnalysisResult> PatternMatches { get; set; } = new();

    // Detecção de Outliers (somente para colunas numéricas)
    public OutlierAnalysis? OutlierAnalysis { get; set; }
}

public class PatternAnalysisResult
{
    public string PatternName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Culture { get; set; } = string.Empty;
    public double ConformityPercentage { get; set; }
    public int TotalSamples { get; set; }
    public int MatchingSamples { get; set; }
    public List<string> SampleMatchingValues { get; set; } = new();
    public List<string> SampleNonMatchingValues { get; set; } = new();
}

public class OutlierAnalysis
{
    public int TotalValues { get; set; }
    public int OutlierCount { get; set; }
    public double OutlierPercentage { get; set; }
    public double Mean { get; set; }
    public double StandardDeviation { get; set; }
    public double LowerBound { get; set; } // Mean - 3*StdDev
    public double UpperBound { get; set; } // Mean + 3*StdDev
    public List<double> SampleOutliers { get; set; } = new();
    public List<OutlierRowData> OutlierRows { get; set; } = new();
    public int CurrentPage { get; set; } = 0;
    public int PageSize { get; set; } = 20;
    public int TotalPages => OutlierCount > 0 ? (int)Math.Ceiling((double)OutlierCount / PageSize) : 0;
}

public class OutlierRowData
{
    public double OutlierValue { get; set; }
    public string OutlierColumn { get; set; } = string.Empty;
    public Dictionary<string, object?> RowData { get; set; } = new();
}

public class RelationshipMetrics
{
    public List<StatusDateRelationship> StatusDateRelationships { get; set; } = new();
    public List<NumericCorrelation> NumericCorrelations { get; set; } = new();
}

public class StatusDateRelationship
{
    public string StatusColumn { get; set; } = string.Empty;
    public string DateColumn { get; set; } = string.Empty;
    public string CommonRadical { get; set; } = string.Empty;
    public double InconsistencyPercentage { get; set; }
    public int TotalActiveRecords { get; set; }
    public int InconsistentRecords { get; set; }
    public List<string> ActiveValues { get; set; } = new(); // Valores considerados "ativos"
    public string SqlQuery { get; set; } = string.Empty; // Query usada para análise
}

public class NumericCorrelation
{
    public string Column1 { get; set; } = string.Empty;
    public string Column2 { get; set; } = string.Empty;
    public double CorrelationCoefficient { get; set; }
    public string CorrelationStrength { get; set; } = string.Empty; // "Strong Positive", "Strong Negative", etc.
    public int SampleSize { get; set; }
}

public class AdvancedTableMetrics
{
    public string TableName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public List<AdvancedColumnMetrics> ColumnMetrics { get; set; } = new();
    public RelationshipMetrics RelationshipMetrics { get; set; } = new();
    public DateTime AnalysisTimestamp { get; set; } = DateTime.UtcNow;
    public TimeSpan ProcessingTime { get; set; }
}