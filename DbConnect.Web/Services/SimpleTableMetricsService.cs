using DbConnect.Core.Models;
using DbConnect.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;

namespace DbConnect.Web.Services;

/// <summary>
/// Versão simplificada do serviço de métricas essenciais
/// </summary>
public class SimpleTableMetricsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SimpleTableMetricsService> _logger;

    public SimpleTableMetricsService(AppDbContext db, ILogger<SimpleTableMetricsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Coleta métricas básicas de uma tabela
    /// </summary>
    public async Task<TableEssentialMetricsDto> CollectBasicMetricsAsync(
        int userId, ConnectionProfile profile, string schema, string tableName)
    {
        if (profile.Kind != DbKind.PostgreSql)
            throw new NotSupportedException("Apenas PostgreSQL suportado");

        Console.WriteLine($"📊 Coletando métricas básicas para {schema}.{tableName}...");

        await using var conn = new NpgsqlConnection(profile.ConnectionString);
        await conn.OpenAsync();

        var quotedTable = $"\"{schema}\".\"{tableName}\"";

        // 1. Métricas básicas da tabela
        var totalRowsSql = $"SELECT COUNT(*) FROM {quotedTable}";
        await using var totalCmd = new NpgsqlCommand(totalRowsSql, conn);
        var totalRows = Convert.ToInt64(await totalCmd.ExecuteScalarAsync());

        // 2. Informações das colunas
        var columnsSql = @"
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE table_schema = $1 AND table_name = $2
            ORDER BY ordinal_position";

        await using var colCmd = new NpgsqlConnection(profile.ConnectionString);
        await colCmd.OpenAsync();
        await using var cmd = new NpgsqlCommand(columnsSql, colCmd);
        cmd.Parameters.AddWithValue(schema);
        cmd.Parameters.AddWithValue(tableName);

        var columnInfos = new List<(string Name, string DataType, bool IsNullable)>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString(0);
            var dataType = reader.GetString(1);
            var isNullable = reader.GetString(2) == "YES";

            columnInfos.Add((columnName, dataType, isNullable));
        }

        await reader.CloseAsync();
        await colCmd.CloseAsync();

        // 3. Calcular métricas para cada coluna
        var columns = new List<ColumnEssentialDto>();
        foreach (var (name, dataType, isNullable) in columnInfos)
        {
            var columnMetrics = await CalculateBasicColumnMetricsAsync(conn, quotedTable, name, dataType, totalRows);
            columnMetrics.ColumnName = name;
            columnMetrics.DataType = dataType;
            columnMetrics.IsNullable = isNullable;

            columns.Add(columnMetrics);
        }

        // 4. Calcular estatísticas de duplicatas (simplificado)
        var duplicateDetails = await CalculateDuplicateDetailsAsync(conn, quotedTable, totalRows);

        // 5. Criar resultado
        var general = new TableGeneralMetrics
        {
            TotalRows = totalRows,
            TotalColumns = columns.Count,
            ColumnsWithNulls = columns.Count(c => c.NullValues > 0),
            OverallCompleteness = columns.Count > 0 ?
                Math.Round(columns.Average(c => c.CompletenessRate), 2) : 100,
            EstimatedSizeBytes = await EstimateTableSizeAsync(conn, quotedTable),
            DuplicateRows = duplicateDetails.TotalDuplicates,
            DuplicationRate = totalRows > 0 ? Math.Round((double)duplicateDetails.TotalDuplicates / totalRows * 100, 2) : 0,
            PrimaryKeyColumns = "Detectando...",
            DuplicateDetails = duplicateDetails,
            ColumnsWithNullsSample = columns.Where(c => c.NullValues > 0).Select(c => c.ColumnName).ToList()
        };

        return new TableEssentialMetricsDto
        {
            Schema = schema,
            TableName = tableName,
            CollectedAt = DateTime.UtcNow,
            General = general,
            Columns = columns
        };
    }

    private async Task<ColumnEssentialDto> CalculateBasicColumnMetricsAsync(
        NpgsqlConnection conn, string quotedTable, string columnName, string dataType, long totalRows)
    {
        var quotedColumn = $"\"{columnName}\"";

        // Contar nulos
        var nullSql = $"SELECT COUNT(*) FROM {quotedTable} WHERE {quotedColumn} IS NULL";
        await using var nullCmd = new NpgsqlCommand(nullSql, conn);
        var nullCount = Convert.ToInt64(await nullCmd.ExecuteScalarAsync());

        // Contar valores únicos
        var uniqueSql = $"SELECT COUNT(DISTINCT {quotedColumn}) FROM {quotedTable} WHERE {quotedColumn} IS NOT NULL";
        await using var uniqueCmd = new NpgsqlCommand(uniqueSql, conn);
        var uniqueCount = Convert.ToInt64(await uniqueCmd.ExecuteScalarAsync());

        var filledValues = totalRows - nullCount;
        var completenessRate = totalRows > 0 ? (double)filledValues / totalRows * 100 : 0;
        var cardinalityRate = filledValues > 0 ? (double)uniqueCount / filledValues * 100 : 0;

        // Calcular top valores e anomalias
        var topValues = await CalculateTopValuesAsync(conn, quotedTable, quotedColumn, totalRows);
        var anomalies = DetectColumnAnomalies(topValues, filledValues, columnName);
        var sampleNulls = await GetSampleNullRowsAsync(conn, quotedTable, quotedColumn);

        // Classificar tipo de coluna para otimizar análise
        var typeClassification = ClassifyColumnType(columnName, dataType, cardinalityRate, topValues);

        // Calcular estatísticas descritivas específicas por tipo
        var numericStats = await CalculateNumericStatsAsync(conn, quotedTable, quotedColumn, dataType);
        var dateStats = await CalculateDateStatsAsync(conn, quotedTable, quotedColumn, dataType);
        var textStats = await CalculateTextStatsAsync(conn, quotedTable, quotedColumn, dataType);
        var booleanStats = await CalculateBooleanStatsAsync(conn, quotedTable, quotedColumn, dataType);

        // Aplicar otimizações baseadas no tipo de coluna
        var optimizedTopValues = OptimizeTopValuesForColumnType(typeClassification, topValues);
        var smartRecommendation = GenerateSmartRecommendation(typeClassification, anomalies, completenessRate, cardinalityRate, topValues);

        // Gerar visualizações específicas por tipo
        var timeline = await GenerateTimelineAsync(conn, quotedTable, quotedColumn, typeClassification);
        var geographicPoints = await GenerateGeographicPointsAsync(conn, quotedTable, quotedColumn, typeClassification);

        return new ColumnEssentialDto
        {
            TotalValues = totalRows,
            NullValues = nullCount,
            EmptyValues = 0, // Simplificado
            CompletenessRate = Math.Round(completenessRate, 2),
            UniqueValues = uniqueCount,
            CardinalityRate = Math.Round(cardinalityRate, 2),
            TypeClassification = typeClassification,
            TopValues = optimizedTopValues,
            SampleNullRows = sampleNulls,
            QualityAnomalies = anomalies,
            Distribution = new DistributionInsights
            {
                HasSuspiciousFrequency = anomalies.Any(a => a.Type == "suspicious_frequency"),
                HasPatternViolations = anomalies.Any(a => a.Type == "pattern_violation"),
                UniformityScore = CalculateUniformityScore(topValues, filledValues),
                RecommendedAction = smartRecommendation
            },
            // Estatísticas específicas por tipo
            Numeric = numericStats,
            Date = dateStats,
            Text = textStats,
            Boolean = booleanStats,

            // Visualizações específicas
            Timeline = timeline,
            GeographicPoints = geographicPoints
        };
    }

    private async Task<DuplicateRowsDetail> CalculateDuplicateDetailsAsync(NpgsqlConnection conn, string quotedTable, long totalRows)
    {
        try
        {
            // Primeiro, contar duplicatas baseado em MD5 hash de todas as colunas
            var duplicateCountSql = $@"
                WITH table_hash AS (
                    SELECT ctid, MD5(t::text) as row_hash
                    FROM {quotedTable} t
                ),
                duplicates AS (
                    SELECT row_hash, COUNT(*) as cnt
                    FROM table_hash
                    GROUP BY row_hash
                    HAVING COUNT(*) > 1
                )
                SELECT COALESCE(SUM(cnt - 1), 0) as duplicate_count
                FROM duplicates";

            await using var countCmd = new NpgsqlCommand(duplicateCountSql, conn);
            var duplicateCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync());

            var sampleDuplicateRows = new List<string>();
            var duplicateGroups = new List<DuplicateGroup>();

            // Se há duplicatas, buscar exemplos
            if (duplicateCount > 0)
            {
                // Buscar sample de CTIDs das linhas duplicadas
                var sampleSql = $@"
                    WITH table_hash AS (
                        SELECT ctid, MD5(t::text) as row_hash
                        FROM {quotedTable} t
                    ),
                    duplicate_hashes AS (
                        SELECT row_hash
                        FROM table_hash
                        GROUP BY row_hash
                        HAVING COUNT(*) > 1
                        LIMIT 3
                    )
                    SELECT th.ctid::text
                    FROM table_hash th
                    INNER JOIN duplicate_hashes dh ON th.row_hash = dh.row_hash
                    LIMIT 5";

                await using var sampleCmd = new NpgsqlCommand(sampleSql, conn);
                await using var sampleReader = await sampleCmd.ExecuteReaderAsync();

                while (await sampleReader.ReadAsync())
                {
                    sampleDuplicateRows.Add($"Row ID: {sampleReader.GetString(0)}");
                }
                await sampleReader.CloseAsync();

                // Criar um grupo de duplicata básico
                if (duplicateCount > 0)
                {
                    duplicateGroups.Add(new DuplicateGroup
                    {
                        HashKey = "all_columns",
                        Count = duplicateCount,
                        AffectedColumns = new List<string> { "Todas as colunas" },
                        SampleRowIds = sampleDuplicateRows.Take(3).ToList()
                    });
                }
            }

            return new DuplicateRowsDetail
            {
                TotalDuplicates = duplicateCount,
                DuplicateGroups = duplicateGroups,
                SampleDuplicateRows = sampleDuplicateRows
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro calculando duplicatas: {ex.Message}");
            // Se der erro (ex: tabela muito grande), retornar valores padrão
            return new DuplicateRowsDetail { TotalDuplicates = 0 };
        }
    }

    private async Task<long> EstimateTableSizeAsync(NpgsqlConnection conn, string quotedTable)
    {
        try
        {
            var sizeSql = $"SELECT pg_total_relation_size('{quotedTable}'::regclass)";
            await using var cmd = new NpgsqlCommand(sizeSql, conn);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync());
        }
        catch
        {
            return 0; // Se der erro, retornar 0
        }
    }

    private async Task<List<ValueFrequency>> CalculateTopValuesAsync(
        NpgsqlConnection conn, string quotedTable, string quotedColumn, long totalRows)
    {
        try
        {
            var topValuesSql = $@"
                SELECT {quotedColumn}::text as value, COUNT(*) as count
                FROM {quotedTable}
                WHERE {quotedColumn} IS NOT NULL
                GROUP BY {quotedColumn}
                ORDER BY count DESC
                LIMIT 10";

            await using var cmd = new NpgsqlCommand(topValuesSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            var topValues = new List<ValueFrequency>();
            while (await reader.ReadAsync())
            {
                var value = reader.GetString(0);
                var count = reader.GetInt64(1);
                var percentage = totalRows > 0 ? Math.Round((double)count / totalRows * 100, 2) : 0;

                topValues.Add(new ValueFrequency
                {
                    Value = value,
                    Count = count,
                    Percentage = percentage
                });
            }

            return topValues;
        }
        catch
        {
            return new List<ValueFrequency>();
        }
    }

    private async Task<List<string>> GetSampleNullRowsAsync(
        NpgsqlConnection conn, string quotedTable, string quotedColumn)
    {
        try
        {
            var nullSampleSql = $@"
                SELECT ctid::text
                FROM {quotedTable}
                WHERE {quotedColumn} IS NULL
                LIMIT 5";

            await using var cmd = new NpgsqlCommand(nullSampleSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            var samples = new List<string>();
            while (await reader.ReadAsync())
            {
                samples.Add($"Row #{reader.GetString(0)}");
            }

            return samples;
        }
        catch
        {
            return new List<string>();
        }
    }

    private List<DataQualityAnomaly> DetectColumnAnomalies(List<ValueFrequency> topValues, long filledValues, string columnName)
    {
        var anomalies = new List<DataQualityAnomaly>();

        foreach (var value in topValues)
        {
            // Detectar frequência suspeita (valores que aparecem muito)
            if (value.Percentage > 50 && filledValues > 100)
            {
                anomalies.Add(new DataQualityAnomaly
                {
                    Type = "suspicious_frequency",
                    Description = $"Valor '{value.Value}' aparece em {value.Percentage:F1}% dos registros - suspeito de duplicação ou erro",
                    Value = value.Value,
                    Count = value.Count,
                    Severity = Math.Min(value.Percentage / 100.0, 1.0),
                    SampleRows = new List<string> { $"Aparece {value.Count} vezes" }
                });
            }

            // Detectar padrões violados (emails, etc)
            if (columnName.ToLower().Contains("email") && !IsValidEmailPattern(value.Value))
            {
                anomalies.Add(new DataQualityAnomaly
                {
                    Type = "pattern_violation",
                    Description = $"Valor '{value.Value}' não parece ser um email válido",
                    Value = value.Value,
                    Count = value.Count,
                    Severity = 0.7,
                    SampleRows = new List<string> { $"Formato inválido: {value.Value}" }
                });
            }
        }

        return anomalies;
    }

    private double CalculateUniformityScore(List<ValueFrequency> topValues, long filledValues)
    {
        if (!topValues.Any()) return 1.0;

        // Score baseado na distribuição dos valores
        var entropy = topValues.Sum(v => {
            var p = (double)v.Count / filledValues;
            return p * Math.Log2(p);
        }) * -1;

        var maxEntropy = Math.Log2(Math.Min(topValues.Count, filledValues));
        return maxEntropy > 0 ? Math.Min(entropy / maxEntropy, 1.0) : 1.0;
    }

    private string GenerateRecommendation(List<DataQualityAnomaly> anomalies, double completenessRate, double cardinalityRate)
    {
        if (anomalies.Any(a => a.Type == "suspicious_frequency"))
            return "⚠️ Investigar valores com alta frequência - podem indicar erros de entrada de dados";

        if (completenessRate < 80)
            return "📝 Considerar regras para tratar valores nulos ou implementar validação obrigatória";

        if (cardinalityRate < 10)
            return "🔍 Baixa variabilidade de dados - verificar se é esperado ou se há limitação na coleta";

        return "✅ Qualidade dos dados aparenta estar adequada";
    }

    private bool IsValidEmailPattern(string value)
    {
        return value.Contains("@") && value.Contains(".") && !value.StartsWith("@") && !value.EndsWith("@");
    }

    private async Task<NumericStats?> CalculateNumericStatsAsync(
        NpgsqlConnection conn, string quotedTable, string quotedColumn, string dataType)
    {
        // Verificar se é coluna numérica
        var numericTypes = new[] { "integer", "bigint", "smallint", "decimal", "numeric", "real", "double precision", "money" };
        if (!numericTypes.Any(t => dataType.ToLower().Contains(t)))
            return null;

        try
        {
            // Calcular estatísticas básicas e percentis
            var statsSql = $@"
                SELECT
                    MIN({quotedColumn}::numeric) as min_val,
                    MAX({quotedColumn}::numeric) as max_val,
                    AVG({quotedColumn}::numeric) as avg_val,
                    STDDEV({quotedColumn}::numeric) as stddev_val,
                    PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY {quotedColumn}::numeric) as p25,
                    PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY {quotedColumn}::numeric) as median_val,
                    PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY {quotedColumn}::numeric) as p75,
                    PERCENTILE_CONT(0.9) WITHIN GROUP (ORDER BY {quotedColumn}::numeric) as p90,
                    PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY {quotedColumn}::numeric) as p95
                FROM {quotedTable}
                WHERE {quotedColumn} IS NOT NULL AND {quotedColumn}::text ~ '^-?[0-9]+\.?[0-9]*$'";

            await using var cmd = new NpgsqlCommand(statsSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                // Usar Convert.ToDecimal para lidar com double precision
                var min = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0));
                var max = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1));
                var avg = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2));
                var stdDev = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3));
                var p25 = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4));
                var median = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5));
                var p75 = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetValue(6));
                var p90 = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader.GetValue(7));
                var p95 = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader.GetValue(8));

                await reader.CloseAsync();

                // Calcular histograma e outliers
                var distribution = await CalculateHistogramAsync(conn, quotedTable, quotedColumn, min, max);
                var (outlierCount, outlierSamples) = await DetectOutliersAsync(conn, quotedTable, quotedColumn, p25, p75);

                return new NumericStats
                {
                    Min = min,
                    Max = max,
                    Avg = avg,
                    StdDev = stdDev,
                    Median = median,
                    P25 = p25,
                    P75 = p75,
                    P90 = p90,
                    P95 = p95,
                    Distribution = distribution,
                    OutlierCount = outlierCount,
                    OutlierSamples = outlierSamples
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro calculando estatísticas numéricas: {ex.Message}");
        }

        return null;
    }

    private async Task<DateStats?> CalculateDateStatsAsync(
        NpgsqlConnection conn, string quotedTable, string quotedColumn, string dataType)
    {
        // Verificar se é coluna de data/hora
        var dateTypes = new[] { "date", "timestamp", "timestamptz", "time", "timetz" };
        if (!dateTypes.Any(t => dataType.ToLower().Contains(t)))
            return null;

        try
        {
            var statsSql = $@"
                SELECT
                    MIN({quotedColumn}) as min_date,
                    MAX({quotedColumn}) as max_date
                FROM {quotedTable}
                WHERE {quotedColumn} IS NOT NULL";

            await using var cmd = new NpgsqlCommand(statsSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new DateStats
                {
                    Min = reader.IsDBNull(0) ? DateTime.MinValue : reader.GetDateTime(0),
                    Max = reader.IsDBNull(1) ? DateTime.MinValue : reader.GetDateTime(1)
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro calculando estatísticas de data: {ex.Message}");
        }

        return null;
    }

    private async Task<TextStats?> CalculateTextStatsAsync(
        NpgsqlConnection conn, string quotedTable, string quotedColumn, string dataType)
    {
        // Verificar se é coluna de texto
        var textTypes = new[] { "text", "varchar", "char", "character" };
        if (!textTypes.Any(t => dataType.ToLower().Contains(t)))
            return null;

        try
        {
            var statsSql = $@"
                SELECT
                    MIN(LENGTH({quotedColumn})) as min_length,
                    MAX(LENGTH({quotedColumn})) as max_length,
                    AVG(LENGTH({quotedColumn})) as avg_length
                FROM {quotedTable}
                WHERE {quotedColumn} IS NOT NULL AND {quotedColumn} != ''";

            await using var cmd = new NpgsqlCommand(statsSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new TextStats
                {
                    MinLength = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    MaxLength = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    AvgLength = reader.IsDBNull(2) ? 0 : reader.GetDouble(2)
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro calculando estatísticas de texto: {ex.Message}");
        }

        return null;
    }

    private ColumnTypeClassification ClassifyColumnType(string columnName, string dataType, double cardinalityRate, List<ValueFrequency> topValues)
    {
        // DEBUG: Mostrar detalhes da classificação
        Console.WriteLine($"🔍 DEBUG: Classificando {columnName} | Tipo: {dataType} | Cardinalidade: {cardinalityRate:F1}%");

        // Detectar colunas numéricas PRIMEIRO (antes de verificar IDs)
        var numericTypes = new[] { "integer", "bigint", "smallint", "decimal", "numeric", "real", "double precision", "money" };
        var isNumericType = numericTypes.Any(t => dataType.ToLower().Contains(t));

        // PRIORIDADE 1: Detectar colunas de data/hora ANTES de IDs para evitar conflitos
        var dateTypes = new[] { "timestamp", "date", "time", "datetime" };
        if (dateTypes.Any(t => dataType.ToLower().Contains(t)) ||
            columnName.ToLower().Contains("dt_") || columnName.ToLower().Contains("date") ||
            columnName.ToLower().Contains("created_at") || columnName.ToLower().Contains("updated_at") ||
            columnName.ToLower().Contains("criacao") || columnName.ToLower().Contains("atualizacao"))
        {
            Console.WriteLine($"🕒 DEBUG: Coluna {columnName} ({dataType}) classificada como DateTime");
            return ColumnTypeClassification.DateTime;
        }

        // PRIORIDADE 2: Detectar IDs (alta cardinalidade) - mas não se for campo de valor/renda/data
        var isValueField = columnName.ToLower().Contains("vl_") ||
                          columnName.ToLower().Contains("valor") ||
                          columnName.ToLower().Contains("renda") ||
                          columnName.ToLower().Contains("preco") ||
                          columnName.ToLower().Contains("price");

        var isDateField = columnName.ToLower().Contains("dt_") ||
                         columnName.ToLower().Contains("date") ||
                         columnName.ToLower().Contains("created_at") ||
                         columnName.ToLower().Contains("updated_at") ||
                         columnName.ToLower().Contains("criacao") ||
                         columnName.ToLower().Contains("atualizacao");

        if (cardinalityRate >= 95.0 && !isValueField && !isDateField)
        {
            return ColumnTypeClassification.UniqueId;
        }

        // Verificar se é ID pelos nomes, mas excluir campos de valor e data
        if (!isValueField && !isDateField && (columnName.ToLower().Contains("id") ||
            columnName.ToLower().Contains("uuid") || columnName.ToLower().Contains("guid")))
        {
            return ColumnTypeClassification.UniqueId;
        }

        // Detectar colunas booleanas
        var booleanTypes = new[] { "boolean", "bool" };
        if (booleanTypes.Any(t => dataType.ToLower().Contains(t)))
        {
            return ColumnTypeClassification.Boolean;
        }

        // Detectar colunas numéricas (usando a variável já definida)
        if (isNumericType)
        {
            return ColumnTypeClassification.Numeric;
        }


        // Detectar coordenadas geográficas
        if (isNumericType && (
            columnName.ToLower().Contains("lat") || columnName.ToLower().Contains("lon") ||
            columnName.ToLower().Contains("lng") || columnName.ToLower().Contains("longitude") ||
            columnName.ToLower().Contains("latitude") || columnName.ToLower().Contains("coord")))
        {
            return ColumnTypeClassification.Geographic;
        }

        // Detectar colunas categóricas (baixa cardinalidade)
        if (cardinalityRate <= 10.0 && topValues.Count <= 50)
        {
            return ColumnTypeClassification.Categorical;
        }

        // Detectar texto livre
        var textTypes = new[] { "text", "varchar", "char", "character" };
        if (textTypes.Any(t => dataType.ToLower().Contains(t)))
        {
            return ColumnTypeClassification.Text;
        }

        return ColumnTypeClassification.Other;
    }

    private List<ValueFrequency> OptimizeTopValuesForColumnType(ColumnTypeClassification typeClassification, List<ValueFrequency> topValues)
    {
        switch (typeClassification)
        {
            case ColumnTypeClassification.UniqueId:
                // Para IDs com 100% de cardinalidade, remover top values
                return new List<ValueFrequency>();

            case ColumnTypeClassification.DateTime:
                // Para datas, remover top values (mostrar apenas min/max)
                return new List<ValueFrequency>();

            case ColumnTypeClassification.Boolean:
                // Para booleanos, manter apenas os valores true/false/null
                return topValues.Where(v => v.Value.ToLower() == "true" || v.Value.ToLower() == "false" || v.Value == "").ToList();

            case ColumnTypeClassification.Numeric:
            case ColumnTypeClassification.Categorical:
            case ColumnTypeClassification.Text:
            default:
                // Manter top values para estes tipos
                return topValues;
        }
    }

    private string GenerateSmartRecommendation(ColumnTypeClassification typeClassification,
        List<DataQualityAnomaly> anomalies, double completenessRate, double cardinalityRate,
        List<ValueFrequency> topValues)
    {
        // Recomendações específicas por tipo de coluna
        switch (typeClassification)
        {
            case ColumnTypeClassification.UniqueId:
                if (cardinalityRate >= 99.0)
                    return "✅ Campo identificador único funcionando corretamente";
                else
                    return "⚠️ Campo identificador com duplicatas - verificar integridade";

            case ColumnTypeClassification.Numeric:
                if (anomalies.Any(a => a.Type == "suspicious_frequency"))
                {
                    var suspiciousValues = topValues.Where(v => v.Percentage > 30).ToList();
                    if (suspiciousValues.Any())
                    {
                        var examples = string.Join(", ", suspiciousValues.Take(3).Select(v => v.Value));
                        return $"⚠️ Valores concentrados suspeitos: {examples} - verificar se é esperado ou erro de coleta";
                    }
                    return "⚠️ Distribuição numérica pouco variada - investigar processo de coleta";
                }
                if (completenessRate < 80)
                    return "📝 Muitos valores nulos em campo numérico - considerar valores padrão ou validação obrigatória";
                return "✅ Distribuição numérica adequada";

            case ColumnTypeClassification.DateTime:
                if (completenessRate < 90)
                    return "📅 Datas incompletas - considerar timestamps automáticos para novos registros";
                return "✅ Dados temporais completos";

            case ColumnTypeClassification.Boolean:
                var trueFalseBalance = Math.Abs(
                    topValues.Where(v => v.Value.ToLower() == "true").Sum(v => v.Percentage) -
                    topValues.Where(v => v.Value.ToLower() == "false").Sum(v => v.Percentage)
                );

                if (completenessRate < 95)
                    return "🔘 Valores booleanos incompletos - considerar valor padrão (true/false)";
                else if (trueFalseBalance > 80)
                    return "⚖️ Campo booleano muito desbalanceado - verificar se é esperado";
                else
                    return "✅ Campo booleano balanceado e completo";

            case ColumnTypeClassification.Categorical:
                if (cardinalityRate > 50)
                    return "🏷️ Muitas categorias diferentes - considerar agrupamento ou revisão das regras";
                if (completenessRate < 95)
                    return "📋 Valores categóricos faltando - implementar validação de lista";
                return "✅ Categorização adequada";

            default:
                if (completenessRate < 80)
                    return "📝 Considerar regras para tratar valores nulos ou implementar validação obrigatória";
                return "✅ Qualidade dos dados aparenta estar adequada";
        }
    }

    private async Task<List<HistogramBucket>> CalculateHistogramAsync(
        NpgsqlConnection conn, string quotedTable, string quotedColumn, decimal min, decimal max)
    {
        try
        {
            if (min == max) return new List<HistogramBucket>();

            // Para distribuições muito concentradas, usar percentis ao invés de divisão linear
            var range = max - min;
            var totalRowsCmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {quotedTable} WHERE {quotedColumn} IS NOT NULL", conn);
            var totalRows = Convert.ToInt64(await totalRowsCmd.ExecuteScalarAsync());

            // Usar 20 buckets lineares fixos (preferência do usuário)
            int bucketCount = 20;
            List<decimal> bucketBoundaries = CalculateLinearBoundaries(min, max, bucketCount);
            // Criar buckets usando os boundaries inteligentes
            var buckets = new List<HistogramBucket>();

            for (int i = 0; i < bucketBoundaries.Count - 1; i++)
            {
                var rangeStart = bucketBoundaries[i];
                var rangeEnd = bucketBoundaries[i + 1];

                var countSql = $@"
                    SELECT COUNT(*)
                    FROM {quotedTable}
                    WHERE {quotedColumn}::numeric >= {rangeStart}
                      AND {quotedColumn}::numeric {(i == bucketBoundaries.Count - 2 ? "<=" : "<")} {rangeEnd}
                      AND {quotedColumn} IS NOT NULL";

                await using var cmd = new NpgsqlCommand(countSql, conn);
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                buckets.Add(new HistogramBucket
                {
                    RangeStart = rangeStart,
                    RangeEnd = rangeEnd,
                    Count = count,
                    Percentage = 0 // Será calculado depois
                });
            }

            // Calcular percentuais
            var totalCount = buckets.Sum(b => b.Count);
            if (totalCount > 0)
            {
                foreach (var bucket in buckets)
                {
                    bucket.Percentage = Math.Round((bucket.Count * 100.0) / totalCount, 2);
                }
            }

            return buckets;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Erro calculando histograma: {ex.Message}");
            return new List<HistogramBucket>();
        }
    }

    private async Task<(bool IsHighlyConcentrated, decimal ConcentrationThreshold)> CheckConcentrationAsync(
        NpgsqlConnection conn, string quotedTable, string quotedColumn, decimal min, decimal max)
    {
        try
        {
            var range = max - min;
            var firstTenPercent = min + (range * 0.1m);

            // Contar quantos valores estão nos primeiros 10% do range
            var concentratedCountSql = $@"
                SELECT COUNT(*)
                FROM {quotedTable}
                WHERE {quotedColumn}::numeric <= {firstTenPercent}
                  AND {quotedColumn} IS NOT NULL";

            var totalCountSql = $@"
                SELECT COUNT(*)
                FROM {quotedTable}
                WHERE {quotedColumn} IS NOT NULL";

            await using var concentratedCmd = new NpgsqlCommand(concentratedCountSql, conn);
            await using var totalCmd = new NpgsqlCommand(totalCountSql, conn);

            var concentratedCount = Convert.ToInt64(await concentratedCmd.ExecuteScalarAsync());
            var totalCount = Convert.ToInt64(await totalCmd.ExecuteScalarAsync());

            var concentrationRate = totalCount > 0 ? (double)concentratedCount / totalCount : 0;

            // Se 80% ou mais dos dados estão nos primeiros 10% do range, é altamente concentrado
            return (concentrationRate >= 0.8, (decimal)concentrationRate);
        }
        catch
        {
            return (false, 0);
        }
    }

    private async Task<List<decimal>> CalculatePercentileBasedBoundariesAsync(
        NpgsqlConnection conn, string quotedTable, string quotedColumn, int bucketCount)
    {
        try
        {
            // Gerar percentis dinamicamente baseado no número de buckets
            var boundaries = new List<decimal>();
            var step = 100.0 / bucketCount;

            var percentileQueries = new List<string>();
            for (int i = 0; i <= bucketCount; i++)
            {
                var percentile = Math.Min(i * step / 100.0, 1.0);
                percentileQueries.Add($"PERCENTILE_CONT({percentile:F3}) WITHIN GROUP (ORDER BY {quotedColumn}::numeric) as p{i}");
            }

            var percentilesSql = $@"
                SELECT {string.Join(",\n                    ", percentileQueries)}
                FROM {quotedTable}
                WHERE {quotedColumn} IS NOT NULL";

            await using var percCmd = new NpgsqlCommand(percentilesSql, conn);
            await using var reader = await percCmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                for (int i = 0; i <= bucketCount; i++)
                {
                    var value = reader.IsDBNull(i) ? 0 : Convert.ToDecimal(reader.GetValue(i));
                    boundaries.Add(value);
                }
            }

            return boundaries;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Erro calculando boundaries baseados em percentis: {ex.Message}");
            return new List<decimal>();
        }
    }

    private List<decimal> CalculateLinearBoundaries(decimal min, decimal max, int bucketCount)
    {
        var boundaries = new List<decimal>();
        var range = max - min;
        var bucketSize = range / bucketCount;

        for (int i = 0; i <= bucketCount; i++)
        {
            boundaries.Add(min + (bucketSize * i));
        }

        return boundaries;
    }

    private async Task<(int outlierCount, List<decimal> outlierSamples)> DetectOutliersAsync(
        NpgsqlConnection conn, string quotedTable, string quotedColumn, decimal p25, decimal p75)
    {
        try
        {
            // Usar método IQR (Interquartile Range) para detectar outliers
            var iqr = p75 - p25;
            var lowerBound = p25 - (1.5m * iqr);
            var upperBound = p75 + (1.5m * iqr);

            // Contar outliers
            var countSql = $@"
                SELECT COUNT(*)
                FROM {quotedTable}
                WHERE {quotedColumn}::numeric < {lowerBound}
                   OR {quotedColumn}::numeric > {upperBound}
                   AND {quotedColumn} IS NOT NULL";

            await using var countCmd = new NpgsqlCommand(countSql, conn);
            var outlierCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            // Buscar sample de outliers (máximo 5)
            var sampleSql = $@"
                SELECT {quotedColumn}::numeric
                FROM {quotedTable}
                WHERE ({quotedColumn}::numeric < {lowerBound} OR {quotedColumn}::numeric > {upperBound})
                  AND {quotedColumn} IS NOT NULL
                LIMIT 5";

            await using var sampleCmd = new NpgsqlCommand(sampleSql, conn);
            await using var reader = await sampleCmd.ExecuteReaderAsync();

            var samples = new List<decimal>();
            while (await reader.ReadAsync())
            {
                samples.Add(Convert.ToDecimal(reader.GetValue(0)));
            }

            return (outlierCount, samples);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Erro detectando outliers: {ex.Message}");
            return (0, new List<decimal>());
        }
    }

    private async Task<BooleanStats?> CalculateBooleanStatsAsync(
        NpgsqlConnection conn, string quotedTable, string quotedColumn, string dataType)
    {
        // Verificar se é coluna booleana
        var booleanTypes = new[] { "boolean", "bool" };
        if (!booleanTypes.Any(t => dataType.ToLower().Contains(t)))
            return null;

        try
        {
            var statsSql = $@"
                SELECT
                    SUM(CASE WHEN {quotedColumn} = true THEN 1 ELSE 0 END) as true_count,
                    SUM(CASE WHEN {quotedColumn} = false THEN 1 ELSE 0 END) as false_count,
                    SUM(CASE WHEN {quotedColumn} IS NULL THEN 1 ELSE 0 END) as null_count,
                    COUNT(*) as total_count
                FROM {quotedTable}";

            await using var cmd = new NpgsqlCommand(statsSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var trueCount = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                var falseCount = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                var nullCount = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                var totalCount = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);

                return new BooleanStats
                {
                    TrueCount = trueCount,
                    FalseCount = falseCount,
                    NullCount = nullCount,
                    TruePercentage = totalCount > 0 ? Math.Round((trueCount * 100.0) / totalCount, 2) : 0,
                    FalsePercentage = totalCount > 0 ? Math.Round((falseCount * 100.0) / totalCount, 2) : 0,
                    NullPercentage = totalCount > 0 ? Math.Round((nullCount * 100.0) / totalCount, 2) : 0
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Erro calculando estatísticas booleanas: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Gera timeline para visualização de dados temporais
    /// </summary>
    private async Task<List<TimelineBucket>> GenerateTimelineAsync(
        NpgsqlConnection conn, string quotedTable, string quotedColumn, ColumnTypeClassification typeClassification)
    {
        var timeline = new List<TimelineBucket>();

        Console.WriteLine($"📅 DEBUG: GenerateTimelineAsync chamado para {quotedColumn} com tipo {typeClassification}");

        if (typeClassification != ColumnTypeClassification.DateTime)
        {
            Console.WriteLine($"⚠️ DEBUG: Coluna {quotedColumn} não é DateTime, retornando timeline vazia");
            return timeline;
        }

        try
        {
            // Agrupar por mês para criar timeline mensal
            var timelineSql = $@"
                SELECT
                    DATE_TRUNC('month', {quotedColumn}) as period,
                    COUNT(*) as count,
                    COUNT(*) * 100.0 / SUM(COUNT(*)) OVER() as percentage
                FROM {quotedTable}
                WHERE {quotedColumn} IS NOT NULL
                GROUP BY DATE_TRUNC('month', {quotedColumn})
                ORDER BY period
                LIMIT 50"; // Limitar para não sobrecarregar o gráfico

            await using var cmd = new NpgsqlCommand(timelineSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var period = reader.GetDateTime(0);
                var count = reader.GetInt64(1);
                var percentage = reader.GetDouble(2);

                timeline.Add(new TimelineBucket
                {
                    Period = period,
                    Count = count,
                    Percentage = Math.Round(percentage, 2),
                    Label = period.ToString("MMM yyyy") // "Jan 2023"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Erro gerando timeline: {ex.Message}");
        }

        return timeline;
    }

    /// <summary>
    /// Gera pontos geográficos para visualização em mapa
    /// </summary>
    private async Task<List<GeographicPoint>> GenerateGeographicPointsAsync(
        NpgsqlConnection conn, string quotedTable, string quotedColumn, ColumnTypeClassification typeClassification)
    {
        var points = new List<GeographicPoint>();

        if (typeClassification != ColumnTypeClassification.Geographic)
            return points;

        try
        {
            // Para coordenadas, assumir que há colunas lat/lon na mesma tabela
            // Detectar colunas relacionadas
            var columnInfo = await DetectGeographicColumnsAsync(conn, quotedTable, quotedColumn);

            if (columnInfo.latColumn != null && columnInfo.lonColumn != null)
            {
                var geoSql = $@"
                    SELECT
                        {columnInfo.latColumn} as lat,
                        {columnInfo.lonColumn} as lon,
                        COUNT(*) as count,
                        COUNT(*) * 100.0 / SUM(COUNT(*)) OVER() as percentage
                    FROM {quotedTable}
                    WHERE {columnInfo.latColumn} IS NOT NULL
                        AND {columnInfo.lonColumn} IS NOT NULL
                        AND {columnInfo.latColumn} BETWEEN -90 AND 90
                        AND {columnInfo.lonColumn} BETWEEN -180 AND 180
                    GROUP BY {columnInfo.latColumn}, {columnInfo.lonColumn}
                    ORDER BY count DESC
                    LIMIT 100"; // Top 100 pontos mais frequentes

                await using var cmd = new NpgsqlCommand(geoSql, conn);
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var lat = Convert.ToDouble(reader.GetValue(0));
                    var lon = Convert.ToDouble(reader.GetValue(1));
                    var count = reader.GetInt64(2);
                    var percentage = reader.GetDouble(3);

                    points.Add(new GeographicPoint
                    {
                        Latitude = lat,
                        Longitude = lon,
                        Count = count,
                        Percentage = Math.Round(percentage, 2),
                        Label = $"{lat:F4}, {lon:F4}"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Erro gerando pontos geográficos: {ex.Message}");
        }

        return points;
    }

    /// <summary>
    /// Detecta colunas de latitude e longitude relacionadas
    /// </summary>
    private async Task<(string? latColumn, string? lonColumn)> DetectGeographicColumnsAsync(
        NpgsqlConnection conn, string quotedTable, string currentColumn)
    {
        try
        {
            // Listar todas as colunas da tabela
            var columnsSql = $@"
                SELECT column_name
                FROM information_schema.columns
                WHERE table_name = {quotedTable.Replace("\"", "").Replace("[", "").Replace("]", "")}
                AND data_type IN ('numeric', 'decimal', 'double precision', 'real', 'float')";

            await using var cmd = new NpgsqlCommand(columnsSql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            var columns = new List<string>();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            // Buscar padrões de latitude e longitude
            var latColumn = columns.FirstOrDefault(c =>
                c.ToLower().Contains("lat") && !c.ToLower().Contains("lon"));
            var lonColumn = columns.FirstOrDefault(c =>
                c.ToLower().Contains("lon") || c.ToLower().Contains("lng"));

            return (latColumn != null ? $"\"{latColumn}\"" : null,
                    lonColumn != null ? $"\"{lonColumn}\"" : null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Erro detectando colunas geográficas: {ex.Message}");
            return (null, null);
        }
    }
}