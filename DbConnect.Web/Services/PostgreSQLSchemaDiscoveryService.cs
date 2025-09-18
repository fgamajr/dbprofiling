using DbConnect.Web.Models.SchemaDiscovery;
using Dapper;
using Npgsql;
using System.Data;

namespace DbConnect.Web.Services;

/// <summary>
/// PostgreSQL Schema Discovery Engine
/// Inspirado no pg-mcp-server (TypeScript) para descoberta automática de esquemas
/// Implementação C# nativa usando information_schema
/// </summary>
public interface IPostgreSQLSchemaDiscoveryService
{
    Task<DatabaseSchema> DiscoverCompleteSchemaAsync(string connectionString);
    Task<List<TableInfo>> GetAllTablesAsync(string connectionString);
    Task<List<ForeignKeyInfo>> GetDeclaredForeignKeysAsync(string connectionString);
    Task<List<ImplicitRelation>> DetectImplicitRelationsAsync(string connectionString);
}

public class PostgreSQLSchemaDiscoveryService : IPostgreSQLSchemaDiscoveryService
{
    private readonly ILogger<PostgreSQLSchemaDiscoveryService> _logger;

    public PostgreSQLSchemaDiscoveryService(ILogger<PostgreSQLSchemaDiscoveryService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Descoberta automática completa do banco PostgreSQL
    /// Baseada nas técnicas do pg-mcp-server
    /// </summary>
    public async Task<DatabaseSchema> DiscoverCompleteSchemaAsync(string connectionString)
    {
        _logger.LogInformation("Iniciando descoberta automática de schema PostgreSQL");

        var schema = new DatabaseSchema();

        try
        {
            // 1. Descobrir todas as tabelas (baseado em pg-mcp-server)
            schema.Tables = await GetAllTablesAsync(connectionString);
            _logger.LogInformation("Descobertas {TableCount} tabelas", schema.Tables.Count);

            // 2. Descobrir colunas para cada tabela
            foreach (var table in schema.Tables)
            {
                table.Columns = await GetTableColumnsAsync(connectionString, table.SchemaName, table.TableName);
            }

            // 3. Descobrir relacionamentos FK declarados
            schema.ForeignKeys = await GetDeclaredForeignKeysAsync(connectionString);
            _logger.LogInformation("Descobertas {FKCount} foreign keys", schema.ForeignKeys.Count);

            // 4. Detectar relacionamentos implícitos (nossa inovação)
            schema.ImplicitRelations = await DetectImplicitRelationsAsync(connectionString);
            _logger.LogInformation("Detectados {ImplicitCount} relacionamentos implícitos", schema.ImplicitRelations.Count);

            // 5. Ranquear relacionamentos por importância
            schema.RelevantRelations = RankRelationshipsByImportance(schema);
            _logger.LogInformation("Ranqueados {RelevantCount} relacionamentos relevantes", schema.RelevantRelations.Count);

            schema.DatabaseName = ExtractDatabaseName(connectionString);

            _logger.LogInformation("Descoberta de schema concluída com sucesso");
            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante descoberta de schema");
            throw;
        }
    }

    /// <summary>
    /// Descobrir todas as tabelas usando information_schema.tables
    /// Query baseada no pg-mcp-server
    /// </summary>
    public async Task<List<TableInfo>> GetAllTablesAsync(string connectionString)
    {
        const string sql = @"
            SELECT
                table_schema,
                table_name,
                table_type,
                (SELECT COUNT(*)
                 FROM information_schema.columns
                 WHERE table_schema = t.table_schema
                   AND table_name = t.table_name) as column_count,
                (SELECT schemaname || '.' || tablename as full_name
                 FROM pg_tables
                 WHERE schemaname = t.table_schema
                   AND tablename = t.table_name) as full_table_name
            FROM information_schema.tables t
            WHERE table_schema NOT IN ('information_schema', 'pg_catalog', 'pg_toast')
              AND table_type = 'BASE TABLE'
            ORDER BY table_schema, table_name";

        using var connection = new NpgsqlConnection(connectionString);
        var results = await connection.QueryAsync<dynamic>(sql);

        return results.Select(r => new TableInfo
        {
            SchemaName = r.table_schema,
            TableName = r.table_name,
            TableType = r.table_type,
            ColumnCount = r.column_count
        }).ToList();
    }

    /// <summary>
    /// Descobrir colunas de uma tabela específica
    /// </summary>
    private async Task<List<ColumnInfo>> GetTableColumnsAsync(string connectionString, string schemaName, string tableName)
    {
        const string sql = @"
            SELECT
                column_name,
                data_type,
                is_nullable,
                column_default,
                character_maximum_length,
                numeric_precision,
                numeric_scale,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_primary_key
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
            WHERE c.table_schema = @schemaName
              AND c.table_name = @tableName
            ORDER BY c.ordinal_position";

        using var connection = new NpgsqlConnection(connectionString);
        var results = await connection.QueryAsync<dynamic>(sql, new { schemaName, tableName });

        return results.Select(r => new ColumnInfo
        {
            ColumnName = r.column_name,
            DataType = r.data_type,
            IsNullable = r.is_nullable == "YES",
            DefaultValue = r.column_default,
            CharacterMaximumLength = r.character_maximum_length,
            NumericPrecision = r.numeric_precision,
            NumericScale = r.numeric_scale,
            IsPrimaryKey = r.is_primary_key
        }).ToList();
    }

    /// <summary>
    /// Descobrir relacionamentos FK declarados usando information_schema.key_column_usage
    /// Query exata do pg-mcp-server adaptada para C#
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
    /// Usando padrões de nomenclatura e análise estatística
    /// </summary>
    public async Task<List<ImplicitRelation>> DetectImplicitRelationsAsync(string connectionString)
    {
        var implicitRelations = new List<ImplicitRelation>();

        // 1. Detectar por padrões de nomenclatura (ID_*, *_ID)
        var namingPatternRelations = await DetectNamingPatternRelationsAsync(connectionString);
        implicitRelations.AddRange(namingPatternRelations);

        // 2. TODO: Detectar por análise estatística de cardinalidade
        // 3. TODO: Detectar por inclusion dependencies

        return implicitRelations;
    }

    /// <summary>
    /// Detectar relacionamentos por padrões de nomenclatura
    /// </summary>
    private async Task<List<ImplicitRelation>> DetectNamingPatternRelationsAsync(string connectionString)
    {
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
            )
            WHERE c1.table_schema NOT IN ('information_schema', 'pg_catalog')
              AND c2.table_schema NOT IN ('information_schema', 'pg_catalog')
              AND c1.table_name != c2.table_name
              -- Excluir FKs já declaradas
              AND NOT EXISTS (
                  SELECT 1 FROM information_schema.table_constraints tc
                  JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
                  WHERE tc.constraint_type = 'FOREIGN KEY'
                    AND kcu.table_schema = c1.table_schema
                    AND kcu.table_name = c1.table_name
                    AND kcu.column_name = c1.column_name
              )";

        using var connection = new NpgsqlConnection(connectionString);
        var results = await connection.QueryAsync<dynamic>(sql);

        return results.Select(r => new ImplicitRelation
        {
            SourceTable = $"{r.source_schema}.{r.source_table}",
            SourceColumn = r.source_column,
            TargetTable = $"{r.target_schema}.{r.target_table}",
            TargetColumn = r.target_column,
            ConfidenceScore = 0.8, // Alta confiança para padrões de nomenclatura
            DetectionMethod = "NAMING_PATTERN",
            Evidence = $"Padrão de nomenclatura: {r.source_column} -> {r.target_table}.{r.target_column}"
        }).ToList();
    }

    /// <summary>
    /// Ranquear relacionamentos por importância para validações cruzadas
    /// </summary>
    private List<RelevantRelation> RankRelationshipsByImportance(DatabaseSchema schema)
    {
        var relevantRelations = new List<RelevantRelation>();

        // Relacionamentos FK declarados têm prioridade máxima
        foreach (var fk in schema.ForeignKeys)
        {
            relevantRelations.Add(new RelevantRelation
            {
                SourceTable = fk.SourceFullName,
                TargetTable = fk.TargetFullName,
                JoinCondition = $"{fk.SourceFullName}.{fk.SourceColumn} = {fk.TargetFullName}.{fk.TargetColumn}",
                ImportanceScore = 10, // Máxima prioridade
                RelationType = "FK_DECLARED",
                ConfidenceLevel = 1.0,
                ValidationOpportunities = new List<string> { "REFERENTIAL_INTEGRITY", "ORPHANED_RECORDS", "CASCADE_CONSISTENCY" }
            });
        }

        // Relacionamentos implícitos têm prioridade baseada na confiança
        foreach (var implicitRelation in schema.ImplicitRelations)
        {
            var score = (int)Math.Round(implicitRelation.ConfidenceScore * 8) + 2; // 2-10 baseado na confiança

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