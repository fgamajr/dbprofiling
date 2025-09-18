using DbConnect.Web.AI.Enhanced;
using DbConnect.Web.Models.SchemaDiscovery;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DbConnect.Web.Services.Enhanced;

/// <summary>
/// MCP Translation Service
/// Traduz validações de linguagem natural para SQL executável
/// Baseado nas capacidades do pg-mcp-server
/// </summary>
public interface IMCPTranslationService
{
    Task<string> TranslateValidationToSQLAsync(ContextualValidation validation, EnhancedDatabaseSchema schema, string? apiKey);
    Task<List<TranslatedValidation>> TranslateMultipleValidationsAsync(List<ContextualValidation> validations, EnhancedDatabaseSchema schema, string? apiKey);
    Task<string> OptimizeSQLForPostgreSQLAsync(string sql, EnhancedDatabaseSchema schema);
    Task<bool> ValidateSQLSafetyAsync(string sql);
}

public class MCPTranslationService : IMCPTranslationService
{
    private readonly ILogger<MCPTranslationService> _logger;
    private readonly HttpClient _httpClient;

    public MCPTranslationService(ILogger<MCPTranslationService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Traduzir validação individual para SQL
    /// </summary>
    public async Task<string> TranslateValidationToSQLAsync(
        ContextualValidation validation,
        EnhancedDatabaseSchema schema,
        string? apiKey)
    {
        _logger.LogInformation("🔄 Traduzindo validação: {Description}", validation.Description);

        try
        {
            // 1. Tentar tradução template-based (rápida)
            var templateSQL = TryTemplateBasedTranslation(validation, schema);
            if (!string.IsNullOrEmpty(templateSQL))
            {
                _logger.LogInformation("⚡ Tradução via template realizada");
                validation.TranslationMethod = "template";
                return templateSQL;
            }

            // 2. Fallback: tradução via IA (mais lenta mas mais flexível)
            if (!string.IsNullOrEmpty(apiKey))
            {
                var aiSQL = await TranslateViaAIAsync(validation, schema, apiKey);
                if (!string.IsNullOrEmpty(aiSQL))
                {
                    _logger.LogInformation("🤖 Tradução via IA realizada");
                    validation.TranslationMethod = "ai";
                    return aiSQL;
                }
            }

            // 3. Última opção: SQL genérico
            _logger.LogWarning("⚠️ Usando tradução genérica para: {Description}", validation.Description);
            validation.TranslationMethod = "generic";
            return GenerateGenericSQL(validation, schema);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao traduzir validação: {Description}", validation.Description);
            throw;
        }
    }

    /// <summary>
    /// Traduzir múltiplas validações em paralelo
    /// </summary>
    public async Task<List<TranslatedValidation>> TranslateMultipleValidationsAsync(
        List<ContextualValidation> validations,
        EnhancedDatabaseSchema schema,
        string? apiKey)
    {
        _logger.LogInformation("🔄 Traduzindo {Count} validações para SQL", validations.Count);

        var translationTasks = validations.Select(async validation =>
        {
            try
            {
                var sql = await TranslateValidationToSQLAsync(validation, schema, apiKey);
                var optimizedSQL = await OptimizeSQLForPostgreSQLAsync(sql, schema);
                var isValid = await ValidateSQLSafetyAsync(optimizedSQL);

                return new TranslatedValidation
                {
                    OriginalValidation = validation,
                    TranslatedSQL = optimizedSQL,
                    IsValidSQL = isValid,
                    TranslationMethod = validation.TranslationMethod ?? "unknown",
                    TranslatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao traduzir validação {Number}: {Description}",
                    validation.Number, validation.Description);

                return new TranslatedValidation
                {
                    OriginalValidation = validation,
                    TranslatedSQL = $"-- ERROR: Failed to translate validation\n-- {ex.Message}",
                    IsValidSQL = false,
                    TranslationMethod = "error",
                    TranslatedAt = DateTime.UtcNow
                };
            }
        });

        var results = await Task.WhenAll(translationTasks);

        var successCount = results.Count(r => r.IsValidSQL);
        _logger.LogInformation("✅ Traduzidas {Success}/{Total} validações com sucesso", successCount, validations.Count);

        return results.ToList();
    }

    /// <summary>
    /// Otimizar SQL para PostgreSQL
    /// Baseado nas práticas do pg-mcp-server
    /// </summary>
    public Task<string> OptimizeSQLForPostgreSQLAsync(string sql, EnhancedDatabaseSchema schema)
    {
        var optimizedSQL = sql;

        // 1. Adicionar quote nos identificadores se necessário
        optimizedSQL = AddPostgreSQLIdentifierQuotes(optimizedSQL, schema);

        // 2. Usar funções específicas do PostgreSQL
        optimizedSQL = UsePostgreSQLFunctions(optimizedSQL);

        // 3. Adicionar limites para performance
        if (!optimizedSQL.ToLower().Contains("limit"))
        {
            optimizedSQL += " LIMIT 10000"; // Evitar scans massivos
        }

        // 4. Adicionar comentário explicativo
        var comment = $"-- Generated by Enhanced MCP Translation Service\n-- Optimization: PostgreSQL-specific functions and safety limits\n";
        optimizedSQL = comment + optimizedSQL;

        return Task.FromResult(optimizedSQL);
    }

    /// <summary>
    /// Validar segurança do SQL (baseado no pg-mcp-server)
    /// </summary>
    public Task<bool> ValidateSQLSafetyAsync(string sql)
    {
        var lowerSQL = sql.ToLower();

        // 1. Operações proibidas (pg-mcp-server style)
        var forbiddenOperations = new[]
        {
            "insert", "update", "delete", "truncate", "drop",
            "alter", "create", "grant", "revoke", "import",
            "copy", "merge", "upsert", "replace"
        };

        foreach (var operation in forbiddenOperations)
        {
            if (Regex.IsMatch(lowerSQL, $@"\b{operation}\b"))
            {
                _logger.LogWarning("⚠️ SQL contém operação proibida: {Operation}", operation);
                return Task.FromResult(false);
            }
        }

        // 2. Padrões de SQL injection
        var injectionPatterns = new[]
        {
            @";\s*drop\s+",
            @";\s*delete\s+from\s+",
            @"union\s+all\s+select\s+null",
            @"\/\*.*\*\/\s*drop",
            @"--.*drop\s+",
            @"xp_cmdshell",
            @"exec\s*\("
        };

        foreach (var pattern in injectionPatterns)
        {
            if (Regex.IsMatch(lowerSQL, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogWarning("⚠️ SQL contém padrão suspeito: {Pattern}", pattern);
                return Task.FromResult(false);
            }
        }

        // 3. Verificar se é SELECT válido
        if (!Regex.IsMatch(lowerSQL.Trim(), @"^\s*(with\s+|select\s+|explain\s+|show\s+)", RegexOptions.IgnoreCase))
        {
            _logger.LogWarning("⚠️ SQL não é uma consulta SELECT válida");
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Tentar tradução baseada em templates (rápida)
    /// </summary>
    private string TryTemplateBasedTranslation(ContextualValidation validation, EnhancedDatabaseSchema schema)
    {
        var description = validation.Description.ToLower();

        // Template para integridade referencial
        if (description.Contains("órfão") || description.Contains("referencial"))
        {
            return GenerateReferentialIntegritySQL(validation, schema);
        }

        // Template para consistência temporal
        if (description.Contains("data") && (description.Contains("anterior") || description.Contains("posterior")))
        {
            return GenerateTemporalConsistencySQL(validation, schema);
        }

        // Template para consistência de status
        if (description.Contains("status") && description.Contains("ativo"))
        {
            return GenerateStatusConsistencySQL(validation, schema);
        }

        // Template para duplicatas
        if (description.Contains("duplicat") || description.Contains("único"))
        {
            return GenerateDuplicateDetectionSQL(validation, schema);
        }

        return string.Empty; // Não conseguiu mapear para template
    }

    /// <summary>
    /// Traduzir via IA (OpenAI)
    /// </summary>
    private async Task<string> TranslateViaAIAsync(
        ContextualValidation validation,
        EnhancedDatabaseSchema schema,
        string apiKey)
    {
        var prompt = BuildSQLTranslationPrompt(validation, schema);

        var requestPayload = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = @"Você é um especialista em PostgreSQL que traduz validações de qualidade de dados para SQL executável.
IMPORTANTE:
- Retorne APENAS o código SQL, sem explicações
- Use sintaxe PostgreSQL específica
- Sempre inclua comentários explicativos no SQL
- Garanta que o SQL é seguro (apenas SELECT/WITH)
- Use JOINs quando necessário para validações cruzadas"
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            temperature = 0.1, // Baixíssima criatividade para consistência
            max_tokens = 1000
        };

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var jsonPayload = JsonSerializer.Serialize(requestPayload);
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("❌ Erro na API OpenAI: {StatusCode}", response.StatusCode);
            return string.Empty;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

        var sqlResponse = responseObj
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        // Extrair apenas o SQL da resposta
        return ExtractSQLFromResponse(sqlResponse);
    }

    /// <summary>
    /// Gerar SQL de integridade referencial
    /// </summary>
    private string GenerateReferentialIntegritySQL(ContextualValidation validation, EnhancedDatabaseSchema schema)
    {
        var relationships = validation.InvolvedRelationships;
        if (!relationships.Any()) return string.Empty;

        var rel = relationships.First();

        return $@"
-- Verificação de Integridade Referencial: {validation.Description}
SELECT
    COUNT(*) as total_records,
    COUNT(target.id) as valid_references,
    COUNT(*) - COUNT(target.id) as orphaned_records,
    ROUND((COUNT(target.id)::numeric / COUNT(*)) * 100, 2) as integrity_percentage
FROM {QuoteIdentifier(validation.InvolvedTables.First())} source
LEFT JOIN {QuoteIdentifier(rel.TableName)} target ON {rel.JoinCondition}
WHERE source.id IS NOT NULL;";
    }

    /// <summary>
    /// Gerar SQL de consistência temporal
    /// </summary>
    private string GenerateTemporalConsistencySQL(ContextualValidation validation, EnhancedDatabaseSchema schema)
    {
        var tables = validation.InvolvedTables;
        if (tables.Count < 2) return string.Empty;

        return $@"
-- Verificação de Consistência Temporal: {validation.Description}
SELECT
    COUNT(*) as total_records,
    SUM(CASE WHEN table1.created_at <= table2.created_at THEN 1 ELSE 0 END) as valid_temporal_order,
    SUM(CASE WHEN table1.created_at > table2.created_at THEN 1 ELSE 0 END) as invalid_temporal_order,
    ROUND((SUM(CASE WHEN table1.created_at <= table2.created_at THEN 1 ELSE 0 END)::numeric / COUNT(*)) * 100, 2) as temporal_consistency_percentage
FROM {QuoteIdentifier(tables[0])} table1
JOIN {QuoteIdentifier(tables[1])} table2 ON table1.id = table2.{tables[0]}_id
WHERE table1.created_at IS NOT NULL AND table2.created_at IS NOT NULL;";
    }

    /// <summary>
    /// Gerar SQL de consistência de status
    /// </summary>
    private string GenerateStatusConsistencySQL(ContextualValidation validation, EnhancedDatabaseSchema schema)
    {
        var tables = validation.InvolvedTables;
        if (tables.Count < 2) return string.Empty;

        return $@"
-- Verificação de Consistência de Status: {validation.Description}
SELECT
    COUNT(*) as total_active_records,
    SUM(CASE WHEN table2.status = 'ATIVO' THEN 1 ELSE 0 END) as consistent_status,
    SUM(CASE WHEN table2.status != 'ATIVO' THEN 1 ELSE 0 END) as inconsistent_status,
    ROUND((SUM(CASE WHEN table2.status = 'ATIVO' THEN 1 ELSE 0 END)::numeric / COUNT(*)) * 100, 2) as status_consistency_percentage
FROM {QuoteIdentifier(tables[0])} table1
JOIN {QuoteIdentifier(tables[1])} table2 ON table1.id = table2.{tables[0]}_id
WHERE table1.status = 'ATIVO';";
    }

    /// <summary>
    /// Gerar SQL de detecção de duplicatas
    /// </summary>
    private string GenerateDuplicateDetectionSQL(ContextualValidation validation, EnhancedDatabaseSchema schema)
    {
        var table = validation.InvolvedTables.FirstOrDefault();
        if (string.IsNullOrEmpty(table)) return string.Empty;

        return $@"
-- Detecção de Duplicatas: {validation.Description}
SELECT
    column_name,
    COUNT(*) as duplicate_count,
    COUNT(*) - COUNT(DISTINCT column_name) as total_duplicates
FROM {QuoteIdentifier(table)}
GROUP BY column_name
HAVING COUNT(*) > 1
ORDER BY duplicate_count DESC;";
    }

    /// <summary>
    /// Gerar SQL genérico
    /// </summary>
    private string GenerateGenericSQL(ContextualValidation validation, EnhancedDatabaseSchema schema)
    {
        var table = validation.InvolvedTables.FirstOrDefault() ?? schema.Tables.First().FullName;

        return $@"
-- Validação Genérica: {validation.Description}
-- ATENÇÃO: SQL genérico - pode necessitar ajustes manuais
SELECT
    COUNT(*) as total_records,
    COUNT(*) as processed_records,
    0 as issues_found,
    'Generic validation - manual review required' as notes
FROM {QuoteIdentifier(table)}
LIMIT 100;";
    }

    /// <summary>
    /// Construir prompt para tradução SQL via IA
    /// </summary>
    private string BuildSQLTranslationPrompt(ContextualValidation validation, EnhancedDatabaseSchema schema)
    {
        var tablesInfo = string.Join("\n", schema.Tables.Take(10).Select(t =>
            $"- {t.FullName}: {string.Join(", ", t.Columns.Take(5).Select(c => $"{c.ColumnName} ({c.DataType})"))}"));

        var relationshipsInfo = string.Join("\n", validation.InvolvedRelationships.Select(r =>
            $"- {r.JoinCondition} [{r.RelationshipType}]"));

        return $@"
CONTEXTO:
Schema PostgreSQL com as seguintes tabelas:
{tablesInfo}

Relacionamentos relevantes:
{relationshipsInfo}

TAREFA:
Traduza esta validação para SQL PostgreSQL executável:
""{validation.Description}""

Tipo de validação: {validation.ValidationType}
Tabelas envolvidas: {string.Join(", ", validation.InvolvedTables)}

REQUISITOS:
1. Use apenas SELECT/WITH (sem modificações)
2. Inclua comentários explicativos
3. Use JOINs quando necessário
4. Retorne métricas úteis (contagens, percentuais)
5. Use funções PostgreSQL específicas quando apropriado

RETORNE APENAS O SQL:";
    }

    /// <summary>
    /// Extrair SQL limpo da resposta da IA
    /// </summary>
    private string ExtractSQLFromResponse(string response)
    {
        // Remover blocos de código markdown
        var sqlPattern = @"```(?:sql)?\s*(.*?)\s*```";
        var match = Regex.Match(response, sqlPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        // Se não tem markdown, assumir que é SQL direto
        return response.Trim();
    }

    /// <summary>
    /// Adicionar quotes PostgreSQL nos identificadores
    /// </summary>
    private string AddPostgreSQLIdentifierQuotes(string sql, EnhancedDatabaseSchema schema)
    {
        foreach (var table in schema.Tables)
        {
            var unquotedName = table.FullName;
            var quotedName = QuoteIdentifier(unquotedName);
            sql = sql.Replace(unquotedName, quotedName);
        }

        return sql;
    }

    /// <summary>
    /// Usar funções específicas do PostgreSQL
    /// </summary>
    private string UsePostgreSQLFunctions(string sql)
    {
        // Substituir funções genéricas por PostgreSQL específicas
        sql = sql.Replace("LEN(", "LENGTH(");
        sql = sql.Replace("ISNULL(", "COALESCE(");
        sql = sql.Replace("GETDATE()", "NOW()");

        return sql;
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
/// Validação traduzida para SQL
/// </summary>
public class TranslatedValidation
{
    public ContextualValidation OriginalValidation { get; set; } = new();
    public string TranslatedSQL { get; set; } = string.Empty;
    public bool IsValidSQL { get; set; }
    public string TranslationMethod { get; set; } = string.Empty; // "template", "ai", "generic", "error"
    public DateTime TranslatedAt { get; set; }
    public string? ValidationErrors { get; set; }
    public TimeSpan? TranslationDuration { get; set; }
}