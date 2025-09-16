using DbConnect.Core.Models;
using DbConnect.Web.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
using System.Text.Json;

namespace DbConnect.Web.Services;

public class StandardMetricsService
{
    private readonly AppDbContext _context;
    private readonly ILogger<StandardMetricsService> _logger;

    public StandardMetricsService(AppDbContext context, ILogger<StandardMetricsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TableMetricsDto> CollectTableMetricsAsync(ConnectionProfile profile, string schemaName, string tableName)
    {
        var result = new TableMetricsDto
        {
            SchemaName = schemaName,
            TableName = tableName,
            CollectedAt = DateTime.UtcNow
        };

        using var connection = new NpgsqlConnection(profile.ConnectionString);
        await connection.OpenAsync();

        try
        {
            // Métricas de volume e tamanho
            var volumeQuery = @"
                SELECT
                    c.relname AS table_name,
                    pg_total_relation_size(c.oid)/1024/1024.0 AS table_size_mb,
                    pg_indexes_size(c.oid)/1024/1024.0 AS index_size_mb,
                    COALESCE(s.n_live_tup, 0) AS approx_row_count,
                    (SELECT COUNT(*) FROM {0}.{1}) AS exact_row_count
                FROM pg_class c
                LEFT JOIN pg_stat_user_tables s ON s.relid = c.oid
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = @schema AND c.relname = @table;";

            var formattedQuery = string.Format(volumeQuery, schemaName, tableName);

            using var cmd = new NpgsqlCommand(formattedQuery, connection);
            cmd.Parameters.AddWithValue("@schema", schemaName);
            cmd.Parameters.AddWithValue("@table", tableName);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.RowCount = reader.GetInt64("exact_row_count");
                result.TableSizeMb = reader.GetDecimal("table_size_mb");
                result.IndexSizeMb = reader.GetDecimal("index_size_mb");
            }

            // Persistir métricas de tabela
            await PersistTableMetricsAsync(result);

            _logger.LogInformation("Coletadas métricas da tabela {Schema}.{Table}: {RowCount} registros",
                schemaName, tableName, result.RowCount);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao coletar métricas da tabela {Schema}.{Table}", schemaName, tableName);
            throw;
        }

        return result;
    }

    public async Task<List<ColumnMetricsDto>> CollectColumnMetricsAsync(ConnectionProfile profile, string schemaName, string tableName)
    {
        var results = new List<ColumnMetricsDto>();

        using var connection = new NpgsqlConnection(profile.ConnectionString);
        await connection.OpenAsync();

        try
        {
            // Obter lista de colunas
            var columnsQuery = @"
                SELECT column_name, data_type, is_nullable
                FROM information_schema.columns
                WHERE table_schema = @schema AND table_name = @table
                ORDER BY ordinal_position;";

            var columns = new List<(string Name, string Type, bool IsNullable)>();

            using (var cmd = new NpgsqlCommand(columnsQuery, connection))
            {
                cmd.Parameters.AddWithValue("@schema", schemaName);
                cmd.Parameters.AddWithValue("@table", tableName);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add((
                        reader.GetString("column_name"),
                        reader.GetString("data_type"),
                        reader.GetString("is_nullable") == "YES"
                    ));
                }
            }

            // Coletar métricas para cada coluna
            foreach (var (columnName, dataType, isNullable) in columns)
            {
                var columnMetrics = await CollectSingleColumnMetricsAsync(connection, schemaName, tableName, columnName, dataType);
                results.Add(columnMetrics);
            }

            // Persistir métricas de coluna
            await PersistColumnMetricsAsync(results);

            _logger.LogInformation("Coletadas métricas de {Count} colunas da tabela {Schema}.{Table}",
                results.Count, schemaName, tableName);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao coletar métricas das colunas da tabela {Schema}.{Table}", schemaName, tableName);
            throw;
        }

        return results;
    }

    private async Task<ColumnMetricsDto> CollectSingleColumnMetricsAsync(
        NpgsqlConnection connection, string schemaName, string tableName, string columnName, string dataType)
    {
        var result = new ColumnMetricsDto
        {
            SchemaName = schemaName,
            TableName = tableName,
            ColumnName = columnName,
            CollectedAt = DateTime.UtcNow
        };

        try
        {
            // Métricas básicas: nulos, distintos, duplicados
            var basicQuery = $@"
                WITH counts AS (
                    SELECT
                        COUNT(*) AS total,
                        SUM(CASE WHEN ""{columnName}"" IS NULL THEN 1 ELSE 0 END) AS null_count,
                        COUNT(DISTINCT ""{columnName}"") AS distinct_count
                    FROM {schemaName}.""{tableName}""
                ),
                duplicates AS (
                    SELECT SUM(CASE WHEN cnt > 1 THEN cnt ELSE 0 END) AS dup_count
                    FROM (
                        SELECT ""{columnName}"", COUNT(*) AS cnt
                        FROM {schemaName}.""{tableName}""
                        GROUP BY ""{columnName}""
                    ) t
                )
                SELECT
                    c.total,
                    c.null_count,
                    c.distinct_count,
                    d.dup_count,
                    CASE WHEN c.total > 0 THEN (c.null_count::decimal / c.total::decimal) ELSE 0 END AS null_rate
                FROM counts c, duplicates d;";

            using (var cmd = new NpgsqlCommand(basicQuery, connection))
            {
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    result.NullRate = reader.GetDecimal("null_rate");
                    result.DistinctCount = reader.GetInt64("distinct_count");
                    result.DuplicateCount = reader.GetInt64("dup_count");
                }
            }

            // Métricas específicas por tipo
            if (IsTextType(dataType))
            {
                await CollectTextMetricsAsync(connection, result, schemaName, tableName, columnName);
            }
            else if (IsDateType(dataType))
            {
                await CollectDateMetricsAsync(connection, result, schemaName, tableName, columnName);
            }

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao coletar métricas da coluna {Schema}.{Table}.{Column}",
                schemaName, tableName, columnName);
        }

        return result;
    }

    private async Task CollectTextMetricsAsync(NpgsqlConnection connection, ColumnMetricsDto result,
        string schemaName, string tableName, string columnName)
    {
        var textQuery = $@"
            SELECT
                AVG(LENGTH(""{columnName}""))::numeric(10,2) AS avg_len,
                STDDEV_POP(LENGTH(""{columnName}""))::numeric(10,2) AS std_len
            FROM {schemaName}.""{tableName}""
            WHERE ""{columnName}"" IS NOT NULL;";

        try
        {
            using var cmd = new NpgsqlCommand(textQuery, connection);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync() && !reader.IsDBNull("avg_len"))
            {
                result.AvgLength = reader.GetDecimal("avg_len");
                result.StdLength = reader.IsDBNull("std_len") ? 0 : reader.GetDecimal("std_len");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao coletar métricas de texto para {Column}", columnName);
        }
    }

    private async Task CollectDateMetricsAsync(NpgsqlConnection connection, ColumnMetricsDto result,
        string schemaName, string tableName, string columnName)
    {
        var dateQuery = $@"
            SELECT
                MIN(""{columnName}"") AS min_dt,
                MAX(""{columnName}"") AS max_dt,
                AVG((""{columnName}"" > CURRENT_DATE)::int)::numeric(5,2) AS pct_future
            FROM {schemaName}.""{tableName}""
            WHERE ""{columnName}"" IS NOT NULL;";

        try
        {
            using var cmd = new NpgsqlCommand(dateQuery, connection);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                if (!reader.IsDBNull("min_dt"))
                    result.MinDate = reader.GetDateTime("min_dt");
                if (!reader.IsDBNull("max_dt"))
                    result.MaxDate = reader.GetDateTime("max_dt");
                if (!reader.IsDBNull("pct_future"))
                    result.PctFutureDates = reader.GetDecimal("pct_future");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao coletar métricas temporais para {Column}", columnName);
        }
    }

    private async Task PersistTableMetricsAsync(TableMetricsDto dto)
    {
        var metrics = new List<TableMetric>
        {
            new() { SchemaName = dto.SchemaName, TableName = dto.TableName, MetricGroup = "volume", MetricName = "row_count", MetricValue = dto.RowCount, CollectedAt = dto.CollectedAt },
            new() { SchemaName = dto.SchemaName, TableName = dto.TableName, MetricGroup = "volume", MetricName = "table_size_mb", MetricValue = dto.TableSizeMb, CollectedAt = dto.CollectedAt },
            new() { SchemaName = dto.SchemaName, TableName = dto.TableName, MetricGroup = "volume", MetricName = "index_size_mb", MetricValue = dto.IndexSizeMb, CollectedAt = dto.CollectedAt }
        };

        _context.TableMetrics.AddRange(metrics);
        await _context.SaveChangesAsync();
    }

    private async Task PersistColumnMetricsAsync(List<ColumnMetricsDto> dtos)
    {
        var metrics = new List<ColumnMetric>();

        foreach (var dto in dtos)
        {
            metrics.AddRange(new[]
            {
                new ColumnMetric { SchemaName = dto.SchemaName, TableName = dto.TableName, ColumnName = dto.ColumnName, MetricName = "null_rate", MetricValue = dto.NullRate, CollectedAt = dto.CollectedAt },
                new ColumnMetric { SchemaName = dto.SchemaName, TableName = dto.TableName, ColumnName = dto.ColumnName, MetricName = "distinct_count", MetricValue = dto.DistinctCount, CollectedAt = dto.CollectedAt },
                new ColumnMetric { SchemaName = dto.SchemaName, TableName = dto.TableName, ColumnName = dto.ColumnName, MetricName = "duplicate_count", MetricValue = dto.DuplicateCount, CollectedAt = dto.CollectedAt }
            });

            if (dto.AvgLength.HasValue)
            {
                metrics.Add(new ColumnMetric { SchemaName = dto.SchemaName, TableName = dto.TableName, ColumnName = dto.ColumnName, MetricName = "avg_length", MetricValue = dto.AvgLength, CollectedAt = dto.CollectedAt });
                metrics.Add(new ColumnMetric { SchemaName = dto.SchemaName, TableName = dto.TableName, ColumnName = dto.ColumnName, MetricName = "std_length", MetricValue = dto.StdLength, CollectedAt = dto.CollectedAt });
            }

            if (dto.MinDate.HasValue)
            {
                metrics.Add(new ColumnMetric { SchemaName = dto.SchemaName, TableName = dto.TableName, ColumnName = dto.ColumnName, MetricName = "pct_future_dates", MetricValue = dto.PctFutureDates, CollectedAt = dto.CollectedAt });
            }
        }

        _context.ColumnMetrics.AddRange(metrics);
        await _context.SaveChangesAsync();
    }

    private static bool IsTextType(string dataType) =>
        dataType.Contains("text") || dataType.Contains("char") || dataType.Contains("varchar");

    private static bool IsDateType(string dataType) =>
        dataType.Contains("date") || dataType.Contains("timestamp");

    public async Task<DataQualityDashboardDto> GetDashboardDataAsync(string schemaName, string tableName)
    {
        var result = new DataQualityDashboardDto();

        // Obter métricas mais recentes da tabela
        var latestTableMetrics = await _context.TableMetrics
            .Where(m => m.SchemaName == schemaName && m.TableName == tableName)
            .GroupBy(m => new { m.SchemaName, m.TableName })
            .Select(g => g.OrderByDescending(m => m.CollectedAt).First().CollectedAt)
            .FirstOrDefaultAsync();

        if (latestTableMetrics != default)
        {
            var tableMetrics = await _context.TableMetrics
                .Where(m => m.SchemaName == schemaName && m.TableName == tableName && m.CollectedAt == latestTableMetrics)
                .ToListAsync();

            result.TableMetrics = new TableMetricsDto
            {
                SchemaName = schemaName,
                TableName = tableName,
                RowCount = tableMetrics.FirstOrDefault(m => m.MetricName == "row_count")?.MetricValue != null ? Convert.ToInt64(tableMetrics.FirstOrDefault(m => m.MetricName == "row_count")!.MetricValue) : null,
                TableSizeMb = tableMetrics.FirstOrDefault(m => m.MetricName == "table_size_mb")?.MetricValue,
                IndexSizeMb = tableMetrics.FirstOrDefault(m => m.MetricName == "index_size_mb")?.MetricValue,
                CollectedAt = latestTableMetrics
            };
        }

        // Obter métricas das colunas
        var latestColumnMetrics = await _context.ColumnMetrics
            .Where(m => m.SchemaName == schemaName && m.TableName == tableName)
            .GroupBy(m => new { m.SchemaName, m.TableName, m.ColumnName })
            .Select(g => new { g.Key.ColumnName, LatestDate = g.Max(m => m.CollectedAt) })
            .ToListAsync();

        foreach (var col in latestColumnMetrics)
        {
            var columnMetrics = await _context.ColumnMetrics
                .Where(m => m.SchemaName == schemaName && m.TableName == tableName &&
                           m.ColumnName == col.ColumnName && m.CollectedAt == col.LatestDate)
                .ToListAsync();

            var columnDto = new ColumnMetricsDto
            {
                SchemaName = schemaName,
                TableName = tableName,
                ColumnName = col.ColumnName,
                NullRate = columnMetrics.FirstOrDefault(m => m.MetricName == "null_rate")?.MetricValue,
                DistinctCount = columnMetrics.FirstOrDefault(m => m.MetricName == "distinct_count")?.MetricValue != null ? Convert.ToInt64(columnMetrics.FirstOrDefault(m => m.MetricName == "distinct_count")!.MetricValue) : null,
                DuplicateCount = columnMetrics.FirstOrDefault(m => m.MetricName == "duplicate_count")?.MetricValue != null ? Convert.ToInt64(columnMetrics.FirstOrDefault(m => m.MetricName == "duplicate_count")!.MetricValue) : null,
                AvgLength = columnMetrics.FirstOrDefault(m => m.MetricName == "avg_length")?.MetricValue,
                StdLength = columnMetrics.FirstOrDefault(m => m.MetricName == "std_length")?.MetricValue,
                PctFutureDates = columnMetrics.FirstOrDefault(m => m.MetricName == "pct_future_dates")?.MetricValue,
                CollectedAt = col.LatestDate
            };

            result.ColumnMetrics.Add(columnDto);
        }

        // Obter regras candidatas
        result.RuleCandidates = await _context.RuleCandidates
            .Where(r => r.SchemaName == schemaName && r.TableName == tableName)
            .Include(r => r.Executions.OrderByDescending(e => e.ExecutedAt).Take(1))
            .ToListAsync();

        return result;
    }
}