using DbConnect.Core.Models;
using DbConnect.Web.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using DbConnect.Web.AI;

namespace DbConnect.Web.Services;

public class PreflightService
{
    private readonly AppDbContext _context;
    private readonly DataQualityAI _aiService;
    private readonly ApiKeyService _apiKeyService;
    private readonly ILogger<PreflightService> _logger;

    public PreflightService(AppDbContext context, DataQualityAI aiService, ApiKeyService apiKeyService, ILogger<PreflightService> logger)
    {
        _context = context;
        _aiService = aiService;
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    public async Task<PreflightResponse> GeneratePreflightPlanAsync(int userId, int profileId, string schemaName, List<string> tableNames)
    {
        var profile = await _context.Profiles.FindAsync(profileId)
            ?? throw new InvalidOperationException("Profile não encontrado");

        // Obter esquema das tabelas
        var schemaInfo = await GetSchemaInfoAsync(profile, schemaName, tableNames);

        // Gerar prompt para a LLM
        var prompt = BuildPreflightPrompt(schemaName, tableNames, schemaInfo);

        // Obter API key do usuário (sem exposição)
        var apiSettings = await _context.UserApiSettings
            .FirstOrDefaultAsync(s => s.UserId == userId)
            ?? throw new InvalidOperationException("Configurações de API não encontradas");

        var apiKey = await _apiKeyService.GetDecryptedApiKeyAsync(userId, apiSettings.Provider);
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("API key não encontrada ou inválida");
        }

        // Chamar LLM com dados simulados para gerar preflight
        var llmResponse = await CallLLMForPreflightAsync(prompt, apiKey, apiSettings.Provider);

        // Parsear resposta JSON
        var preflightResponse = ParsePreflightResponse(llmResponse);

        return preflightResponse;
    }

    public async Task<List<PreflightResult>> ExecutePreflightTestsAsync(ConnectionProfile profile, PreflightResponse preflightPlan)
    {
        var results = new List<PreflightResult>();

        using var connection = new NpgsqlConnection(profile.ConnectionString);
        await connection.OpenAsync();

        // Executar testes de conectividade/introspecção
        foreach (var test in preflightPlan.PreflightTests)
        {
            var result = await ExecuteSingleTestAsync(connection, test, "preflight", null);
            results.Add(result);
        }

        // Executar queries de sanidade
        foreach (var query in preflightPlan.SanityQueries)
        {
            var result = await ExecuteSingleTestAsync(connection,
                new PreflightTest { Name = query.Name, Sql = query.Sql, Expectation = query.Expectation },
                "sanity", query.Table);
            results.Add(result);
        }

        // Persistir resultados
        _context.PreflightResults.AddRange(results);
        await _context.SaveChangesAsync();

        return results;
    }

    private async Task<PreflightResult> ExecuteSingleTestAsync(NpgsqlConnection connection, PreflightTest test, string testType, string? tableName)
    {
        var result = new PreflightResult
        {
            SchemaName = ExtractSchemaFromTest(test),
            TableName = tableName,
            TestType = testType,
            TestName = test.Name,
            SqlExecuted = test.Sql,
            Expectation = test.Expectation,
            ExecutedAt = DateTime.UtcNow
        };

        try
        {
            using var cmd = new NpgsqlCommand(test.Sql, connection);
            cmd.CommandTimeout = 30; // 30 segundos max

            var resultData = new List<Dictionary<string, object>>();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = value?.ToString() ?? "NULL";
                }
                resultData.Add(row);
            }

            result.ResultData = JsonSerializer.Serialize(resultData);
            result.Success = ValidateExpectation(test.Expectation, resultData);

            if (result.Success)
            {
                _logger.LogDebug("Teste {TestName} executado com sucesso", test.Name);
            }
            else
            {
                _logger.LogWarning("Teste {TestName} falhou na expectativa: {Expectation}", test.Name, test.Expectation);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Erro ao executar teste {TestName}: {Sql}", test.Name, test.Sql);
        }

        return result;
    }

    private bool ValidateExpectation(string? expectation, List<Dictionary<string, object>> resultData)
    {
        if (string.IsNullOrEmpty(expectation)) return true;

        return expectation.ToLower() switch
        {
            "one_row_one_col_eq_1" => resultData.Count == 1 && resultData[0].Count == 1 && resultData[0].Values.First()?.ToString() == "1",
            "non_empty" => resultData.Count > 0,
            "n>0" => resultData.Count > 0 && resultData[0].ContainsKey("n") && Convert.ToInt64(resultData[0]["n"]) > 0,
            _ => true // expectativas desconhecidas passam
        };
    }

    private string ExtractSchemaFromTest(PreflightTest test)
    {
        // Extrair schema do SQL (básico)
        var sql = test.Sql.ToLower();
        if (sql.Contains("table_schema"))
        {
            var parts = sql.Split('\'');
            if (parts.Length >= 2)
            {
                return parts[1];
            }
        }

        return "unknown";
    }

    private async Task<Dictionary<string, object>> GetSchemaInfoAsync(ConnectionProfile profile, string schemaName, List<string> tableNames)
    {
        var result = new Dictionary<string, object>();

        using var connection = new NpgsqlConnection(profile.ConnectionString);
        await connection.OpenAsync();

        var tablesFilter = string.Join("','", tableNames);
        var query = $@"
            SELECT
                t.table_name,
                c.column_name,
                c.data_type,
                c.is_nullable,
                c.column_default,
                c.character_maximum_length
            FROM information_schema.tables t
            JOIN information_schema.columns c ON c.table_name = t.table_name
            WHERE t.table_schema = @schema
            AND t.table_name IN ('{tablesFilter}')
            ORDER BY t.table_name, c.ordinal_position;";

        using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@schema", schemaName);

        var tables = new Dictionary<string, List<object>>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(reader.GetOrdinal("table_name"));
            if (!tables.ContainsKey(tableName))
                tables[tableName] = new List<object>();

            tables[tableName].Add(new
            {
                column_name = reader.GetString(reader.GetOrdinal("column_name")),
                data_type = reader.GetString(reader.GetOrdinal("data_type")),
                is_nullable = reader.GetString(reader.GetOrdinal("is_nullable")),
                column_default = reader.IsDBNull(reader.GetOrdinal("column_default")) ? null : reader.GetString(reader.GetOrdinal("column_default")),
                max_length = reader.IsDBNull(reader.GetOrdinal("character_maximum_length")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("character_maximum_length"))
            });
        }

        result["schema"] = schemaName;
        result["tables"] = tables;
        result["table_count"] = tables.Count;

        return result;
    }

    private string BuildPreflightPrompt(string schemaName, List<string> tableNames, Dictionary<string, object> schemaInfo)
    {
        var tablesList = string.Join(", ", tableNames);
        var schemaJson = JsonSerializer.Serialize(schemaInfo, new JsonSerializerOptions { WriteIndented = true });

        return $@"Você é um verificador técnico de Data Quality. Gere um plano de preflight em JSON no dialeto PostgreSQL.

IMPORTANTE: Você deve responder APENAS com JSON válido, sem comentários ou explicações adicionais.

## Contexto
- Schema: {schemaName}
- Tabelas para análise: {tablesList}
- Schema detalhado: {schemaJson}

## Formato de resposta esperado (JSON):

{{
  ""preflight_tests"": [
    {{
      ""name"": ""db_connectivity"",
      ""sql"": ""SELECT 1;"",
      ""expectation"": ""one_row_one_col_eq_1""
    }},
    {{
      ""name"": ""schema_introspection"",
      ""sql"": ""SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = '{schemaName}' LIMIT 10;"",
      ""expectation"": ""non_empty""
    }}
  ],
  ""sanity_queries"": [
    {{
      ""name"": ""row_count"",
      ""table"": ""NOME_TABELA"",
      ""sql"": ""SELECT COUNT(*) AS n FROM {schemaName}.NOME_TABELA;"",
      ""expectation"": ""n>0""
    }},
    {{
      ""name"": ""sample"",
      ""table"": ""NOME_TABELA"",
      ""sql"": ""SELECT * FROM {schemaName}.NOME_TABELA LIMIT 5;"",
      ""expectation"": ""non_empty""
    }}
  ],
  ""rule_candidates"": [
    {{
      ""dimension"": ""completude"",
      ""table"": ""NOME_TABELA"",
      ""column"": ""NOME_COLUNA"",
      ""check_sql"": ""SELECT COUNT(*) AS invalid_count FROM {schemaName}.NOME_TABELA WHERE NOME_COLUNA IS NULL;"",
      ""description"": ""Verificar nulos na coluna NOME_COLUNA"",
      ""severity"": ""medium""
    }}
  ],
  ""notes"": ""Considerações: dialeto PostgreSQL, schema={schemaName}, tabelas verificadas existem no catálogo.""
}}

## Regras importantes:
1. Use apenas PostgreSQL syntax
2. Substitua NOME_TABELA e NOME_COLUNA pelos nomes reais das tabelas/colunas fornecidas
3. Gere pelo menos uma regra por dimensão: completude, consistencia, conformidade, precisao
4. Quotes duplas para nomes de colunas com caracteres especiais: ""nome_coluna""
5. Schema qualificado: {schemaName}.nome_tabela
6. Máximo 2 rule_candidates por tabela para este preflight

Responda apenas com o JSON válido:";
    }

    private PreflightResponse ParsePreflightResponse(string llmResponse)
    {
        try
        {
            // Limpar resposta (remover markdown se houver)
            var cleanResponse = llmResponse.Trim();
            if (cleanResponse.StartsWith("```json"))
            {
                cleanResponse = cleanResponse.Substring(7);
            }
            if (cleanResponse.EndsWith("```"))
            {
                cleanResponse = cleanResponse.Substring(0, cleanResponse.Length - 3);
            }
            cleanResponse = cleanResponse.Trim();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            var response = JsonSerializer.Deserialize<PreflightResponse>(cleanResponse, options);

            if (response == null)
            {
                throw new InvalidOperationException("Resposta da LLM resultou em objeto nulo");
            }

            _logger.LogInformation("Plano de preflight gerado: {TestCount} testes, {QueryCount} queries, {RuleCount} regras",
                response.PreflightTests.Count, response.SanityQueries.Count, response.RuleCandidates.Count);

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Erro ao parsear resposta da LLM: {Response}", llmResponse);

            // Fallback: criar resposta mínima
            return new PreflightResponse
            {
                PreflightTests = new List<PreflightTest>
                {
                    new() { Name = "db_connectivity", Sql = "SELECT 1;", Expectation = "one_row_one_col_eq_1" }
                },
                SanityQueries = new List<SanityQuery>(),
                RuleCandidates = new List<RuleCandidateDto>(),
                Notes = $"Erro ao parsear resposta da LLM: {ex.Message}"
            };
        }
    }

    public async Task<List<RuleCandidate>> PersistRuleCandidatesAsync(string schemaName, List<RuleCandidateDto> candidateDtos)
    {
        var ruleCandidates = candidateDtos.Select(dto => new RuleCandidate
        {
            SchemaName = schemaName,
            TableName = dto.Table,
            ColumnName = dto.Column,
            Dimension = dto.Dimension,
            RuleName = GenerateRuleName(dto),
            CheckSql = dto.CheckSql,
            Description = dto.Description,
            Severity = dto.Severity,
            AutoGenerated = true,
            ApprovedByUser = false,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.RuleCandidates.AddRange(ruleCandidates);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Persistidas {Count} regras candidatas para {Schema}", ruleCandidates.Count, schemaName);

        return ruleCandidates;
    }

    private string GenerateRuleName(RuleCandidateDto dto)
    {
        var suffix = string.IsNullOrEmpty(dto.Column) ? $"_{dto.Table}" : $"_{dto.Table}_{dto.Column}";
        return $"{dto.Dimension}{suffix}_{DateTime.UtcNow:yyyyMMddHHmm}";
    }

    public async Task<List<PreflightResult>> GetPreflightHistoryAsync(string schemaName, string? tableName = null)
    {
        var query = _context.PreflightResults
            .Where(p => p.SchemaName == schemaName);

        if (!string.IsNullOrEmpty(tableName))
        {
            query = query.Where(p => p.TableName == tableName);
        }

        return await query
            .OrderByDescending(p => p.ExecutedAt)
            .Take(50)
            .ToListAsync();
    }

    private async Task<string> CallLLMForPreflightAsync(string prompt, string apiKey, string provider)
    {
        // Usar HttpClient diretamente para chamar a API
        // Simulando chamada similar ao DataQualityAI mas com prompt customizado

        var httpClient = new HttpClient();
        object request;
        string endpoint;

        if (provider.ToLower() == "openai")
        {
            request = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = "Você é um especialista em Data Quality e PostgreSQL. Retorne APENAS JSON válido." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 2000,
                temperature = 0.1
            };
            endpoint = "https://api.openai.com/v1/chat/completions";
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }
        else
        {
            // Claude/Anthropic
            request = new
            {
                model = "claude-3-haiku-20240307",
                max_tokens = 2000,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };
            endpoint = "https://api.anthropic.com/v1/messages";
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(endpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Erro na API da LLM: {response.StatusCode} - {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();

        if (provider.ToLower() == "openai")
        {
            var result = JsonSerializer.Deserialize<DbConnect.Web.AI.OpenAIResponse>(responseJson);
            return result?.Choices?[0]?.Message?.Content ?? throw new Exception("Resposta OpenAI inválida");
        }
        else
        {
            var result = JsonSerializer.Deserialize<DbConnect.Web.AI.ClaudeResponse>(responseJson);
            return result?.Content?[0]?.Text ?? throw new Exception("Resposta Claude inválida");
        }
    }
}