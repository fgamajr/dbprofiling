using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;
using Microsoft.Extensions.Options;
using DbConnect.Core.Models;
using System.Data.Common;
using System.Data.SqlClient;
using Npgsql;
using MySqlConnector;
using Microsoft.Data.Sqlite;
using Dapper;

namespace DbConnect.Web.Services;

public interface IPatternAnalysisService
{
    Task<List<AdvancedColumnMetrics>> AnalyzeTablePatterns(string connectionString, string schemaName, string tableName);
    Task<RelationshipMetrics> AnalyzeTableRelationships(string connectionString, string schemaName, string tableName);
    Task<OutlierAnalysis?> AnalyzeColumnOutliers(string connectionString, string schemaName, string tableName, string columnName, int page, int pageSize);
}

public class PatternAnalysisService : IPatternAnalysisService
{
    private readonly List<PatternValidationRule> _patternRules;
    private readonly ILogger<PatternAnalysisService> _logger;

    public PatternAnalysisService(IConfiguration configuration, ILogger<PatternAnalysisService> logger)
    {
        _logger = logger;
        _patternRules = configuration.GetSection("PatternValidationRules").Get<List<PatternValidationRule>>() ?? GetDefaultPatternRules();
    }

    private List<PatternValidationRule> GetDefaultPatternRules()
    {
        return new List<PatternValidationRule>
        {
            new PatternValidationRule { PatternName = "Email", Regex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", Description = "Formato de email válido" },
            new PatternValidationRule { PatternName = "CPF", Regex = @"^\d{3}\.\d{3}\.\d{3}-\d{2}$|^\d{11}$", Description = "Formato de CPF brasileiro" },
            new PatternValidationRule { PatternName = "CNPJ", Regex = @"^\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}$|^\d{14}$", Description = "Formato de CNPJ brasileiro" },
            new PatternValidationRule { PatternName = "Telefone BR", Regex = @"^\(\d{2}\)\s?\d{4,5}-?\d{4}$|^\d{10,11}$", Description = "Formato de telefone brasileiro" },
            new PatternValidationRule { PatternName = "CEP", Regex = @"^\d{5}-?\d{3}$", Description = "Formato de CEP brasileiro" },
            new PatternValidationRule { PatternName = "Código", Regex = @"^[A-Z]{2,4}-?\d{3,6}$", Description = "Formato de código alfanumérico" },
            new PatternValidationRule { PatternName = "URL", Regex = @"^https?://[^\s/$.?#].[^\s]*$", Description = "Formato de URL web" },
            new PatternValidationRule { PatternName = "UUID", Regex = @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", Description = "Formato de UUID" },
            new PatternValidationRule { PatternName = "Data ISO", Regex = @"^\d{4}-\d{2}-\d{2}$", Description = "Formato de data ISO" },
            new PatternValidationRule { PatternName = "Horário", Regex = @"^\d{2}:\d{2}(:\d{2})?$", Description = "Formato de horário" }
        };
    }

    // Classe interna para representar as regras de padrão
    private class PatternValidationRule
    {
        public string PatternName { get; set; } = string.Empty;
        public string Regex { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Culture { get; set; } = "pt-BR";
        public List<string> ColumnKeywords { get; set; } = new();
    }

    public async Task<List<AdvancedColumnMetrics>> AnalyzeTablePatterns(string connectionString, string schemaName, string tableName)
    {
        _logger.LogInformation("Iniciando análise de padrões para {Schema}.{Table}", schemaName, tableName);

        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogError("ConnectionString está nulo ou vazio");
            throw new ArgumentNullException(nameof(connectionString));
        }

        var results = new List<AdvancedColumnMetrics>();

        try
        {
            _logger.LogInformation("Criando conexão...");
            using var connection = CreateConnection(connectionString);
            if (connection == null)
            {
                _logger.LogError("Falha ao criar conexão");
                throw new InvalidOperationException("Não foi possível criar a conexão com o banco de dados");
            }

            _logger.LogInformation("Abrindo conexão...");
            await connection.OpenAsync();

            // Obter metadados das colunas
            _logger.LogInformation("Obtendo metadados das colunas...");
            var columns = await GetTableColumns(connection, schemaName, tableName);
            _logger.LogInformation("Encontradas {Count} colunas", columns?.Count ?? 0);

            if (columns != null && columns.Any())
            {
                foreach (var column in columns)
                {
                    _logger.LogInformation("Processando coluna {Column} ({Type})", column.ColumnName, column.DataType);

                    var columnMetrics = new AdvancedColumnMetrics
                    {
                        ColumnName = column.ColumnName ?? string.Empty,
                        DataType = column.DataType ?? string.Empty
                    };

                    // Análise de Padrões para colunas de texto
                    if (!string.IsNullOrEmpty(column.DataType) && IsTextColumn(column.DataType))
                    {
                        try
                        {
                            _logger.LogInformation("Analisando padrões para coluna de texto {Column}", column.ColumnName);
                            if (!string.IsNullOrEmpty(column.ColumnName))
                            {
                                columnMetrics.PatternMatches = await AnalyzeColumnPatterns(connection, schemaName, tableName, column.ColumnName);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Erro na análise de padrões para coluna {Column}", column.ColumnName);
                            columnMetrics.PatternMatches = new List<PatternAnalysisResult>();
                        }
                    }

                    // Análise de Outliers para colunas numéricas (simplificada)
                    if (!string.IsNullOrEmpty(column.DataType) && IsNumericColumn(column.DataType))
                    {
                        try
                        {
                            _logger.LogInformation("Analisando outliers para coluna numérica {Column}", column.ColumnName);
                            if (!string.IsNullOrEmpty(column.ColumnName))
                            {
                                columnMetrics.OutlierAnalysis = await AnalyzeColumnOutliers(connection, schemaName, tableName, column.ColumnName, columns);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Erro na análise de outliers para coluna {Column}", column.ColumnName);
                            columnMetrics.OutlierAnalysis = null;
                        }
                    }

                    results.Add(columnMetrics);
                }
            }
            else
            {
                _logger.LogWarning("Nenhuma coluna encontrada na tabela {Schema}.{Table}", schemaName, tableName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao analisar padrões da tabela {Schema}.{Table}", schemaName, tableName);
            throw;
        }

        return results;
    }

    public async Task<RelationshipMetrics> AnalyzeTableRelationships(string connectionString, string schemaName, string tableName)
    {
        var relationshipMetrics = new RelationshipMetrics();

        try
        {
            using var connection = CreateConnection(connectionString);
            await connection.OpenAsync();

            var columns = await GetTableColumns(connection, schemaName, tableName);

            // Análise de Relações Status-Data (simplificada)
            try
            {
                relationshipMetrics.StatusDateRelationships = await AnalyzeStatusDateRelationships(connection, schemaName, tableName, columns);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro na análise de relações status-data");
                relationshipMetrics.StatusDateRelationships = new List<StatusDateRelationship>();
            }

            // Análise de Correlações Numéricas (simplificada)
            try
            {
                relationshipMetrics.NumericCorrelations = await AnalyzeNumericCorrelations(connection, schemaName, tableName, columns);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro na análise de correlações");
                relationshipMetrics.NumericCorrelations = new List<NumericCorrelation>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao analisar relacionamentos da tabela {Schema}.{Table}", schemaName, tableName);
            throw;
        }

        return relationshipMetrics;
    }

    public async Task<OutlierAnalysis?> AnalyzeColumnOutliers(string connectionString, string schemaName, string tableName, string columnName, int page, int pageSize)
    {
        try
        {
            _logger.LogInformation("🔢 Analisando outliers paginados para coluna {Column}, página {Page}", columnName, page);

            using var connection = CreateConnection(connectionString);
            if (connection == null)
            {
                _logger.LogError("Falha ao criar conexão");
                return null;
            }

            await connection.OpenAsync();

            // Obter metadados das colunas
            var columns = await GetTableColumns(connection, schemaName, tableName);

            // Query para calcular estatísticas reais da coluna numérica
            var statsQuery = $@"
                SELECT
                    COUNT(*) as total_count,
                    AVG(CAST(""{columnName}"" AS FLOAT)) as mean_value,
                    STDDEV(CAST(""{columnName}"" AS FLOAT)) as std_dev,
                    MIN(CAST(""{columnName}"" AS FLOAT)) as min_value,
                    MAX(CAST(""{columnName}"" AS FLOAT)) as max_value
                FROM ""{schemaName}"".""{tableName}""
                WHERE ""{columnName}"" IS NOT NULL";

            var stats = await connection.QueryFirstOrDefaultAsync<dynamic>(statsQuery);
            if (stats == null)
            {
                _logger.LogWarning("⚠️ Nenhuma estatística retornada para {Column}", columnName);
                return null;
            }

            var totalCount = Convert.ToInt32(stats.total_count);
            var mean = Convert.ToDouble(stats.mean_value ?? 0);
            var stdDev = Convert.ToDouble(stats.std_dev ?? 0);

            // Calcular limites usando regra dos 3 sigmas
            var lowerBound = mean - (3 * stdDev);
            var upperBound = mean + (3 * stdDev);

            // Query para contar o total de outliers
            var outlierCountQuery = $@"
                SELECT COUNT(*)
                FROM ""{schemaName}"".""{tableName}""
                WHERE ""{columnName}"" IS NOT NULL
                  AND (CAST(""{columnName}"" AS FLOAT) < {lowerBound} OR CAST(""{columnName}"" AS FLOAT) > {upperBound})";

            var totalOutlierCount = await connection.QuerySingleAsync<int>(outlierCountQuery);
            var totalPages = (int)Math.Ceiling((double)totalOutlierCount / pageSize);

            // Query para buscar outliers paginados
            var offset = page * pageSize;
            var columnsList = string.Join(", ", columns.Select(c => $"\"{c.ColumnName}\""));
            var outliersQuery = $@"
                SELECT {columnsList}
                FROM ""{schemaName}"".""{tableName}""
                WHERE ""{columnName}"" IS NOT NULL
                  AND (CAST(""{columnName}"" AS FLOAT) < {lowerBound} OR CAST(""{columnName}"" AS FLOAT) > {upperBound})
                ORDER BY ABS(CAST(""{columnName}"" AS FLOAT) - {mean}) DESC
                LIMIT {pageSize} OFFSET {offset}";

            var outlierRows = await connection.QueryAsync(outliersQuery);
            var outlierRowsData = new List<OutlierRowData>();

            foreach (var row in outlierRows)
            {
                var rowDict = (IDictionary<string, object>)row;
                var outlierValue = rowDict[columnName];

                var rowData = new Dictionary<string, object>();
                foreach (var kvp in rowDict)
                {
                    rowData[kvp.Key] = kvp.Value ?? new object();
                }

                outlierRowsData.Add(new OutlierRowData
                {
                    OutlierValue = outlierValue ?? new object(),
                    OutlierColumn = columnName,
                    RowData = rowData
                });
            }

            _logger.LogInformation("✅ Outliers paginados encontrados: {Count} da página {Page}/{TotalPages}",
                outlierRowsData.Count, page + 1, totalPages);

            return new OutlierAnalysis
            {
                OutlierRows = outlierRowsData,
                OutlierCount = totalOutlierCount,
                CurrentPage = page,
                PageSize = pageSize,
                TotalValues = totalCount,
                OutlierPercentage = totalCount > 0 ? (totalOutlierCount / (double)totalCount) * 100 : 0,
                Mean = mean,
                StandardDeviation = stdDev,
                LowerBound = lowerBound,
                UpperBound = upperBound,
                SampleOutliers = outlierRowsData.Take(10).Select(r =>
                    r.OutlierValue is double d ? d :
                    double.TryParse(r.OutlierValue?.ToString(), out var parsed) ? parsed : 0.0).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao analisar outliers paginados da coluna {Column}: {Error}", columnName, ex.Message);
            return null;
        }
    }

    private async Task<List<PatternAnalysisResult>> AnalyzeColumnPatterns(DbConnection connection, string schemaName, string tableName, string columnName)
    {
        var results = new List<PatternAnalysisResult>();

        // Para análise inteligente, vamos testar TODOS os padrões nos dados
        var applicableRules = _patternRules;

        if (!applicableRules.Any())
            return results;

        // Obter amostra da coluna (máximo 10.000 registros para performance)
        var sampleData = await GetColumnSample(connection, schemaName, tableName, columnName, 10000);

        if (!sampleData.Any())
            return results;

        foreach (var rule in applicableRules)
        {
            try
            {
                var regex = new Regex(rule.Regex, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                var matchingValues = new List<string>();
                var nonMatchingValues = new List<string>();
                int matchCount = 0;

                foreach (var value in sampleData)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    bool isMatch = regex.IsMatch(value);
                    if (isMatch)
                    {
                        matchCount++;
                        if (matchingValues.Count < 5) // Manter apenas 5 exemplos
                            matchingValues.Add(value);
                    }
                    else
                    {
                        if (nonMatchingValues.Count < 5) // Manter apenas 5 exemplos
                            nonMatchingValues.Add(value);
                    }
                }

                var conformityPercentage = sampleData.Count > 0 ? (double)matchCount / sampleData.Count * 100 : 0;

                results.Add(new PatternAnalysisResult
                {
                    PatternName = rule.PatternName,
                    Description = rule.Description,
                    Culture = rule.Culture,
                    ConformityPercentage = Math.Round(conformityPercentage, 2),
                    TotalSamples = sampleData.Count,
                    MatchingSamples = matchCount,
                    SampleMatchingValues = matchingValues,
                    SampleNonMatchingValues = nonMatchingValues
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao aplicar regra {PatternName} na coluna {Column}", rule.PatternName, columnName);
            }
        }

        return results.Where(r => r.ConformityPercentage > 0).OrderByDescending(r => r.ConformityPercentage).ToList();
    }

    private async Task<OutlierAnalysis?> AnalyzeColumnOutliers(DbConnection connection, string schemaName, string tableName, string columnName, List<(string ColumnName, string DataType)>? allColumns = null, int page = 0, int pageSize = 20)
    {
        try
        {
            _logger.LogInformation("🔢 Analisando outliers REAIS para coluna {Column}", columnName);

            // Query para calcular estatísticas reais da coluna numérica
            var statsQuery = $@"
                SELECT
                    COUNT(*) as total_count,
                    AVG(CAST(""{columnName}"" AS FLOAT)) as mean_value,
                    STDDEV(CAST(""{columnName}"" AS FLOAT)) as std_dev,
                    MIN(CAST(""{columnName}"" AS FLOAT)) as min_value,
                    MAX(CAST(""{columnName}"" AS FLOAT)) as max_value
                FROM ""{schemaName}"".""{tableName}""
                WHERE ""{columnName}"" IS NOT NULL";

            _logger.LogInformation("📊 Executando query de estatísticas: {Query}", statsQuery);

            var stats = await connection.QueryFirstOrDefaultAsync<dynamic>(statsQuery);

            if (stats == null)
            {
                _logger.LogWarning("⚠️ Nenhuma estatística retornada para {Column}", columnName);
                return null;
            }

            var totalCount = Convert.ToInt32(stats.total_count);
            var mean = Convert.ToDouble(stats.mean_value ?? 0);
            var stdDev = Convert.ToDouble(stats.std_dev ?? 0);
            var minValue = Convert.ToDouble(stats.min_value ?? 0);
            var maxValue = Convert.ToDouble(stats.max_value ?? 0);

            _logger.LogInformation("📈 Estatísticas calculadas - Total: {Total}, Média: {Mean}, Desvio: {StdDev}, Min: {Min}, Max: {Max}", (object)totalCount, (object)mean, (object)stdDev, (object)minValue, (object)maxValue);

            // Calcular limites usando regra dos 3 sigmas
            var lowerBound = mean - (3 * stdDev);
            var upperBound = mean + (3 * stdDev);

            _logger.LogInformation("📏 Limites 3σ calculados - Inferior: {Lower}, Superior: {Upper}", (object)lowerBound, (object)upperBound);

            // Query para encontrar outliers ordenados por distância da média (mais extremos primeiro)
            var offset = page * pageSize;
            var outliersQuery = $@"
                SELECT CAST(""{columnName}"" AS FLOAT) as value
                FROM ""{schemaName}"".""{tableName}""
                WHERE ""{columnName}"" IS NOT NULL
                  AND (CAST(""{columnName}"" AS FLOAT) < {lowerBound} OR CAST(""{columnName}"" AS FLOAT) > {upperBound})
                ORDER BY ABS(CAST(""{columnName}"" AS FLOAT) - {mean}) DESC
                LIMIT {pageSize} OFFSET {offset}";

            _logger.LogInformation("🔍 Buscando outliers com query: {Query}", outliersQuery);

            var outlierValues = await connection.QueryAsync<double>(outliersQuery);
            var outliersList = outlierValues.ToList();

            // Query para contar o total real de outliers (sem limit)
            var outlierCountQuery = $@"
                SELECT COUNT(*)
                FROM ""{schemaName}"".""{tableName}""
                WHERE ""{columnName}"" IS NOT NULL
                  AND (CAST(""{columnName}"" AS FLOAT) < {lowerBound} OR CAST(""{columnName}"" AS FLOAT) > {upperBound})";

            var totalOutlierCount = await connection.QuerySingleAsync<int>(outlierCountQuery);

            // Query para buscar dados completos das linhas com outliers
            var outlierRowsData = new List<OutlierRowData>();
            if (allColumns != null && allColumns.Any())
            {
                var columnsList = string.Join(", ", allColumns.Select(c => $"\"{c.ColumnName}\""));
                var fullRowQuery = $@"
                    SELECT {columnsList}
                    FROM ""{schemaName}"".""{tableName}""
                    WHERE ""{columnName}"" IS NOT NULL
                      AND (CAST(""{columnName}"" AS FLOAT) < {lowerBound} OR CAST(""{columnName}"" AS FLOAT) > {upperBound})
                    ORDER BY ABS(CAST(""{columnName}"" AS FLOAT) - {mean}) DESC
                    LIMIT {pageSize} OFFSET {offset}";

                _logger.LogInformation("🗂️ Buscando dados completos das linhas com outliers: {Query}", fullRowQuery);

                var fullRowsData = await connection.QueryAsync(fullRowQuery);
                foreach (var row in fullRowsData)
                {
                    var rowDict = (IDictionary<string, object>)row;
                    var outlierValue = Convert.ToDouble(rowDict[columnName]);

                    var rowData = new Dictionary<string, object?>();
                    foreach (var kvp in rowDict)
                    {
                        rowData[kvp.Key] = kvp.Value;
                    }

                    outlierRowsData.Add(new OutlierRowData
                    {
                        OutlierValue = outlierValue,
                        OutlierColumn = columnName,
                        RowData = rowData
                    });
                }
            }
            var outlierPercentage = totalCount > 0 ? (totalOutlierCount / (double)totalCount) * 100 : 0;

            _logger.LogInformation("✅ Análise concluída - Outliers encontrados: {Count} ({Percentage:F2}%)", totalOutlierCount, outlierPercentage);

            return new OutlierAnalysis
            {
                TotalValues = totalCount,
                OutlierCount = totalOutlierCount,
                OutlierPercentage = outlierPercentage,
                Mean = mean,
                StandardDeviation = stdDev,
                LowerBound = lowerBound,
                UpperBound = upperBound,
                SampleOutliers = outliersList,
                OutlierRows = outlierRowsData,
                CurrentPage = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "❌ Erro ao analisar outliers REAIS da coluna {Column}: {Error}", columnName, ex.Message);
            return null;
        }
    }

    private async Task<List<StatusDateRelationship>> AnalyzeStatusDateRelationships(DbConnection connection, string schemaName, string tableName, List<(string ColumnName, string DataType)> columns)
    {
        var relationships = new List<StatusDateRelationship>();

        try
        {
            _logger.LogInformation("🔗 Analisando relações status-data REAIS");

            var statusColumns = columns.Where(c => IsStatusColumn(c.ColumnName)).ToList();
            var dateColumns = columns.Where(c => IsDateColumn(c.ColumnName, c.DataType)).ToList();

            _logger.LogInformation("📋 Encontradas {StatusCols} colunas de status e {DateCols} colunas de data", statusColumns.Count, dateColumns.Count);

            foreach (var statusCol in statusColumns)
            {
                foreach (var dateCol in dateColumns)
                {
                    try
                    {
                        _logger.LogInformation("🔍 Analisando relação {Status} ↔ {Date}", statusCol.ColumnName, dateCol.ColumnName);

                        // Query para boolean PostgreSQL (só valores boolean válidos)
                        var analysisQuery = $@"
                            SELECT
                                COUNT(*) as total_records,
                                COUNT(CASE WHEN ""{statusCol.ColumnName}"" = true AND ""{dateCol.ColumnName}"" IS NULL THEN 1 END) as inconsistent_records,
                                COUNT(CASE WHEN ""{statusCol.ColumnName}"" = true THEN 1 END) as active_records
                            FROM ""{schemaName}"".""{tableName}""";

                        _logger.LogInformation("📊 Executando análise: {Query}", analysisQuery);

                        var stats = await connection.QueryFirstOrDefaultAsync<dynamic>(analysisQuery);

                        if (stats != null)
                        {
                            var totalRecords = Convert.ToInt32(stats.total_records);
                            var inconsistentRecords = Convert.ToInt32(stats.inconsistent_records);
                            var activeRecords = Convert.ToInt32(stats.active_records);

                            if (activeRecords > 0 && inconsistentRecords > 0)
                            {
                                var inconsistencyPercentage = (inconsistentRecords / (double)activeRecords) * 100;

                                // Query para obter valores únicos da coluna de status
                                var valuesQuery = $@"
                                    SELECT DISTINCT ""{statusCol.ColumnName}"" as status_value
                                    FROM ""{schemaName}"".""{tableName}""
                                    WHERE ""{statusCol.ColumnName}"" IS NOT NULL
                                    LIMIT 10";

                                var statusValues = await connection.QueryAsync<string>(valuesQuery);
                                var activeValues = statusValues.Where(v => !string.IsNullOrEmpty(v)).ToList();

                                var relationship = new StatusDateRelationship
                                {
                                    StatusColumn = statusCol.ColumnName,
                                    DateColumn = dateCol.ColumnName,
                                    CommonRadical = ExtractCommonRadical(statusCol.ColumnName, dateCol.ColumnName),
                                    InconsistencyPercentage = Math.Round(inconsistencyPercentage, 2),
                                    TotalActiveRecords = activeRecords,
                                    InconsistentRecords = inconsistentRecords,
                                    ActiveValues = activeValues,
                                    SqlQuery = $@"SELECT COUNT(*) FROM ""{schemaName}"".""{tableName}"" WHERE ""{statusCol.ColumnName}"" IN ('true', 'ativo') AND ""{dateCol.ColumnName}"" IS NULL"
                                };

                                relationships.Add(relationship);

                                _logger.LogInformation("✅ Relação encontrada: {Status} ↔ {Date} - {Inconsistent}/{Active} inconsistentes ({Percentage:F2}%)",
                                    (object)statusCol.ColumnName, (object)dateCol.ColumnName, (object)inconsistentRecords, (object)activeRecords, (object)inconsistencyPercentage);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Erro analisando relação {Status} ↔ {Date}: {Error}", statusCol.ColumnName, dateCol.ColumnName, ex.Message);
                    }
                }
            }

            _logger.LogInformation("🎯 Análise de relações concluída: {Count} relações encontradas", relationships.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro na análise de relações status-data: {Error}", ex.Message);
        }

        return relationships;
    }

    private string ExtractCommonRadical(string statusColumn, string dateColumn)
    {
        // Extrair radical comum simples
        var statusLower = statusColumn.ToLower();
        var dateLower = dateColumn.ToLower();

        if (statusLower.Contains("ativ") || dateLower.Contains("ativ")) return "ativ";
        if (statusLower.Contains("cria") || dateLower.Contains("cria")) return "cria";
        if (statusLower.Contains("migr") || dateLower.Contains("migr")) return "migr";

        return "comum";
    }

    private async Task<List<NumericCorrelation>> AnalyzeNumericCorrelations(DbConnection connection, string schemaName, string tableName, List<(string ColumnName, string DataType)> columns)
    {
        var correlations = new List<NumericCorrelation>();

        try
        {
            _logger.LogInformation("📊 Analisando correlações numéricas REAIS");

            var numericColumns = columns.Where(c => IsNumericColumn(c.DataType)).ToList();

            var numericColumnNames = string.Join(", ", numericColumns.Select(c => $"{c.ColumnName} ({c.DataType})"));
            _logger.LogInformation("🔢 Encontradas {NumCols} colunas numéricas: {Columns}", numericColumns.Count, numericColumnNames);

            if (numericColumns.Count < 2)
            {
                _logger.LogInformation("⚠️ Menos de 2 colunas numéricas encontradas, pulando análise de correlação. Precisa de pelo menos 2 para calcular correlação.");
                return correlations;
            }

            // Analisar correlações entre pares de colunas numéricas
            for (int i = 0; i < numericColumns.Count; i++)
            {
                for (int j = i + 1; j < numericColumns.Count; j++)
                {
                    var col1 = numericColumns[i].ColumnName;
                    var col2 = numericColumns[j].ColumnName;

                    try
                    {
                        _logger.LogInformation("🔍 Calculando correlação entre {Col1} e {Col2}", col1, col2);

                        // Query para calcular correlação de Pearson (PostgreSQL-compatible)
                        var correlationQuery = $@"
                            WITH data AS (
                                SELECT
                                    CAST(""{col1}"" AS FLOAT) as x,
                                    CAST(""{col2}"" AS FLOAT) as y
                                FROM ""{schemaName}"".""{tableName}""
                                WHERE ""{col1}"" IS NOT NULL AND ""{col2}"" IS NOT NULL
                                LIMIT 1000
                            ),
                            means AS (
                                SELECT
                                    COUNT(*) as n,
                                    AVG(x) as mean_x,
                                    AVG(y) as mean_y
                                FROM data
                            ),
                            deviations AS (
                                SELECT
                                    data.x,
                                    data.y,
                                    means.n,
                                    means.mean_x,
                                    means.mean_y,
                                    (data.x - means.mean_x) * (data.y - means.mean_y) as xy_dev,
                                    POWER(data.x - means.mean_x, 2) as x_dev_sq,
                                    POWER(data.y - means.mean_y, 2) as y_dev_sq
                                FROM data, means
                            ),
                            stats AS (
                                SELECT
                                    n,
                                    SUM(xy_dev) as sum_xy,
                                    SUM(x_dev_sq) as sum_x2,
                                    SUM(y_dev_sq) as sum_y2
                                FROM deviations
                                GROUP BY n
                            )
                            SELECT
                                n,
                                CASE
                                    WHEN sum_x2 > 0 AND sum_y2 > 0 THEN
                                        sum_xy / SQRT(sum_x2 * sum_y2)
                                    ELSE 0
                                END as correlation
                            FROM stats";

                        _logger.LogInformation("📈 Executando cálculo de correlação: {Query}", correlationQuery.Substring(0, Math.Min(100, correlationQuery.Length)) + "...");

                        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(correlationQuery);

                        if (result != null)
                        {
                            var sampleSize = Convert.ToInt32(result.n);
                            var correlation = Convert.ToDouble(result.correlation ?? 0);

                            // Incluir correlações fracas também (|r| > 0.1) para análise mais completa
                            if (Math.Abs(correlation) > 0.1 && sampleSize >= 3)
                            {
                                var strength = GetCorrelationStrength(correlation);

                                correlations.Add(new NumericCorrelation
                                {
                                    Column1 = col1,
                                    Column2 = col2,
                                    CorrelationCoefficient = Math.Round(correlation, 3),
                                    CorrelationStrength = strength,
                                    SampleSize = sampleSize
                                });

                                _logger.LogInformation("✅ Correlação significativa encontrada: {Col1} ↔ {Col2} = {Corr:F3} ({Strength})",
                                    (object)col1, (object)col2, (object)correlation, (object)strength);
                            }
                            else
                            {
                                _logger.LogInformation("📉 Correlação fraca entre {Col1} e {Col2}: {Corr:F3} (amostra: {Sample})",
                                    (object)col1, (object)col2, (object)correlation, (object)sampleSize);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Erro calculando correlação {Col1} ↔ {Col2}: {Error}", col1, col2, ex.Message);
                    }
                }
            }

            _logger.LogInformation("🎯 Análise de correlações concluída: {Count} correlações significativas encontradas", correlations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro na análise de correlações numéricas: {Error}", ex.Message);
        }

        return correlations;
    }

    private string GetCorrelationStrength(double correlation)
    {
        var abs = Math.Abs(correlation);
        var direction = correlation >= 0 ? "Positiva" : "Negativa";

        return abs switch
        {
            >= 0.9 => $"Correlação {direction} Muito Forte",
            >= 0.7 => $"Correlação {direction} Forte",
            >= 0.5 => $"Correlação {direction} Moderada",
            >= 0.3 => $"Correlação {direction} Fraca",
            _ => "Correlação Muito Fraca"
        };
    }

    // Métodos auxiliares
    private DbConnection CreateConnection(string connectionString)
    {
        _logger.LogInformation("Criando conexão para: {ConnectionStringStart}", connectionString.Substring(0, Math.Min(50, connectionString.Length)));

        try
        {
            // Detectar tipo de banco pela connection string
            if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Detectado: PostgreSQL");
                return new NpgsqlConnection(connectionString);
            }
            if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) && connectionString.Contains("Uid=", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Detectado: MySQL");
                return new MySqlConnection(connectionString);
            }
            if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) && connectionString.ToLower().Contains(".db"))
            {
                _logger.LogInformation("Detectado: SQLite");
                return new SqliteConnection(connectionString);
            }
            if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) || connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Detectado: SQL Server");
                return new SqlConnection(connectionString);
            }

            // Default para SQL Server
            _logger.LogWarning("Tipo de banco não detectado, usando SQL Server como padrão");
            return new SqlConnection(connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar conexão");
            throw;
        }
    }

    private List<PatternValidationRule> GetApplicableRules(string columnName)
    {
        var normalizedColumnName = NormalizeString(columnName);
        var applicableRules = new List<PatternValidationRule>();

        foreach (var rule in _patternRules)
        {
            foreach (var keyword in rule.ColumnKeywords)
            {
                if (normalizedColumnName.Contains(NormalizeString(keyword)))
                {
                    applicableRules.Add(rule);
                    break;
                }
            }
        }

        return applicableRules;
    }

    private string NormalizeString(string input)
    {
        return RemoveDiacritics(input.ToLowerInvariant());
    }

    private string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    private async Task<List<string>> GetColumnSample(DbConnection connection, string schemaName, string tableName, string columnName, int maxSample = 10000)
    {
        try
        {
            // SQL específico para PostgreSQL (com aspas duplas)
            var query = $@"
                SELECT ""{columnName}""
                FROM ""{schemaName}"".""{tableName}""
                WHERE ""{columnName}"" IS NOT NULL
                LIMIT {maxSample}";

            var results = await connection.QueryAsync<object>(query);
            return results
                .Select(r => r?.ToString()?.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .Cast<string>()
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao obter amostra da coluna {Column}. Retornando lista vazia.", columnName);
            return new List<string>();
        }
    }

    private async Task<List<(string ColumnName, string DataType)>> GetTableColumns(DbConnection connection, string schemaName, string tableName)
    {
        try
        {
            var query = @"
                SELECT column_name, data_type
                FROM information_schema.columns
                WHERE table_schema = @schemaName AND table_name = @tableName
                ORDER BY ordinal_position";

            _logger.LogInformation("Executando query: {Query} com schema={Schema}, table={Table}", query, schemaName, tableName);

            var results = await connection.QueryAsync<dynamic>(query, new { schemaName, tableName });

            _logger.LogInformation("Query retornou {Count} resultados", results.Count());

            var columns = new List<(string, string)>();
            foreach (var row in results)
            {
                // Tentar acessar tanto em maiúscula quanto minúscula (compatibilidade PostgreSQL/SQL Server)
                string? columnName = null;
                string? dataType = null;

                try
                {
                    columnName = (string)(row.column_name ?? row.COLUMN_NAME);
                    dataType = (string)(row.data_type ?? row.DATA_TYPE);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao acessar propriedades da linha: {Error}", ex.Message);
                    continue;
                }

                if (!string.IsNullOrEmpty(columnName) && !string.IsNullOrEmpty(dataType))
                {
                    _logger.LogInformation("Coluna encontrada: {Column} ({Type})", columnName, dataType);
                    columns.Add((columnName, dataType));
                }
            }

            return columns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter colunas da tabela {Schema}.{Table}", schemaName, tableName);
            return new List<(string, string)>();
        }
    }

    private bool IsTextColumn(string dataType)
    {
        if (string.IsNullOrEmpty(dataType))
            return false;

        var textTypes = new[] {
            // SQL Server/MySQL
            "varchar", "nvarchar", "char", "nchar", "text", "ntext",
            // PostgreSQL
            "character varying", "character", "text",
            // Genérico
            "string"
        };
        return textTypes.Any(t => dataType.ToLowerInvariant().StartsWith(t));
    }

    private bool IsNumericColumn(string dataType)
    {
        if (string.IsNullOrEmpty(dataType))
            return false;

        var numericTypes = new[] {
            // SQL Server/MySQL
            "int", "bigint", "smallint", "tinyint", "decimal", "numeric", "float", "real", "money", "smallmoney",
            // PostgreSQL
            "integer", "bigint", "smallint", "numeric", "decimal", "real", "double precision", "serial", "bigserial", "smallserial",
            // Genérico
            "number"
        };
        return numericTypes.Any(t => dataType.ToLowerInvariant().StartsWith(t));
    }

    private bool IsDateColumn(string columnName, string dataType)
    {
        if (string.IsNullOrEmpty(dataType) || string.IsNullOrEmpty(columnName))
            return false;

        var dateTypes = new[] { "datetime", "datetime2", "date", "time", "datetimeoffset" };
        var dateKeywords = new[] { "dt_", "data_", "date_", "timestamp_", "_at", "_date", "_time" };

        return dateTypes.Any(t => dataType.ToLowerInvariant().StartsWith(t)) ||
               dateKeywords.Any(k => columnName.ToLowerInvariant().Contains(k));
    }

    private bool IsStatusColumn(string columnName)
    {
        if (string.IsNullOrEmpty(columnName))
            return false;

        var statusKeywords = new[] { "st_", "situacao_", "status_", "fl_", "in_", "is_", "has_", "ativo", "active" };
        var normalizedName = columnName.ToLowerInvariant();

        return statusKeywords.Any(k => normalizedName.Contains(k));
    }


    private string FindCommonRadical(string statusColumn, string dateColumn)
    {
        var statusNormalized = NormalizeString(statusColumn).Replace("st_", "").Replace("fl_", "").Replace("is_", "").Replace("has_", "");
        var dateNormalized = NormalizeString(dateColumn).Replace("dt_", "").Replace("data_", "").Replace("date_", "").Replace("_at", "").Replace("_date", "");

        // Encontrar substring comum mais longa
        var longestCommon = "";
        for (int i = 0; i < statusNormalized.Length; i++)
        {
            for (int j = i + 1; j <= statusNormalized.Length; j++)
            {
                var substring = statusNormalized[i..j];
                if (substring.Length > longestCommon.Length && dateNormalized.Contains(substring))
                {
                    longestCommon = substring;
                }
            }
        }

        return longestCommon;
    }

}