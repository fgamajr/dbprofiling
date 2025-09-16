using System.ComponentModel.DataAnnotations;

namespace DbConnect.Core.Models;

/// <summary>
/// Bucket para histograma de distribuição numérica
/// </summary>
public class HistogramBucket
{
    public decimal RangeStart { get; set; }
    public decimal RangeEnd { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Gap temporal detectado em campos de data
/// </summary>
public class DateGap
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int GapDays { get; set; }
}

/// <summary>
/// Frequência de datas para timeline
/// </summary>
public class DateFrequency
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// Estatísticas específicas para campos booleanos
/// </summary>
public class BooleanStats
{
    public long TrueCount { get; set; }
    public long FalseCount { get; set; }
    public long NullCount { get; set; }
    public double TruePercentage { get; set; }
    public double FalsePercentage { get; set; }
    public double NullPercentage { get; set; }
    public bool IsBalanced => Math.Abs(TruePercentage - FalsePercentage) <= 20; // Balanceado se diferença ≤ 20%
}

/// <summary>
/// Métricas essenciais de uma tabela - análise básica sem IA
/// </summary>
public class TableEssentialMetrics
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int ProfileId { get; set; }

    [Required, MaxLength(100)]
    public string Schema { get; set; } = "";

    [Required, MaxLength(100)]
    public string TableName { get; set; } = "";

    [Required]
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    // Métricas Gerais da Tabela
    public long TotalRows { get; set; }
    public long EstimatedSizeBytes { get; set; }
    public int TotalColumns { get; set; }
    public int ColumnsWithNulls { get; set; }
    public double OverallCompleteness { get; set; } // Percentual geral de preenchimento

    // Análise de Duplicatas
    public long DuplicateRows { get; set; }
    public double DuplicationRate { get; set; }
    public string? PrimaryKeyColumns { get; set; } // Colunas usadas para detectar duplicatas

    // Navegação
    public User User { get; set; } = null!;
    public ConnectionProfile Profile { get; set; } = null!;
    public List<ColumnEssentialMetrics> ColumnMetrics { get; set; } = new();
}

/// <summary>
/// Métricas essenciais por coluna
/// </summary>
public class ColumnEssentialMetrics
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int TableMetricsId { get; set; }

    [Required, MaxLength(100)]
    public string ColumnName { get; set; } = "";

    [Required, MaxLength(50)]
    public string DataType { get; set; } = "";

    public bool IsNullable { get; set; }

    // Métricas de Completude
    public long TotalValues { get; set; }
    public long NullValues { get; set; }
    public long EmptyValues { get; set; } // Para strings vazias
    public double CompletenessRate { get; set; } // Percentual de preenchimento

    // Métricas de Cardinalidade
    public long UniqueValues { get; set; }
    public double CardinalityRate { get; set; } // unique/total

    // Estatísticas Descritivas (campos opcionais dependendo do tipo)
    public decimal? MinNumeric { get; set; }
    public decimal? MaxNumeric { get; set; }
    public decimal? AvgNumeric { get; set; }
    public decimal? StdDevNumeric { get; set; }

    public DateTime? MinDate { get; set; }
    public DateTime? MaxDate { get; set; }

    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public double? AvgLength { get; set; }

    // Top 10 valores mais frequentes (JSON)
    public string? TopValuesJson { get; set; }

    // Navegação
    public TableEssentialMetrics TableMetrics { get; set; } = null!;
}

/// <summary>
/// DTO para retornar as métricas essenciais completas
/// </summary>
public class TableEssentialMetricsDto
{
    public int Id { get; set; }
    public string Schema { get; set; } = "";
    public string TableName { get; set; } = "";
    public DateTime CollectedAt { get; set; }

    // Métricas Gerais
    public TableGeneralMetrics General { get; set; } = new();

    // Métricas por Coluna
    public List<ColumnEssentialDto> Columns { get; set; } = new();
}

public class TableGeneralMetrics
{
    public long TotalRows { get; set; }
    public long EstimatedSizeBytes { get; set; }
    public string EstimatedSizeFormatted => FormatBytes(EstimatedSizeBytes);
    public int TotalColumns { get; set; }
    public int ColumnsWithNulls { get; set; }
    public double OverallCompleteness { get; set; }
    public long DuplicateRows { get; set; }
    public double DuplicationRate { get; set; }
    public string? PrimaryKeyColumns { get; set; }

    // Detalhes sobre problemas encontrados
    public DuplicateRowsDetail? DuplicateDetails { get; set; }
    public List<string> ColumnsWithNullsSample { get; set; } = new(); // Nome das colunas com nulos

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}

public class ColumnEssentialDto
{
    public string ColumnName { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsNullable { get; set; }

    // Tipo de coluna identificado automaticamente
    public ColumnTypeClassification TypeClassification { get; set; } = ColumnTypeClassification.Other;
    public bool IsUniqueIdentifier => TypeClassification == ColumnTypeClassification.UniqueId && CardinalityRate >= 95.0;

    // Completude
    public long TotalValues { get; set; }
    public long NullValues { get; set; }
    public long EmptyValues { get; set; }
    public double CompletenessRate { get; set; }

    // Cardinalidade
    public long UniqueValues { get; set; }
    public double CardinalityRate { get; set; }

    // Estatísticas Descritivas
    public NumericStats? Numeric { get; set; }
    public DateStats? Date { get; set; }
    public TextStats? Text { get; set; }
    public BooleanStats? Boolean { get; set; }

    // Top Valores
    public List<ValueFrequency> TopValues { get; set; } = new();

    // Visualizações específicas por tipo
    public List<TimelineBucket> Timeline { get; set; } = new(); // Para dados DateTime
    public List<GeographicPoint> GeographicPoints { get; set; } = new(); // Para coordenadas

    // Análise detalhada
    public List<string> SampleNullRows { get; set; } = new(); // Sample de IDs das linhas com nulos
    public List<DataQualityAnomaly> QualityAnomalies { get; set; } = new(); // Anomalias detectadas
    public DistributionInsights Distribution { get; set; } = new(); // Insights sobre distribuição
}

public class NumericStats
{
    public decimal Min { get; set; }
    public decimal Max { get; set; }
    public decimal Avg { get; set; }
    public decimal Median { get; set; }
    public decimal StdDev { get; set; }

    // Percentis para análise de distribuição
    public decimal P25 { get; set; }  // 1º quartil
    public decimal P75 { get; set; }  // 3º quartil
    public decimal P90 { get; set; }  // 90º percentil
    public decimal P95 { get; set; }  // 95º percentil

    // Dados para histograma (distribuição)
    public List<HistogramBucket> Distribution { get; set; } = new();

    // Detecção de outliers
    public int OutlierCount { get; set; }
    public List<decimal> OutlierSamples { get; set; } = new();
}

public class DateStats
{
    public DateTime Min { get; set; }
    public DateTime Max { get; set; }
    public TimeSpan Range => Max - Min;

    // Análise de sazonalidade
    public Dictionary<string, int> DayOfWeekDistribution { get; set; } = new();
    public Dictionary<string, int> MonthDistribution { get; set; } = new();
    public Dictionary<int, int> HourDistribution { get; set; } = new();

    // Detecção de gaps temporais
    public List<DateGap> DateGaps { get; set; } = new();
    public int TotalGapDays { get; set; }

    // Timeline visual (para gráficos)
    public List<DateFrequency> Timeline { get; set; } = new();
}

public class TextStats
{
    public int MinLength { get; set; }
    public int MaxLength { get; set; }
    public double AvgLength { get; set; }
}

public class ValueFrequency
{
    public string Value { get; set; } = "";
    public long Count { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// Timeline para visualização de distribuição temporal
/// </summary>
public class TimelineBucket
{
    public DateTime Period { get; set; }
    public long Count { get; set; }
    public double Percentage { get; set; }
    public string Label { get; set; } = ""; // "Jan 2023", "2023-Q1", etc
}

/// <summary>
/// Coordenadas geográficas para mapeamento
/// </summary>
public class GeographicPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public long Count { get; set; }
    public double Percentage { get; set; }
    public string Label { get; set; } = ""; // Nome do local, se disponível
}

/// <summary>
/// Detecta anomalias de qualidade nos dados
/// </summary>
public class DataQualityAnomaly
{
    public string Type { get; set; } = ""; // "suspicious_frequency", "pattern_violation", "outlier"
    public string Description { get; set; } = "";
    public string Value { get; set; } = ""; // Valor problemático
    public long Count { get; set; }
    public double Severity { get; set; } // 0.0 a 1.0
    public List<string> SampleRows { get; set; } = new(); // Sample de linhas afetadas
}

/// <summary>
/// Insights sobre distribuição dos dados
/// </summary>
public class DistributionInsights
{
    public bool HasSuspiciousFrequency { get; set; } // Valores que aparecem frequência anormal
    public bool HasPatternViolations { get; set; } // Padrões inconsistentes (ex: emails inválidos)
    public bool HasOutliers { get; set; } // Valores muito fora do padrão
    public double UniformityScore { get; set; } // 0.0 a 1.0 - quão uniforme é a distribuição
    public string RecommendedAction { get; set; } = ""; // Ação recomendada
}

/// <summary>
/// Detalhes sobre linhas duplicadas
/// </summary>
public class DuplicateRowsDetail
{
    public long TotalDuplicates { get; set; }
    public List<DuplicateGroup> DuplicateGroups { get; set; } = new();
    public List<string> SampleDuplicateRows { get; set; } = new(); // Sample dos IDs duplicados
}

/// <summary>
/// Grupo de linhas duplicadas
/// </summary>
public class DuplicateGroup
{
    public string HashKey { get; set; } = ""; // Hash das colunas que definem a duplicata
    public long Count { get; set; }
    public List<string> AffectedColumns { get; set; } = new(); // Colunas consideradas na duplicação
    public List<string> SampleRowIds { get; set; } = new(); // Sample de IDs das linhas
}

/// <summary>
/// Classificação automática do tipo de coluna para melhor análise
/// </summary>
public enum ColumnTypeClassification
{
    UniqueId,        // IDs, UUIDs, chaves primárias (alta cardinalidade)
    Numeric,         // Valores numéricos (int, decimal, float)
    DateTime,        // Datas e timestamps
    Boolean,         // Campos booleanos (true/false)
    Categorical,     // Categorias com poucos valores únicos
    Text,           // Texto livre com muita variação
    Geographic,     // Coordenadas geográficas (lat/lon)
    Other           // Outros tipos
}