using DbConnect.Core.Models;
using Npgsql;

namespace DbConnect.Web.Services;

public class IntelligentSampler
{
    public async Task<SamplingStrategy> DetermineSamplingStrategyAsync(ConnectionProfile profile, string schema, string tableName)
    {
        var strategy = new SamplingStrategy();
        
        try
        {
            var stats = await GetTableStatisticsAsync(profile, schema, tableName);
            strategy.TableStats = stats;

            // Determinar estratégia baseada no tamanho da tabela
            if (stats.TotalRows <= 1000)
            {
                // Tabelas pequenas: análise completa
                strategy.SamplingType = SamplingType.FullScan;
                strategy.SampleSize = (int)stats.TotalRows;
                strategy.Reason = "Tabela pequena - análise completa";
            }
            else if (stats.TotalRows <= 100000)
            {
                // Tabelas médias: amostragem aleatória stratificada
                strategy.SamplingType = SamplingType.RandomSample;
                strategy.SampleSize = Math.Max(5000, (int)(stats.TotalRows * 0.1)); // 10% ou mín 5k
                strategy.Reason = "Tabela média - amostragem aleatória 10%";
            }
            else if (stats.TotalRows <= 1000000)
            {
                // Tabelas grandes: amostragem sistemática
                strategy.SamplingType = SamplingType.SystematicSample;
                strategy.SampleSize = 10000;
                strategy.Reason = "Tabela grande - amostragem sistemática de 10k registros";
            }
            else
            {
                // Tabelas muito grandes: amostragem adaptativa
                strategy.SamplingType = SamplingType.AdaptiveSample;
                strategy.SampleSize = 15000;
                strategy.Reason = "Tabela muito grande - amostragem adaptativa de 15k registros";
            }

            // Ajustar baseado na complexidade dos dados
            AdjustForDataComplexity(strategy, stats);

            // Ajustar baseado na presença de índices
            await AdjustForIndexesAsync(profile, schema, tableName, strategy);

        }
        catch (Exception ex)
        {
            // Fallback para estratégia segura
            strategy.SamplingType = SamplingType.RandomSample;
            strategy.SampleSize = 1000;
            strategy.Reason = $"Erro ao determinar estratégia, usando fallback: {ex.Message}";
        }

        return strategy;
    }

    private async Task<TableStatistics> GetTableStatisticsAsync(ConnectionProfile profile, string schema, string tableName)
    {
        var stats = new TableStatistics();
        
        if (profile.Kind != DbKind.PostgreSql)
            return stats;

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = profile.HostOrFile,
            Port = profile.Port ?? 5432,
            Database = profile.Database,
            Username = profile.Username,
            Password = profile.Password ?? ""
        };

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync();

        // Obter estatísticas básicas da tabela
        var basicStatsQuery = @"
            SELECT 
                schemaname, tablename, attname, n_distinct, correlation,
                null_frac, avg_width, n_common_vals, n_dups
            FROM pg_stats 
            WHERE schemaname = @schema AND tablename = @table_name
            ORDER BY attname";

        await using var cmd1 = new NpgsqlCommand(basicStatsQuery, conn);
        cmd1.Parameters.AddWithValue("schema", schema);
        cmd1.Parameters.AddWithValue("table_name", tableName);

        var columnStats = new List<ColumnStatistics>();
        await using (var reader1 = await cmd1.ExecuteReaderAsync())
        {
            while (await reader1.ReadAsync())
            {
                columnStats.Add(new ColumnStatistics
                {
                    ColumnName = reader1.GetString(reader1.GetOrdinal("attname")),
                    DistinctValues = reader1.IsDBNull(reader1.GetOrdinal("n_distinct")) ? -1 : Convert.ToInt64(reader1.GetValue(reader1.GetOrdinal("n_distinct"))),
                    NullFraction = reader1.IsDBNull(reader1.GetOrdinal("null_frac")) ? 0 : reader1.GetFloat(reader1.GetOrdinal("null_frac")),
                    AverageWidth = reader1.IsDBNull(reader1.GetOrdinal("avg_width")) ? 0 : Convert.ToInt32(reader1.GetValue(reader1.GetOrdinal("avg_width"))),
                    Correlation = reader1.IsDBNull(reader1.GetOrdinal("correlation")) ? 0 : reader1.GetFloat(reader1.GetOrdinal("correlation"))
                });
            }
        }

        // Obter contagem total de registros e tamanho da tabela
        var sizeQuery = @"
            SELECT 
                c.reltuples::bigint as row_count,
                pg_total_relation_size(c.oid) as total_size_bytes,
                pg_relation_size(c.oid) as table_size_bytes
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = @schema AND c.relname = @table_name";

        await using var cmd2 = new NpgsqlCommand(sizeQuery, conn);
        cmd2.Parameters.AddWithValue("schema", schema);
        cmd2.Parameters.AddWithValue("table_name", tableName);

        await using (var reader2 = await cmd2.ExecuteReaderAsync())
        {
            if (await reader2.ReadAsync())
            {
                stats.TotalRows = Convert.ToInt64(reader2.GetValue(reader2.GetOrdinal("row_count")));
                stats.TotalSizeBytes = Convert.ToInt64(reader2.GetValue(reader2.GetOrdinal("total_size_bytes")));
                stats.TableSizeBytes = Convert.ToInt64(reader2.GetValue(reader2.GetOrdinal("table_size_bytes")));
            }
        }

        stats.ColumnStatistics = columnStats;
        stats.AnalysisTimestamp = DateTime.UtcNow;

        return stats;
    }

    private void AdjustForDataComplexity(SamplingStrategy strategy, TableStatistics stats)
    {
        var complexityFactors = 0;

        // Verificar se há muitas colunas com alta cardinalidade
        var highCardinalityColumns = stats.ColumnStatistics.Count(c => c.DistinctValues > stats.TotalRows * 0.8);
        if (highCardinalityColumns > 3)
        {
            complexityFactors++;
            strategy.Reason += " | Alta cardinalidade";
        }

        // Verificar se há muitas colunas com valores nulos
        var highNullColumns = stats.ColumnStatistics.Count(c => c.NullFraction > 0.5);
        if (highNullColumns > 2)
        {
            complexityFactors++;
            strategy.Reason += " | Muitos NULLs";
        }

        // Verificar se há colunas muito largas (possível texto/JSON)
        var wideColumns = stats.ColumnStatistics.Count(c => c.AverageWidth > 100);
        if (wideColumns > 1)
        {
            complexityFactors++;
            strategy.Reason += " | Colunas largas";
        }

        // Ajustar tamanho da amostra baseado na complexidade
        if (complexityFactors > 1)
        {
            strategy.SampleSize = Math.Min(strategy.SampleSize * 2, 50000);
            strategy.Reason += " - aumentado por complexidade";
        }
    }

    private async Task AdjustForIndexesAsync(ConnectionProfile profile, string schema, string tableName, SamplingStrategy strategy)
    {
        if (profile.Kind != DbKind.PostgreSql)
            return;

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = profile.HostOrFile,
            Port = profile.Port ?? 5432,
            Database = profile.Database,
            Username = profile.Username,
            Password = profile.Password ?? ""
        };

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync();

        // Verificar se há índices que podem ajudar na amostragem
        var indexQuery = @"
            SELECT i.indisprimary, i.indisunique, a.attname
            FROM pg_index i
            JOIN pg_class c ON c.oid = i.indrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = ANY(i.indkey)
            WHERE n.nspname = @schema AND c.relname = @table_name
            ORDER BY i.indisprimary DESC, i.indisunique DESC";

        await using var cmd = new NpgsqlCommand(indexQuery, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table_name", tableName);

        var hasClusteredIndex = false;
        var hasUniqueIndex = false;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetBoolean(reader.GetOrdinal("indisprimary")))
            {
                hasClusteredIndex = true;
                strategy.PrimaryKeyColumn = reader.GetString(reader.GetOrdinal("attname"));
            }
            if (reader.GetBoolean(reader.GetOrdinal("indisunique")))
            {
                hasUniqueIndex = true;
            }
        }

        // Otimizar estratégia baseada em índices disponíveis
        if (hasClusteredIndex && strategy.SamplingType == SamplingType.RandomSample)
        {
            strategy.SamplingType = SamplingType.SystematicSample;
            strategy.Reason += " | Otimizado para PK";
        }
        
        if (hasUniqueIndex && strategy.SampleSize > 20000)
        {
            // Com índices únicos, podemos usar amostras menores mas mais representativas
            strategy.SampleSize = (int)(strategy.SampleSize * 0.8);
            strategy.Reason += " | Reduzido por índices únicos";
        }
    }

    public string GenerateSamplingQuery(string schema, string tableName, SamplingStrategy strategy)
    {
        var baseQuery = $"SELECT * FROM \"{schema}\".\"{tableName}\"";

        return strategy.SamplingType switch
        {
            SamplingType.FullScan => baseQuery,
            
            SamplingType.RandomSample => 
                $"{baseQuery} ORDER BY RANDOM() LIMIT {strategy.SampleSize}",
            
            SamplingType.SystematicSample when !string.IsNullOrEmpty(strategy.PrimaryKeyColumn) =>
                $"{baseQuery} WHERE {strategy.PrimaryKeyColumn} % {GetSystematicInterval(strategy)} = 0 LIMIT {strategy.SampleSize}",
                
            SamplingType.SystematicSample =>
                $"{baseQuery} WHERE ctid IN (SELECT ctid FROM \"{schema}\".\"{tableName}\" TABLESAMPLE SYSTEM(10)) LIMIT {strategy.SampleSize}",
            
            SamplingType.AdaptiveSample =>
                GenerateAdaptiveSamplingQuery(schema, tableName, strategy),
            
            _ => $"{baseQuery} ORDER BY RANDOM() LIMIT {strategy.SampleSize}"
        };
    }

    private int GetSystematicInterval(SamplingStrategy strategy)
    {
        if (strategy.TableStats?.TotalRows == null || strategy.SampleSize <= 0)
            return 100;
            
        return Math.Max(1, (int)(strategy.TableStats.TotalRows / strategy.SampleSize));
    }

    private string GenerateAdaptiveSamplingQuery(string schema, string tableName, SamplingStrategy strategy)
    {
        // Para tabelas muito grandes, usar uma combinação de TABLESAMPLE e RANDOM
        var samplePercent = Math.Min(50.0, (strategy.SampleSize * 100.0) / (strategy.TableStats?.TotalRows ?? strategy.SampleSize));
        
        return $@"
            SELECT * FROM (
                SELECT *, ROW_NUMBER() OVER (ORDER BY RANDOM()) as rn
                FROM ""{schema}"".""{tableName}"" TABLESAMPLE SYSTEM({samplePercent:F2})
            ) sample
            WHERE rn <= {strategy.SampleSize}";
    }
}

public class SamplingStrategy
{
    public SamplingType SamplingType { get; set; }
    public int SampleSize { get; set; }
    public string Reason { get; set; } = "";
    public string? PrimaryKeyColumn { get; set; }
    public TableStatistics? TableStats { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TableStatistics
{
    public long TotalRows { get; set; }
    public long TotalSizeBytes { get; set; }
    public long TableSizeBytes { get; set; }
    public List<ColumnStatistics> ColumnStatistics { get; set; } = new();
    public DateTime AnalysisTimestamp { get; set; }
    
    public double SizeMB => TotalSizeBytes / 1024.0 / 1024.0;
    public int AverageRowWidth => (int)(TotalRows > 0 ? TableSizeBytes / TotalRows : 0);
}

public class ColumnStatistics
{
    public string ColumnName { get; set; } = "";
    public long DistinctValues { get; set; }
    public float NullFraction { get; set; }
    public int AverageWidth { get; set; }
    public float Correlation { get; set; }
}

public enum SamplingType
{
    FullScan,        // Análise completa (tabelas pequenas)
    RandomSample,    // Amostra aleatória simples
    SystematicSample, // Amostra sistemática (cada N registro)
    AdaptiveSample   // Amostra adaptativa (combina técnicas)
}