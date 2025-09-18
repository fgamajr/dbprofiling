using DbConnect.Web.Models.SchemaDiscovery;

namespace DbConnect.Web.Models.SchemaDiscovery;

/// <summary>
/// Enhanced Database Schema com capacidades expandidas
/// Baseado no DatabaseSchema original + insights do pg-mcp-server
/// </summary>
public class EnhancedDatabaseSchema
{
    public List<EnhancedTableInfo> Tables { get; set; } = new();
    public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
    public List<ImplicitRelation> ImplicitRelations { get; set; } = new();
    public List<StatisticalRelation> StatisticalRelations { get; set; } = new(); // NOVO
    public List<JoinPattern> JoinPatterns { get; set; } = new(); // NOVO
    public List<RelevantRelation> RelevantRelations { get; set; } = new();
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public string DatabaseName { get; set; } = string.Empty;
    public DiscoveryMetrics DiscoveryMetrics { get; set; } = new(); // NOVO
}

/// <summary>
/// Enhanced Table Info com métricas adicionais
/// Baseado no TableInfo original + estatísticas do PostgreSQL
/// </summary>
public class EnhancedTableInfo
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string TableType { get; set; } = string.Empty;
    public int ColumnCount { get; set; }
    public long? EstimatedRowCount { get; set; }
    public string TableSize { get; set; } = string.Empty; // NOVO: pg_size_pretty
    public bool HasPrimaryKey { get; set; } // NOVO
    public List<EnhancedColumnInfo> Columns { get; set; } = new();
    public string FullName => $"{SchemaName}.{TableName}";

    // Métricas calculadas
    public double DataQualityScore => CalculateDataQualityScore(); // NOVO
    public DataQualityBreakdown QualityBreakdown => CalculateQualityBreakdown(); // NOVO
    public int RelationshipCount { get; set; } // NOVO

    private double CalculateDataQualityScore()
    {
        if (!Columns.Any()) return 0.0;

        var score = 0.0;

        // Tem PK = +30 pontos
        if (HasPrimaryKey) score += 30;

        // Baixa taxa de nulos = +20 pontos
        var avgNullFraction = Columns.Average(c => c.NullFraction);
        score += (1.0 - avgNullFraction) * 20;

        // Colunas com estatísticas disponíveis = +20 pontos
        var columnsWithStats = Columns.Count(c => c.DistinctValues > 0);
        score += (columnsWithStats / (double)Columns.Count) * 20;

        // FKs bem definidas = +15 pontos
        var fkColumns = Columns.Count(c => c.IsForeignKey);
        if (fkColumns > 0) score += 15;

        // Tipos de dados apropriados = +15 pontos
        var appropriateTypes = Columns.Count(c => IsAppropriateDataType(c));
        score += (appropriateTypes / (double)Columns.Count) * 15;

        return Math.Min(100, score);
    }

    private DataQualityBreakdown CalculateQualityBreakdown()
    {
        if (!Columns.Any())
            return new DataQualityBreakdown { TotalColumns = 0, TotalScore = 0 };

        var breakdown = new DataQualityBreakdown
        {
            HasPrimaryKey = HasPrimaryKey,
            TotalColumns = Columns.Count
        };

        // Primary Key Score
        breakdown.PrimaryKeyScore = breakdown.HasPrimaryKey ? 30 : 0;

        // Null Rate Score
        var avgNullFraction = Columns.Average(c => c.NullFraction);
        breakdown.NullFraction = avgNullFraction;
        breakdown.NullScore = (int)Math.Round((1.0 - avgNullFraction) * 20);

        // Statistics Score
        breakdown.ColumnsWithStats = Columns.Count(c => c.DistinctValues > 0);
        breakdown.StatisticsScore = (int)Math.Round((breakdown.ColumnsWithStats / (double)breakdown.TotalColumns) * 20);

        // Foreign Key Score
        breakdown.ForeignKeyCount = Columns.Count(c => c.IsForeignKey);
        breakdown.ForeignKeyScore = breakdown.ForeignKeyCount > 0 ? 15 : 0;

        // Data Type Score
        breakdown.AppropriateTypeCount = Columns.Count(c => IsAppropriateDataType(c));
        breakdown.DataTypeScore = (int)Math.Round((breakdown.AppropriateTypeCount / (double)breakdown.TotalColumns) * 15);

        // Total Score
        breakdown.TotalScore = (int)Math.Min(100,
            breakdown.PrimaryKeyScore +
            breakdown.NullScore +
            breakdown.StatisticsScore +
            breakdown.ForeignKeyScore +
            breakdown.DataTypeScore);

        return breakdown;
    }

    private bool IsAppropriateDataType(EnhancedColumnInfo column)
    {
        // Lógica simplificada para detectar tipos apropriados
        var dataType = column.DataType.ToLower();
        var columnName = column.ColumnName.ToLower();

        // IDs devem ser integer/bigint/uuid
        if (columnName.Contains("id") && (dataType.Contains("int") || dataType.Contains("uuid")))
            return true;

        // Datas devem ser timestamp/date
        if (columnName.Contains("data") || columnName.Contains("date"))
            return dataType.Contains("timestamp") || dataType.Contains("date");

        // Emails devem ser varchar
        if (columnName.Contains("email"))
            return dataType.Contains("varchar") || dataType.Contains("text");

        return true; // Default: assume apropriado
    }
}

/// <summary>
/// Enhanced Column Info com estatísticas PostgreSQL
/// </summary>
public class EnhancedColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
    public int? CharacterMaximumLength { get; set; }
    public int? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
    public int OrdinalPosition { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }

    // Informações FK expandidas
    public string? ForeignTableSchema { get; set; } // NOVO
    public string? ForeignTableName { get; set; } // NOVO
    public string? ForeignColumnName { get; set; } // NOVO
    public string? ForeignTableFullName =>
        !string.IsNullOrEmpty(ForeignTableSchema) && !string.IsNullOrEmpty(ForeignTableName)
            ? $"{ForeignTableSchema}.{ForeignTableName}"
            : null; // NOVO

    // Estatísticas PostgreSQL (pg_stats)
    public int DistinctValues { get; set; } // NOVO: n_distinct
    public double NullFraction { get; set; } // NOVO: null_frac

    // Classificação inteligente
    public string DataClassification => ClassifyDataType(); // NOVO

    private string ClassifyDataType()
    {
        var dataType = DataType.ToLower();
        var columnName = ColumnName.ToLower();

        // Identificadores
        if (columnName.Contains("id") || IsPrimaryKey)
            return "IDENTIFIER";

        // Temporais
        if (dataType.Contains("timestamp") || dataType.Contains("date") || dataType.Contains("time"))
            return "TEMPORAL";

        // Numéricos
        if (dataType.Contains("int") || dataType.Contains("numeric") || dataType.Contains("decimal") || dataType.Contains("float"))
            return "NUMERIC";

        // Texto
        if (dataType.Contains("varchar") || dataType.Contains("text") || dataType.Contains("char"))
        {
            // Subcategorias de texto
            if (columnName.Contains("email")) return "EMAIL";
            if (columnName.Contains("cpf") || columnName.Contains("cnpj")) return "DOCUMENT";
            if (columnName.Contains("phone") || columnName.Contains("telefone")) return "PHONE";
            if (columnName.Contains("cep") || columnName.Contains("zip")) return "POSTAL_CODE";
            return "TEXT";
        }

        // Booleanos
        if (dataType.Contains("bool"))
            return "BOOLEAN";

        // JSON
        if (dataType.Contains("json"))
            return "JSON";

        // UUID
        if (dataType.Contains("uuid"))
            return "UUID";

        return "OTHER";
    }
}

/// <summary>
/// NOVO: Relacionamento estatístico detectado por análise de dados
/// </summary>
public class StatisticalRelation
{
    public string SourceTable { get; set; } = string.Empty;
    public string SourceColumn { get; set; } = string.Empty;
    public string TargetTable { get; set; } = string.Empty;
    public string TargetColumn { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; } // 0.0 - 1.0
    public string DetectionMethod { get; set; } = string.Empty; // "CARDINALITY", "VALUE_OVERLAP", "INCLUSION_DEPENDENCY"
    public string Evidence { get; set; } = string.Empty;
    public int ValueOverlap { get; set; } // Quantos valores em comum
    public double OverlapPercentage => ValueOverlap > 0 ? Math.Min(1.0, ValueOverlap / 100.0) : 0.0;
}

/// <summary>
/// NOVO: Padrão de join detectado por análise de uso
/// </summary>
public class JoinPattern
{
    public string LeftTable { get; set; } = string.Empty;
    public string RightTable { get; set; } = string.Empty;
    public string JoinCondition { get; set; } = string.Empty;
    public int FrequencyCount { get; set; } // Quantas vezes foi usado
    public double FrequencyScore { get; set; } // 0.0 - 1.0
    public string JoinType { get; set; } = string.Empty; // "INNER", "LEFT", "RIGHT", "FULL"
    public DateTime LastUsed { get; set; }
    public string DetectionSource { get; set; } = string.Empty; // "PG_STAT", "QUERY_LOG", "PLAN_CACHE"
}

/// <summary>
/// NOVO: Métricas de descoberta
/// </summary>
public class DiscoveryMetrics
{
    public int TotalTables { get; set; }
    public int TotalColumns { get; set; }
    public int DeclaredFKs { get; set; }
    public int ImplicitRelations { get; set; }
    public int StatisticalRelations { get; set; }
    public int JoinPatterns { get; set; }
    public TimeSpan DiscoveryDuration { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public double RelationshipCoverage =>
        TotalTables > 0 ? (DeclaredFKs + ImplicitRelations) / (double)TotalTables : 0.0;

    public string QualityRating => RelationshipCoverage switch
    {
        >= 0.8 => "EXCELLENT",
        >= 0.6 => "GOOD",
        >= 0.4 => "FAIR",
        >= 0.2 => "POOR",
        _ => "CRITICAL"
    };
}

/// <summary>
/// NOVO: Amostra cross-table inteligente
/// </summary>
public class CrossTableSample
{
    public string FocusTable { get; set; } = string.Empty;
    public Dictionary<string, List<Dictionary<string, object>>> TableSamples { get; set; } = new();
    public List<RelatedDataSample> RelatedSamples { get; set; } = new();
    public int TotalSampleSize { get; set; }
    public DateTime SampledAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// NOVO: Amostra de dados relacionados
/// </summary>
public class RelatedDataSample
{
    public string TableName { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty; // "FK_DECLARED", "IMPLICIT", "STATISTICAL"
    public string JoinCondition { get; set; } = string.Empty;
    public List<Dictionary<string, object>> SampleData { get; set; } = new();
    public int SampleSize { get; set; }
    public double ImportanceScore { get; set; }
}

/// <summary>
/// NOVO: Breakdown detalhado do Data Quality Score
/// </summary>
public class DataQualityBreakdown
{
    public bool HasPrimaryKey { get; set; }
    public int PrimaryKeyScore { get; set; }
    public double NullFraction { get; set; }
    public int NullScore { get; set; }
    public int ColumnsWithStats { get; set; }
    public int TotalColumns { get; set; }
    public int StatisticsScore { get; set; }
    public int ForeignKeyCount { get; set; }
    public int ForeignKeyScore { get; set; }
    public int AppropriateTypeCount { get; set; }
    public int DataTypeScore { get; set; }
    public int TotalScore { get; set; }

    public string Summary =>
        $"🔑{(HasPrimaryKey ? "✓" : "✗")} " +
        $"📊{NullScore}pts " +
        $"📈{StatisticsScore}pts " +
        $"🔗{ForeignKeyScore}pts " +
        $"🏷️{DataTypeScore}pts";

    public string DetailedTooltip =>
        $"🔑 Primary Key: {(HasPrimaryKey ? "✓ (+30pts)" : "✗ (0pts)")}\n" +
        $"📊 Null Rate: {(1.0 - NullFraction):P1} data quality (+{NullScore}pts)\n" +
        $"📈 Statistics: {ColumnsWithStats}/{TotalColumns} columns have stats (+{StatisticsScore}pts)\n" +
        $"🔗 Foreign Keys: {ForeignKeyCount} relationships (+{ForeignKeyScore}pts)\n" +
        $"🏷️ Data Types: {AppropriateTypeCount}/{TotalColumns} appropriate types (+{DataTypeScore}pts)\n" +
        $"Total: {TotalScore}/100pts";
}