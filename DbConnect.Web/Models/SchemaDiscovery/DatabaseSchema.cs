using System.Collections.Generic;

namespace DbConnect.Web.Models.SchemaDiscovery;

/// <summary>
/// Representa a estrutura completa descoberta de um banco PostgreSQL
/// Inspirado no pg-mcp-server para descoberta automática
/// </summary>
public class DatabaseSchema
{
    public List<TableInfo> Tables { get; set; } = new();
    public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
    public List<ImplicitRelation> ImplicitRelations { get; set; } = new();
    public List<RelevantRelation> RelevantRelations { get; set; } = new();
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public string DatabaseName { get; set; } = string.Empty;
}

/// <summary>
/// Informações de uma tabela descoberta via information_schema.tables
/// </summary>
public class TableInfo
{
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string TableType { get; set; } = string.Empty; // BASE TABLE, VIEW, etc.
    public int ColumnCount { get; set; }
    public long? EstimatedRowCount { get; set; }
    public List<ColumnInfo> Columns { get; set; } = new();
    public string FullName => $"{SchemaName}.{TableName}";
}

/// <summary>
/// Informações de uma coluna descoberta via information_schema.columns
/// </summary>
public class ColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
    public int? CharacterMaximumLength { get; set; }
    public int? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
}

/// <summary>
/// Relacionamento FK declarado descoberto via information_schema.key_column_usage
/// </summary>
public class ForeignKeyInfo
{
    public string SourceSchema { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public string SourceColumn { get; set; } = string.Empty;
    public string TargetSchema { get; set; } = string.Empty;
    public string TargetTable { get; set; } = string.Empty;
    public string TargetColumn { get; set; } = string.Empty;
    public string ConstraintName { get; set; } = string.Empty;
    public string SourceFullName => $"{SourceSchema}.{SourceTable}";
    public string TargetFullName => $"{TargetSchema}.{TargetTable}";
}

/// <summary>
/// Relacionamento implícito detectado por padrões de nomenclatura ou análise estatística
/// </summary>
public class ImplicitRelation
{
    public string SourceTable { get; set; } = string.Empty;
    public string SourceColumn { get; set; } = string.Empty;
    public string TargetTable { get; set; } = string.Empty;
    public string TargetColumn { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; } // 0.0 - 1.0
    public string DetectionMethod { get; set; } = string.Empty; // "NAMING_PATTERN", "STATISTICAL", "AI_SEMANTIC"
    public string Evidence { get; set; } = string.Empty; // Justificativa da detecção
}

/// <summary>
/// Relacionamento relevante para validações cruzadas, com score de importância
/// </summary>
public class RelevantRelation
{
    public string SourceTable { get; set; } = string.Empty;
    public string TargetTable { get; set; } = string.Empty;
    public string JoinCondition { get; set; } = string.Empty;
    public int ImportanceScore { get; set; } // 1-10 baseado em frequência/relevância
    public string RelationType { get; set; } = string.Empty; // "FK_DECLARED", "IMPLICIT", "NAMING_PATTERN"
    public double ConfidenceLevel { get; set; } // 0.0-1.0 para relacionamentos implícitos
    public List<string> ValidationOpportunities { get; set; } = new(); // Tipos de validação possíveis
}