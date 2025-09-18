using DbConnect.Web.Models.SchemaDiscovery;
using Dapper;
using Npgsql;
using System.Text.Json;

namespace DbConnect.Web.Services.Enhanced;

/// <summary>
/// Enhanced Context Collector
/// Constrói contexto rico multi-tabela para IA gerar validações cruzadas
/// </summary>
public interface IEnhancedContextCollector
{
    Task<RichAnalysisContext> BuildMultiTableContextAsync(string connectionString, string focusTable, string? businessContext = null);
    Task<CrossTableSample> GetStrategicCrossTableSampleAsync(string connectionString, string focusTable, EnhancedDatabaseSchema schema);
    Task<List<RelatedTableContext>> GetRelatedTablesContextAsync(string connectionString, string focusTable, EnhancedDatabaseSchema schema);
}

public class EnhancedContextCollector : IEnhancedContextCollector
{
    private readonly IMCPSchemaDiscoveryEngine _schemaDiscovery;
    private readonly ILogger<EnhancedContextCollector> _logger;

    public EnhancedContextCollector(
        IMCPSchemaDiscoveryEngine schemaDiscovery,
        ILogger<EnhancedContextCollector> logger)
    {
        _schemaDiscovery = schemaDiscovery;
        _logger = logger;
    }

    /// <summary>
    /// Constrói contexto rico multi-tabela para análise IA
    /// Este é o coração do sistema - monta TUDO que a IA precisa saber
    /// </summary>
    public async Task<RichAnalysisContext> BuildMultiTableContextAsync(
        string connectionString,
        string focusTable,
        string? businessContext = null)
    {
        _logger.LogInformation("🧠 Construindo contexto rico para tabela: {FocusTable}", focusTable);

        // 1. Descobrir schema completo do banco
        var databaseSchema = await _schemaDiscovery.DiscoverCompleteSchemaAsync(connectionString);
        _logger.LogInformation("📊 Schema descoberto: {Tables} tabelas, {FKs} FKs, {Implicit} implícitos",
            databaseSchema.Tables.Count, databaseSchema.ForeignKeys.Count, databaseSchema.ImplicitRelations.Count);

        // 2. Identificar tabela foco
        var focusTableInfo = FindFocusTable(databaseSchema, focusTable);
        if (focusTableInfo == null)
        {
            throw new ArgumentException($"Tabela '{focusTable}' não encontrada no banco");
        }

        // 3. Mapear tabelas relacionadas por ordem de importância
        var relatedTables = await GetRelatedTablesContextAsync(connectionString, focusTable, databaseSchema);
        _logger.LogInformation("🔗 Identificadas {Count} tabelas relacionadas", relatedTables.Count);

        // 4. Coletar amostras estratégicas multi-tabela
        var crossTableSample = await GetStrategicCrossTableSampleAsync(connectionString, focusTable, databaseSchema);
        _logger.LogInformation("🎯 Coletadas amostras de {Count} tabelas", crossTableSample.TableSamples.Count);

        // 5. Montar contexto rico
        var context = new RichAnalysisContext
        {
            FocusTable = focusTable,
            FocusTableInfo = focusTableInfo,
            DatabaseSchema = databaseSchema,
            RelatedTables = relatedTables,
            CrossTableSample = crossTableSample,
            BusinessContext = businessContext ?? "",
            ContextMetrics = new ContextMetrics
            {
                TotalTablesInBank = databaseSchema.Tables.Count,
                RelatedTablesCount = relatedTables.Count,
                DirectRelationshipsCount = relatedTables.Count(r => r.RelationshipType == "FK_DECLARED"),
                ImplicitRelationshipsCount = relatedTables.Count(r => r.RelationshipType == "IMPLICIT"),
                SampleDataSize = crossTableSample.TotalSampleSize,
                ContextComplexity = CalculateContextComplexity(databaseSchema, relatedTables)
            },
            CreatedAt = DateTime.UtcNow
        };

        _logger.LogInformation("✅ Contexto rico construído: {Complexity} complexidade, {SampleSize} amostras",
            context.ContextMetrics.ContextComplexity, context.ContextMetrics.SampleDataSize);

        return context;
    }

    /// <summary>
    /// Coletar amostras estratégicas das tabelas relacionadas
    /// Mantém relacionamentos intactos na amostra
    /// </summary>
    public async Task<CrossTableSample> GetStrategicCrossTableSampleAsync(
        string connectionString,
        string focusTable,
        EnhancedDatabaseSchema schema)
    {
        var sample = new CrossTableSample
        {
            FocusTable = focusTable,
            SampledAt = DateTime.UtcNow
        };

        using var connection = new NpgsqlConnection(connectionString);

        // 1. Coletar amostra da tabela principal (50 registros)
        var focusTableSample = await GetTableSampleAsync(connection, focusTable, 50);
        sample.TableSamples[focusTable] = focusTableSample;
        sample.TotalSampleSize += focusTableSample.Count;

        // 2. Para cada relacionamento relevante, coletar amostras relacionadas
        var relevantRelations = schema.RelevantRelations
            .Where(r => r.SourceTable.EndsWith($".{ExtractTableName(focusTable)}") ||
                       r.TargetTable.EndsWith($".{ExtractTableName(focusTable)}"))
            .OrderByDescending(r => r.ImportanceScore)
            .Take(5); // Top 5 relacionamentos mais importantes

        foreach (var relation in relevantRelations)
        {
            var relatedTable = relation.SourceTable.EndsWith($".{ExtractTableName(focusTable)}")
                ? relation.TargetTable
                : relation.SourceTable;

            if (!sample.TableSamples.ContainsKey(relatedTable))
            {
                var relatedSample = await GetRelatedTableSampleAsync(connection, focusTable, relatedTable, relation.JoinCondition, 30);
                sample.TableSamples[relatedTable] = relatedSample;
                sample.TotalSampleSize += relatedSample.Count;

                sample.RelatedSamples.Add(new RelatedDataSample
                {
                    TableName = relatedTable,
                    RelationshipType = relation.RelationType,
                    JoinCondition = relation.JoinCondition,
                    SampleData = relatedSample,
                    SampleSize = relatedSample.Count,
                    ImportanceScore = relation.ImportanceScore
                });
            }
        }

        return sample;
    }

    /// <summary>
    /// Mapear tabelas relacionadas com contexto detalhado
    /// </summary>
    public Task<List<RelatedTableContext>> GetRelatedTablesContextAsync(
        string connectionString,
        string focusTable,
        EnhancedDatabaseSchema schema)
    {
        var relatedTables = new List<RelatedTableContext>();
        var focusTableName = ExtractTableName(focusTable);

        // 1. Relacionamentos FK declarados (prioridade máxima)
        var declaredFKs = schema.ForeignKeys
            .Where(fk => fk.SourceTable == focusTableName || fk.TargetTable == focusTableName)
            .ToList();

        foreach (var fk in declaredFKs)
        {
            var relatedTableName = fk.SourceTable == focusTableName ? fk.TargetTable : fk.SourceTable;
            var relatedTableInfo = schema.Tables.FirstOrDefault(t => t.TableName == relatedTableName);

            if (relatedTableInfo != null)
            {
                relatedTables.Add(new RelatedTableContext
                {
                    TableName = relatedTableInfo.FullName,
                    TableInfo = relatedTableInfo,
                    RelationshipType = "FK_DECLARED",
                    JoinCondition = $"{fk.SourceFullName}.{fk.SourceColumn} = {fk.TargetFullName}.{fk.TargetColumn}",
                    ImportanceScore = 10,
                    ConfidenceLevel = 1.0,
                    Evidence = $"FK constraint: {fk.ConstraintName}",
                    ValidationOpportunities = new List<string> { "REFERENTIAL_INTEGRITY", "ORPHANED_RECORDS", "CASCADE_CONSISTENCY" }
                });
            }
        }

        // 2. Relacionamentos implícitos (prioridade baseada na confiança)
        var implicitRelations = schema.ImplicitRelations
            .Where(ir => ir.SourceTable.Contains(focusTableName) || ir.TargetTable.Contains(focusTableName))
            .ToList();

        foreach (var implicitRelation in implicitRelations)
        {
            var relatedTableName = implicitRelation.SourceTable.Contains(focusTableName)
                ? implicitRelation.TargetTable
                : implicitRelation.SourceTable;

            var relatedTableInfo = schema.Tables.FirstOrDefault(t => t.FullName == relatedTableName);

            if (relatedTableInfo != null)
            {
                var score = (int)Math.Round(implicitRelation.ConfidenceScore * 8) + 2;

                relatedTables.Add(new RelatedTableContext
                {
                    TableName = relatedTableInfo.FullName,
                    TableInfo = relatedTableInfo,
                    RelationshipType = "IMPLICIT",
                    JoinCondition = $"{implicitRelation.SourceTable}.{implicitRelation.SourceColumn} = {implicitRelation.TargetTable}.{implicitRelation.TargetColumn}",
                    ImportanceScore = score,
                    ConfidenceLevel = implicitRelation.ConfidenceScore,
                    Evidence = implicitRelation.Evidence,
                    ValidationOpportunities = new List<string> { "DATA_CONSISTENCY", "LOGICAL_INTEGRITY" }
                });
            }
        }

        // 3. Relacionamentos estatísticos (prioridade baixa mas pode revelar insights)
        var statisticalRelations = schema.StatisticalRelations
            .Where(sr => sr.SourceTable.Contains(focusTableName) || sr.TargetTable.Contains(focusTableName))
            .ToList();

        foreach (var statRelation in statisticalRelations)
        {
            var relatedTableName = statRelation.SourceTable.Contains(focusTableName)
                ? statRelation.TargetTable
                : statRelation.SourceTable;

            var relatedTableInfo = schema.Tables.FirstOrDefault(t => t.FullName == relatedTableName);

            if (relatedTableInfo != null && !relatedTables.Any(rt => rt.TableName == relatedTableInfo.FullName))
            {
                var score = (int)Math.Round(statRelation.ConfidenceScore * 5) + 1;

                relatedTables.Add(new RelatedTableContext
                {
                    TableName = relatedTableInfo.FullName,
                    TableInfo = relatedTableInfo,
                    RelationshipType = "STATISTICAL",
                    JoinCondition = $"{statRelation.SourceTable}.{statRelation.SourceColumn} = {statRelation.TargetTable}.{statRelation.TargetColumn}",
                    ImportanceScore = score,
                    ConfidenceLevel = statRelation.ConfidenceScore,
                    Evidence = statRelation.Evidence,
                    ValidationOpportunities = new List<string> { "DATA_PATTERNS", "ANOMALY_DETECTION" }
                });
            }
        }

        return Task.FromResult(relatedTables.OrderByDescending(rt => rt.ImportanceScore).ToList());
    }

    /// <summary>
    /// Encontrar informações da tabela foco
    /// </summary>
    private EnhancedTableInfo? FindFocusTable(EnhancedDatabaseSchema schema, string focusTable)
    {
        var tableName = ExtractTableName(focusTable);
        return schema.Tables.FirstOrDefault(t =>
            t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase) ||
            t.FullName.Equals(focusTable, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extrair nome da tabela sem schema
    /// </summary>
    private string ExtractTableName(string tableNameOrFullName)
    {
        var parts = tableNameOrFullName.Split('.');
        return parts.Length > 1 ? parts[1] : parts[0];
    }

    /// <summary>
    /// Coletar amostra de uma tabela específica
    /// </summary>
    private async Task<List<Dictionary<string, object>>> GetTableSampleAsync(
        NpgsqlConnection connection,
        string tableName,
        int sampleSize)
    {
        try
        {
            var quotedTableName = QuoteIdentifier(tableName);
            var sql = $"SELECT * FROM {quotedTableName} ORDER BY RANDOM() LIMIT {sampleSize}";

            var results = await connection.QueryAsync(sql);
            return results.Select(row => ((IDictionary<string, object>)row).ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? "NULL")).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("⚠️ Erro ao coletar amostra da tabela {TableName}: {Error}", tableName, ex.Message);
            return new List<Dictionary<string, object>>();
        }
    }

    /// <summary>
    /// Coletar amostra de tabela relacionada preservando relacionamentos
    /// </summary>
    private async Task<List<Dictionary<string, object>>> GetRelatedTableSampleAsync(
        NpgsqlConnection connection,
        string focusTable,
        string relatedTable,
        string joinCondition,
        int sampleSize)
    {
        try
        {
            var quotedFocusTable = QuoteIdentifier(focusTable);
            var quotedRelatedTable = QuoteIdentifier(relatedTable);

            // Construir JOIN para manter relacionamentos
            var sql = $@"
                SELECT DISTINCT {quotedRelatedTable}.*
                FROM {quotedFocusTable}
                JOIN {quotedRelatedTable} ON {joinCondition}
                ORDER BY RANDOM()
                LIMIT {sampleSize}";

            var results = await connection.QueryAsync(sql);
            return results.Select(row => ((IDictionary<string, object>)row).ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? "NULL")).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("⚠️ Erro ao coletar amostra relacionada {RelatedTable}: {Error}", relatedTable, ex.Message);
            // Fallback: amostra simples da tabela relacionada
            return await GetTableSampleAsync(connection, relatedTable, sampleSize);
        }
    }

    /// <summary>
    /// Calcular complexidade do contexto para otimização de prompts
    /// </summary>
    private string CalculateContextComplexity(EnhancedDatabaseSchema schema, List<RelatedTableContext> relatedTables)
    {
        var totalTables = schema.Tables.Count;
        var totalRelationships = schema.ForeignKeys.Count + schema.ImplicitRelations.Count;
        var relatedTablesCount = relatedTables.Count;

        var complexityScore = (totalTables * 0.1) + (totalRelationships * 0.3) + (relatedTablesCount * 0.6);

        return complexityScore switch
        {
            < 5 => "SIMPLE",
            < 15 => "MODERATE",
            < 30 => "COMPLEX",
            _ => "HIGHLY_COMPLEX"
        };
    }

    /// <summary>
    /// Quote identifier PostgreSQL seguro
    /// </summary>
    private string QuoteIdentifier(string identifier)
    {
        if (identifier.Contains('\0'))
        {
            throw new ArgumentException("Identifier cannot contain null bytes");
        }
        return '"' + identifier.Replace("\"", "\"\"") + '"';
    }
}

/// <summary>
/// Contexto rico para análise IA
/// </summary>
public class RichAnalysisContext
{
    public string FocusTable { get; set; } = string.Empty;
    public EnhancedTableInfo FocusTableInfo { get; set; } = new();
    public EnhancedDatabaseSchema DatabaseSchema { get; set; } = new();
    public List<RelatedTableContext> RelatedTables { get; set; } = new();
    public CrossTableSample CrossTableSample { get; set; } = new();
    public string BusinessContext { get; set; } = string.Empty;
    public ContextMetrics ContextMetrics { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gerar prompt contextual para IA
    /// </summary>
    public string GenerateContextualPrompt()
    {
        var prompt = $@"
# CONTEXTO COMPLETO PARA ANÁLISE DE QUALIDADE DE DADOS

## TABELA PRINCIPAL: {FocusTable}
**Tipo:** {FocusTableInfo.TableType}
**Registros Estimados:** {FocusTableInfo.EstimatedRowCount:N0}
**Tamanho:** {FocusTableInfo.TableSize}
**Qualidade Atual:** {FocusTableInfo.DataQualityScore:F1}/100
**Tem PK:** {(FocusTableInfo.HasPrimaryKey ? "✅ Sim" : "❌ Não")}

### Colunas ({FocusTableInfo.ColumnCount} total):
{string.Join("\n", FocusTableInfo.Columns.Select(c =>
    $"- **{c.ColumnName}**: {c.DataType}" +
    (c.IsPrimaryKey ? " [PK]" : "") +
    (c.IsForeignKey ? $" [FK → {c.ForeignTableFullName}]" : "") +
    $" [{c.DataClassification}]" +
    (c.NullFraction > 0 ? $" (Nulos: {c.NullFraction:P1})" : "")
))}

## RELACIONAMENTOS DESCOBERTOS ({RelatedTables.Count} tabelas relacionadas):
{string.Join("\n", RelatedTables.OrderByDescending(r => r.ImportanceScore).Select(r =>
    $"### {r.ImportanceScore}/10 - {r.TableName} [{r.RelationshipType}]" +
    $"\n- **Join:** {r.JoinCondition}" +
    $"\n- **Confiança:** {r.ConfidenceLevel:P1}" +
    $"\n- **Evidência:** {r.Evidence}" +
    $"\n- **Registros:** {r.TableInfo.EstimatedRowCount:N0}" +
    $"\n- **Validações Possíveis:** {string.Join(", ", r.ValidationOpportunities)}"
))}

## AMOSTRAS DE DADOS ({CrossTableSample.TotalSampleSize} registros total):
{string.Join("\n", CrossTableSample.TableSamples.Take(3).Select(kvp =>
    $"### {kvp.Key} ({kvp.Value.Count} amostras):\n" +
    $"```json\n{JsonSerializer.Serialize(kvp.Value.Take(3), new JsonSerializerOptions { WriteIndented = true })}\n```"
))}

## CONTEXTO DE NEGÓCIO:
{(string.IsNullOrEmpty(BusinessContext) ? "Não fornecido - inferir do schema e dados" : BusinessContext)}

## MÉTRICAS DO CONTEXTO:
- **Complexidade:** {ContextMetrics.ContextComplexity}
- **Relacionamentos Diretos:** {ContextMetrics.DirectRelationshipsCount}
- **Relacionamentos Implícitos:** {ContextMetrics.ImplicitRelationshipsCount}
- **Cobertura de Relacionamentos:** {ContextMetrics.RelationshipCoverage:P1}

---

# TAREFA: GERAR VALIDAÇÕES DE QUALIDADE CRUZADAS

Com base neste contexto COMPLETO, gere 15 validações de qualidade de dados que:

1. **FOQUEM EM RELACIONAMENTOS** entre tabelas (não validações isoladas)
2. **USEM O CONTEXTO** das amostras para ser específico
3. **SEJAM EXECUTÁVEIS** como queries SQL
4. **DETECTEM PROBLEMAS REAIS** de integridade/consistência
5. **APROVEITEM OS PADRÕES** identificados nos dados

Exemplos do que QUEREMOS:
- ""Verificar se documentos ATIVO têm clientes ATIVO""
- ""Validar se data_criacao do documento > data_nascimento do cliente""
- ""Detectar documentos órfãos sem cliente válido""
- ""Checar consistência de status entre tabelas relacionadas""

RETORNE APENAS as validações em linguagem natural, uma por linha, numeradas 1-15.
";

        return prompt;
    }
}

/// <summary>
/// Contexto de tabela relacionada
/// </summary>
public class RelatedTableContext
{
    public string TableName { get; set; } = string.Empty;
    public EnhancedTableInfo TableInfo { get; set; } = new();
    public string RelationshipType { get; set; } = string.Empty; // "FK_DECLARED", "IMPLICIT", "STATISTICAL"
    public string JoinCondition { get; set; } = string.Empty;
    public int ImportanceScore { get; set; } // 1-10
    public double ConfidenceLevel { get; set; } // 0.0-1.0
    public string Evidence { get; set; } = string.Empty;
    public List<string> ValidationOpportunities { get; set; } = new();
}

/// <summary>
/// Métricas do contexto
/// </summary>
public class ContextMetrics
{
    public int TotalTablesInBank { get; set; }
    public int RelatedTablesCount { get; set; }
    public int DirectRelationshipsCount { get; set; }
    public int ImplicitRelationshipsCount { get; set; }
    public int SampleDataSize { get; set; }
    public string ContextComplexity { get; set; } = string.Empty;

    public double RelationshipCoverage =>
        RelatedTablesCount > 0 ? (DirectRelationshipsCount + ImplicitRelationshipsCount) / (double)RelatedTablesCount : 0.0;
}