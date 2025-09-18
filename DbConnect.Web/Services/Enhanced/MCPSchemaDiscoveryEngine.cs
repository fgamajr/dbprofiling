using DbConnect.Web.Models.SchemaDiscovery;
using Dapper;
using Npgsql;
using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DbConnect.Web.Services.Enhanced;

/// <summary>
/// Enhanced Schema Discovery Engine
/// Baseado no pg-mcp-server + nosso PostgreSQLSchemaDiscoveryService
/// Adiciona capacidades de descoberta automática inteligente
/// </summary>
public interface IMCPSchemaDiscoveryEngine
{
    Task<EnhancedDatabaseSchema> DiscoverCompleteSchemaAsync(string connectionString);
    Task<List<EnhancedTableInfo>> GetAllTablesWithMetricsAsync(string connectionString);
    Task<List<StatisticalRelation>> DetectStatisticalRelationshipsAsync(string connectionString);
    Task<CrossTableSample> GetIntelligentCrossTableSampleAsync(string connectionString, string focusTable);
    Task<List<JoinPattern>> AnalyzeJoinPatternsAsync(string connectionString);
}

public class MCPSchemaDiscoveryEngine : IMCPSchemaDiscoveryEngine
{
    private readonly ILogger<MCPSchemaDiscoveryEngine> _logger;

    public MCPSchemaDiscoveryEngine(ILogger<MCPSchemaDiscoveryEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Descoberta automática completa - versão MCP enhanced
    /// Baseada no pg-mcp-server mas com capacidades expandidas
    /// </summary>
    public async Task<EnhancedDatabaseSchema> DiscoverCompleteSchemaAsync(string connectionString)
    {
        _logger.LogInformation("🔍 Iniciando descoberta MCP enhanced do banco PostgreSQL");

        var schema = new EnhancedDatabaseSchema();

        try
        {
            // 1. Descobrir todas as tabelas (baseado no pg-mcp-server)
            schema.Tables = await GetAllTablesWithMetricsAsync(connectionString);
            _logger.LogInformation("📊 Descobertas {TableCount} tabelas", schema.Tables.Count);

            // 2. Descobrir colunas para cada tabela (pg-mcp-server style)
            foreach (var table in schema.Tables)
            {
                table.Columns = await GetTableColumnsEnhancedAsync(connectionString, table.SchemaName, table.TableName);
            }

            // 3. Descobrir relacionamentos FK declarados (pg-mcp-server exact queries)
            schema.ForeignKeys = await GetDeclaredForeignKeysAsync(connectionString);
            _logger.LogInformation("🔗 Descobertas {FKCount} foreign keys", schema.ForeignKeys.Count);

            // 4. NOSSA INOVAÇÃO: Detectar relacionamentos implícitos via ML/padrões
            schema.ImplicitRelations = await DetectImplicitRelationsAsync(connectionString);
            _logger.LogInformation("🤖 Detectados {ImplicitCount} relacionamentos implícitos", schema.ImplicitRelations.Count);

            // 5. NOVA: Análise estatística de relacionamentos
            schema.StatisticalRelations = await DetectStatisticalRelationshipsAsync(connectionString);
            _logger.LogInformation("📈 Analisados {StatCount} relacionamentos estatísticos", schema.StatisticalRelations.Count);

            // 6. NOVA: Análise de padrões de join
            schema.JoinPatterns = await AnalyzeJoinPatternsAsync(connectionString);
            _logger.LogInformation("⚡ Identificados {JoinCount} padrões de join", schema.JoinPatterns.Count);

            // 7. Ranquear relacionamentos por importância (enhanced)
            schema.RelevantRelations = RankRelationshipsByImportanceEnhanced(schema);
            _logger.LogInformation("🎯 Ranqueados {RelevantCount} relacionamentos relevantes", schema.RelevantRelations.Count);

            schema.DatabaseName = ExtractDatabaseName(connectionString);
            schema.DiscoveryMetrics = new DiscoveryMetrics
            {
                TotalTables = schema.Tables.Count,
                TotalColumns = schema.Tables.Sum(t => t.ColumnCount),
                DeclaredFKs = schema.ForeignKeys.Count,
                ImplicitRelations = schema.ImplicitRelations.Count,
                StatisticalRelations = schema.StatisticalRelations.Count,
                JoinPatterns = schema.JoinPatterns.Count
            };

            _logger.LogInformation("✅ Descoberta MCP enhanced concluída com sucesso");
            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro durante descoberta MCP enhanced");
            throw;
        }
    }

    /// <summary>
    /// Descobrir todas as tabelas usando information_schema.tables
    /// Query exata do pg-mcp-server + métricas extras
    /// </summary>
    public async Task<List<EnhancedTableInfo>> GetAllTablesWithMetricsAsync(string connectionString)
    {
        const string sql = @"
            SELECT
                t.table_schema,
                t.table_name,
                t.table_type,
                (SELECT COUNT(*)
                 FROM information_schema.columns
                 WHERE table_schema = t.table_schema
                   AND table_name = t.table_name) as column_count,
                COALESCE(
                    (SELECT CASE
                        WHEN n_live_tup IS NOT NULL THEN n_live_tup + n_dead_tup
                        ELSE 0
                     END
                     FROM pg_stat_user_tables
                     WHERE schemaname = t.table_schema
                       AND relname = t.table_name), 0) as estimated_rows,
                COALESCE(
                    (SELECT pg_size_pretty(pg_total_relation_size(c.oid))
                     FROM pg_class c
                     JOIN pg_namespace n ON c.relnamespace = n.oid
                     WHERE n.nspname = t.table_schema
                       AND c.relname = t.table_name), 'Unknown') as table_size,
                CASE
                    WHEN EXISTS (
                        SELECT 1 FROM information_schema.table_constraints tc
                        WHERE tc.table_schema = t.table_schema
                          AND tc.table_name = t.table_name
                          AND tc.constraint_type = 'PRIMARY KEY'
                    ) THEN true ELSE false
                END as has_primary_key
            FROM information_schema.tables t
            WHERE t.table_schema NOT IN ('information_schema', 'pg_catalog', 'pg_toast')
              AND t.table_type = 'BASE TABLE'
            ORDER BY t.table_schema, t.table_name";

        using var connection = new NpgsqlConnection(connectionString);
        var results = await connection.QueryAsync<dynamic>(sql);

        return results.Select(r => new EnhancedTableInfo
        {
            SchemaName = r.table_schema,
            TableName = r.table_name,
            TableType = r.table_type,
            ColumnCount = Convert.ToInt32(r.column_count),
            EstimatedRowCount = Convert.ToInt32(r.estimated_rows),
            TableSize = r.table_size,
            HasPrimaryKey = r.has_primary_key
        }).ToList();
    }

    /// <summary>
    /// Descobrir colunas de uma tabela específica
    /// Baseado no pg-mcp-server com informações adicionais
    /// </summary>
    private async Task<List<EnhancedColumnInfo>> GetTableColumnsEnhancedAsync(string connectionString, string schemaName, string tableName)
    {
        const string sql = @"
            SELECT
                c.column_name,
                c.data_type,
                c.is_nullable,
                c.column_default,
                c.character_maximum_length,
                c.numeric_precision,
                c.numeric_scale,
                c.ordinal_position,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_primary_key,
                CASE WHEN fk.column_name IS NOT NULL THEN true ELSE false END as is_foreign_key,
                fk.foreign_table_schema,
                fk.foreign_table_name,
                fk.foreign_column_name,
                -- Estatísticas da coluna se disponíveis
                COALESCE(s.n_distinct, 0) as distinct_values,
                COALESCE(s.null_frac, 0) as null_fraction
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT ku.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage ku
                    ON tc.constraint_name = ku.constraint_name
                WHERE tc.table_schema = @schemaName
                  AND tc.table_name = @tableName
                  AND tc.constraint_type = 'PRIMARY KEY'
            ) pk ON c.column_name = pk.column_name
            LEFT JOIN (
                SELECT
                    kcu.column_name,
                    ccu.table_schema as foreign_table_schema,
                    ccu.table_name as foreign_table_name,
                    ccu.column_name as foreign_column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
                JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name = tc.constraint_name
                WHERE tc.table_schema = @schemaName
                  AND tc.table_name = @tableName
                  AND tc.constraint_type = 'FOREIGN KEY'
            ) fk ON c.column_name = fk.column_name
            LEFT JOIN pg_stats s ON s.schemaname = @schemaName
                AND s.tablename = @tableName
                AND s.attname = c.column_name
            WHERE c.table_schema = @schemaName
              AND c.table_name = @tableName
            ORDER BY c.ordinal_position";

        using var connection = new NpgsqlConnection(connectionString);
        var results = await connection.QueryAsync<dynamic>(sql, new { schemaName, tableName });

        return results.Select(r => new EnhancedColumnInfo
        {
            ColumnName = r.column_name,
            DataType = r.data_type,
            IsNullable = r.is_nullable == "YES",
            DefaultValue = r.column_default,
            CharacterMaximumLength = r.character_maximum_length != null ? (int?)Convert.ToInt32((double)r.character_maximum_length) : null,
            NumericPrecision = r.numeric_precision != null ? (int?)Convert.ToInt32((double)r.numeric_precision) : null,
            NumericScale = r.numeric_scale != null ? (int?)Convert.ToInt32((double)r.numeric_scale) : null,
            OrdinalPosition = Convert.ToInt32((double)r.ordinal_position),
            IsPrimaryKey = r.is_primary_key,
            IsForeignKey = r.is_foreign_key,
            ForeignTableSchema = r.foreign_table_schema,
            ForeignTableName = r.foreign_table_name,
            ForeignColumnName = r.foreign_column_name,
            DistinctValues = r.distinct_values != null ? Convert.ToInt32((double)r.distinct_values) : 0,
            NullFraction = r.null_fraction ?? 0.0
        }).ToList();
    }

    /// <summary>
    /// Descobrir relacionamentos FK declarados
    /// Query exata do pg-mcp-server
    /// </summary>
    public async Task<List<ForeignKeyInfo>> GetDeclaredForeignKeysAsync(string connectionString)
    {
        const string sql = @"
            SELECT
                tc.table_schema as source_schema,
                tc.table_name as source_table,
                kcu.column_name as source_column,
                ccu.table_schema AS target_schema,
                ccu.table_name AS target_table,
                ccu.column_name AS target_column,
                tc.constraint_name
            FROM information_schema.table_constraints AS tc
            JOIN information_schema.key_column_usage AS kcu
                ON tc.constraint_name = kcu.constraint_name
                AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage AS ccu
                ON ccu.constraint_name = tc.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND tc.table_schema NOT IN ('information_schema', 'pg_catalog')
            ORDER BY tc.table_schema, tc.table_name, kcu.column_name";

        using var connection = new NpgsqlConnection(connectionString);
        var results = await connection.QueryAsync<dynamic>(sql);

        return results.Select(r => new ForeignKeyInfo
        {
            SourceSchema = r.source_schema,
            SourceTable = r.source_table,
            SourceColumn = r.source_column,
            TargetSchema = r.target_schema,
            TargetTable = r.target_table,
            TargetColumn = r.target_column,
            ConstraintName = r.constraint_name
        }).ToList();
    }

    /// <summary>
    /// NOSSA INOVAÇÃO: Detectar relacionamentos implícitos
    /// Vai além do pg-mcp-server usando padrões + ML
    /// </summary>
    public async Task<List<ImplicitRelation>> DetectImplicitRelationsAsync(string connectionString)
    {
        var implicitRelations = new List<ImplicitRelation>();

        // 1. Detectar por padrões de nomenclatura
        var namingPatternRelations = await DetectNamingPatternRelationsAsync(connectionString);
        implicitRelations.AddRange(namingPatternRelations);

        // 2. NOVA: Detectar por cardinalidade estatística
        var cardinalityRelations = await DetectCardinalityRelationsAsync(connectionString);
        implicitRelations.AddRange(cardinalityRelations);

        // 3. FUTURA: Detectar por inclusion dependencies
        // 4. FUTURA: Detectar por análise semântica com IA

        return implicitRelations;
    }

    /// <summary>
    /// NOVA: Análise estatística de relacionamentos
    /// Identifica possíveis relacionamentos baseado em dados
    /// </summary>
    public Task<List<StatisticalRelation>> DetectStatisticalRelationshipsAsync(string connectionString)
    {
        const string sql = @"
            SELECT
                c1.table_schema as source_schema,
                c1.table_name as source_table,
                c1.column_name as source_column,
                c2.table_schema as target_schema,
                c2.table_name as target_table,
                c2.column_name as target_column,
                -- Calcular overlap de valores
                (SELECT COUNT(DISTINCT c1_vals.val) FROM (
                    SELECT DISTINCT $column1 as val FROM $table1 WHERE $column1 IS NOT NULL LIMIT 1000
                ) c1_vals
                INNER JOIN (
                    SELECT DISTINCT $column2 as val FROM $table2 WHERE $column2 IS NOT NULL LIMIT 1000
                ) c2_vals ON c1_vals.val = c2_vals.val) as value_overlap
            FROM information_schema.columns c1
            JOIN information_schema.columns c2 ON (
                c1.data_type = c2.data_type
                AND c1.table_name != c2.table_name
                AND (
                    c1.column_name = c2.column_name
                    OR c1.column_name LIKE '%_id'
                    OR c2.column_name LIKE '%_id'
                )
            )
            WHERE c1.table_schema NOT IN ('information_schema', 'pg_catalog')
              AND c2.table_schema NOT IN ('information_schema', 'pg_catalog')
              AND c1.data_type IN ('integer', 'bigint', 'uuid', 'character varying')
            LIMIT 100"; // Limitar para performance

        // Implementação simplificada - precisaria de dynamic SQL para funcionar completamente
        // TODO: Implementar usando a query SQL acima
        _ = sql; // Suprimir warning de variável não usada
        return Task.FromResult(new List<StatisticalRelation>());
    }

    /// <summary>
    /// NOVA: Análise de padrões de join
    /// Baseada em estatísticas do PostgreSQL
    /// </summary>
    public Task<List<JoinPattern>> AnalyzeJoinPatternsAsync(string connectionString)
    {
        // Esta seria uma análise dos pg_stat para identificar joins frequentes
        // Por enquanto, retorna vazio - implementação futura
        return Task.FromResult(new List<JoinPattern>());
    }

    /// <summary>
    /// NOVA: Amostragem inteligente cross-table
    /// </summary>
    public Task<CrossTableSample> GetIntelligentCrossTableSampleAsync(string connectionString, string focusTable)
    {
        // TODO: Implementar amostragem estratégica
        return Task.FromResult(new CrossTableSample());
    }

    /// <summary>
    /// Detectar relacionamentos por padrões de nomenclatura
    /// Otimizado para grandes bancos de dados
    /// </summary>
    private async Task<List<ImplicitRelation>> DetectNamingPatternRelationsAsync(string connectionString)
    {
        try
        {
            // Query otimizada com limites e timeout
            const string sql = @"
                SELECT
                    c1.table_schema as source_schema,
                    c1.table_name as source_table,
                    c1.column_name as source_column,
                    c2.table_schema as target_schema,
                    c2.table_name as target_table,
                    c2.column_name as target_column
                FROM information_schema.columns c1
                JOIN information_schema.columns c2 ON (
                    -- Padrão: ID_TABELA -> TABELA.ID
                    (c1.column_name = 'id_' || c2.table_name AND c2.column_name = 'id')
                    OR
                    -- Padrão: TABELA_ID -> TABELA.ID
                    (c1.column_name = c2.table_name || '_id' AND c2.column_name = 'id')
                    OR
                    -- Padrão: coluna com _ID aponta para outra tabela que tem esse nome
                    (c1.column_name LIKE '%_id' AND
                     (c2.table_name = REPLACE(c1.column_name, '_id', '') OR
                      c2.table_name = 'S_' || UPPER(REPLACE(c1.column_name, '_id', ''))))
                    OR
                    -- Padrão: colunas com nomes similares podem estar relacionadas
                    (c1.column_name != c2.column_name AND
                     (c1.column_name LIKE '%' || SUBSTRING(c2.table_name FROM 3) || '%' OR
                      c2.column_name LIKE '%' || SUBSTRING(c1.table_name FROM 3) || '%') AND
                     (c1.data_type = c2.data_type OR
                      (c1.data_type IN ('integer', 'bigint') AND c2.data_type IN ('integer', 'bigint'))))
                )
                WHERE c1.table_schema = 'caf_mapa'
                  AND c2.table_schema = 'caf_mapa'
                  AND c1.table_name != c2.table_name
                  -- Excluir FKs já declaradas
                  AND NOT EXISTS (
                      SELECT 1 FROM information_schema.table_constraints tc
                      JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
                      WHERE tc.constraint_type = 'FOREIGN KEY'
                        AND kcu.table_schema = c1.table_schema
                        AND kcu.table_name = c1.table_name
                        AND kcu.column_name = c1.column_name
                  )
                LIMIT 1000";

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            // Configura timeout de 30 segundos
            var command = new CommandDefinition(sql, commandTimeout: 30);
            var results = await connection.QueryAsync<dynamic>(command);

            return results.Select(r => new ImplicitRelation
            {
                SourceTable = $"{r.source_schema}.{r.source_table}",
                SourceColumn = r.source_column,
                TargetTable = $"{r.target_schema}.{r.target_table}",
                TargetColumn = r.target_column,
                ConfidenceScore = 0.8,
                DetectionMethod = "NAMING_PATTERN",
                Evidence = $"Padrão de nomenclatura: {r.source_column} -> {r.target_table}.{r.target_column}"
            }).ToList();
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("⏱️ Timeout na detecção de padrões de nomenclatura: {Message}", ex.Message);
            return new List<ImplicitRelation>(); // Retorna lista vazia em caso de timeout
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro na detecção de padrões de nomenclatura");
            return new List<ImplicitRelation>(); // Falha graciosamente
        }
    }

    /// <summary>
    /// NOVA: Detectar relacionamentos por cardinalidade
    /// </summary>
    private Task<List<ImplicitRelation>> DetectCardinalityRelationsAsync(string connectionString)
    {
        // TODO: Implementar análise de cardinalidade
        return Task.FromResult(new List<ImplicitRelation>());
    }

    /// <summary>
    /// Ranquear relacionamentos por importância - versão enhanced
    /// </summary>
    private List<RelevantRelation> RankRelationshipsByImportanceEnhanced(EnhancedDatabaseSchema schema)
    {
        var relevantRelations = new List<RelevantRelation>();

        // FKs declaradas = prioridade máxima
        foreach (var fk in schema.ForeignKeys)
        {
            relevantRelations.Add(new RelevantRelation
            {
                SourceTable = fk.SourceFullName,
                TargetTable = fk.TargetFullName,
                JoinCondition = $"{fk.SourceFullName}.{fk.SourceColumn} = {fk.TargetFullName}.{fk.TargetColumn}",
                ImportanceScore = 10,
                RelationType = "FK_DECLARED",
                ConfidenceLevel = 1.0,
                ValidationOpportunities = new List<string> { "REFERENTIAL_INTEGRITY", "ORPHANED_RECORDS", "CASCADE_CONSISTENCY" }
            });
        }

        // Relacionamentos implícitos = prioridade baseada na confiança
        foreach (var implicitRelation in schema.ImplicitRelations)
        {
            var score = (int)Math.Round(implicitRelation.ConfidenceScore * 8) + 2;

            relevantRelations.Add(new RelevantRelation
            {
                SourceTable = implicitRelation.SourceTable,
                TargetTable = implicitRelation.TargetTable,
                JoinCondition = $"{implicitRelation.SourceTable}.{implicitRelation.SourceColumn} = {implicitRelation.TargetTable}.{implicitRelation.TargetColumn}",
                ImportanceScore = score,
                RelationType = "IMPLICIT",
                ConfidenceLevel = implicitRelation.ConfidenceScore,
                ValidationOpportunities = new List<string> { "DATA_CONSISTENCY", "LOGICAL_INTEGRITY" }
            });
        }

        return relevantRelations.OrderByDescending(r => r.ImportanceScore).ToList();
    }

    private string ExtractDatabaseName(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            return builder.Database ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}