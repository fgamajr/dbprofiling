using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DbConnect.Web.AI;

public class DataQualityAI
{
    private readonly HttpClient _httpClient;

    public DataQualityAI(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DataQualityRules> GenerateRulesAsync(string tableName, string schemaName, List<ColumnSchema> columns, List<Dictionary<string, object?>> sampleData, string apiKey, string provider = "openai")
    {
        var prompt = BuildPrompt(tableName, schemaName, columns, sampleData);
        
        var response = await CallLLMAsync(prompt, apiKey, provider);
        return ParseResponse(response);
    }

    private string BuildPrompt(string tableName, string schemaName, List<ColumnSchema> columns, List<Dictionary<string, object?>> sampleData)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Data Quality Assessment - AI Rule Generation");
        sb.AppendLine();
        sb.AppendLine($"**Tabela:** {schemaName}.{tableName}");
        sb.AppendLine($"**Schema:** {schemaName}");
        sb.AppendLine($"**Nome da Tabela:** {tableName}");
        sb.AppendLine();
        
        // Schema da tabela
        sb.AppendLine("## Schema da Tabela:");
        foreach (var col in columns)
        {
            sb.AppendLine($"- **{col.Name}**: {col.DataType} {(col.IsNullable ? "(nullable)" : "(not null)")}");
        }
        sb.AppendLine();
        
        // Dados de amostra
        sb.AppendLine("## Amostra de Dados (primeiras 10 linhas):");
        sb.AppendLine("```json");
        sb.AppendLine(JsonSerializer.Serialize(sampleData, new JsonSerializerOptions { WriteIndented = true }));
        sb.AppendLine("```");
        sb.AppendLine();
        
        sb.AppendLine(@"## Instrução:

Baseado no schema e nos dados de amostra acima, gere regras de Data Quality para **PostgreSQL 14+** focando nas 6 dimensões fundamentais:

1. **Completude** - Detectar valores nulos/vazios onde não deveriam estar
2. **Unicidade** - Identificar duplicatas onde valores deveriam ser únicos  
3. **Validade** - Validar formatos (email, CPF, CNPJ, telefone, datas, etc.)
4. **Consistência** - Verificar relacionamentos lógicos entre campos
5. **Precisão** - Validar ranges numéricos e comprimentos de texto
6. **Tempestividade** - Validar datas (não futuras, idades válidas, etc.)

**IMPORTANTE:** Retorne APENAS um JSON válido no formato exato abaixo, sem texto adicional:

```json
{
  ""rules"": [
    {
      ""id"": ""rule_001"",
      ""name"": ""Nome da regra"",
      ""description"": ""Descrição clara da regra"",
      ""dimension"": ""completeness|uniqueness|validity|consistency|accuracy|timeliness"",
      ""column"": ""nome_da_coluna"",
      ""sqlCondition"": ""SQL WHERE condition que deve retornar true para dados válidos"",
      ""severity"": ""error|warning|info"",
      ""expectedPassRate"": 95.0
    }
  ]
}
```

**IMPORTANTE: Nas condições SQL, use APENAS os nomes das colunas fornecidos no schema acima. NÃO invente nomes de colunas ou tabelas.**

**Exemplos de sqlCondition válidos:**
- Para completude: `""column_name IS NOT NULL AND TRIM(column_name::text) != ''""`
- Para unicidade: `""column_name IS NOT NULL""` (validação de unicidade será feita separadamente)
- Para validade de email: `""column_name ~* '^[A-Za-z0-9._%-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,4}$'""`
- Para range numérico: `""column_name >= 0 AND column_name <= 120""`
- Para data não futura: `""column_name <= CURRENT_DATE""`
- Para texto não vazio: `""LENGTH(TRIM(column_name::text)) > 0""`
- **Para regras condicionais**: `""campo_obrigatorio IS NOT NULL OR status_ativo != true""`
- **Para validação cruzada**: `""data_fim > data_inicio OR data_fim IS NULL""`
- **Para dependências**: `""cnpj IS NOT NULL OR tipo_pessoa != 'JURIDICA'""`

**IMPORTANTE - REGRAS CONDICIONAIS:**
Identifique dependências lógicas entre campos e gere regras condicionais:
- Campos obrigatórios apenas em certas condições
- Validações que dependem do valor de outros campos
- Consistência entre campos relacionados
- Use a lógica: ""condição_principal OR exceção_quando_não_se_aplica""

**REGRAS OBRIGATÓRIAS PARA POSTGRESQL:**
1. Use APENAS nomes de colunas que existem no schema fornecido
2. Use operadores PostgreSQL corretos:
   - ~* para regex case-insensitive
   - ~ para regex case-sensitive
   - :: para cast de tipos (ex: column_name::text)
   - CURRENT_DATE para data atual (não NOW() ou SYSDATE)
   - LENGTH() para comprimento de strings
   - TRIM() para remover espaços
3. NÃO use subqueries complexas na sqlCondition
4. NÃO use funções específicas de outros bancos (DATEDIFF, ISNULL, etc)
5. Para campos texto, sempre faça cast para ::text quando necessário
6. Use aspas duplas para nomes de colunas com espaços ou caracteres especiais

Baseado EXATAMENTE no schema e dados fornecidos, gere 8-12 regras específicas:");

        return sb.ToString();
    }
    
    public async Task<SqlRefinementResult> RefineFailedRuleAsync(string originalSqlCondition, string errorMessage, string tableName, string schemaName, List<ColumnSchema> columns, string apiKey, string provider = "openai")
    {
        var prompt = BuildRefinementPrompt(originalSqlCondition, errorMessage, tableName, schemaName, columns);
        
        try
        {
            var response = await CallLLMAsync(prompt, apiKey, provider);
            return ParseRefinementResponse(response);
        }
        catch (Exception ex)
        {
            return new SqlRefinementResult
            {
                Success = false,
                ErrorMessage = $"Erro ao chamar IA para refinamento: {ex.Message}",
                OriginalCondition = originalSqlCondition
            };
        }
    }
    
    private string BuildRefinementPrompt(string originalSql, string errorMessage, string tableName, string schemaName, List<ColumnSchema> columns)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Assistente de Refinamento SQL - PostgreSQL 14+");
        sb.AppendLine();
        sb.AppendLine($"**Tabela:** {schemaName}.{tableName}");
        sb.AppendLine($"**Erro SQL Original:** {errorMessage}");
        sb.AppendLine($"**Condição SQL com Problema:** {originalSql}");
        sb.AppendLine();
        
        // Schema da tabela
        sb.AppendLine("## Schema da Tabela Disponível:");
        foreach (var col in columns)
        {
            sb.AppendLine($"- **{col.Name}**: {col.DataType} {(col.IsNullable ? "(nullable)" : "(not null)")}");
        }
        sb.AppendLine();
        
        sb.AppendLine(@"## Instrução:

Analise o erro SQL e corrija a condição para **PostgreSQL 14+**. Problemas comuns incluem:

1. **Colunas inexistentes**: Use APENAS colunas do schema fornecido acima
2. **Sintaxe incorreta**: Ajuste para sintaxe PostgreSQL válida
3. **Tipos incompatíveis**: Use casts apropriados (::text, ::integer, etc.)
4. **Operadores incorretos**: Use ~* para regex case-insensitive, ~ para case-sensitive
5. **Funções inexistentes**: Substitua por equivalentes PostgreSQL (LENGTH, TRIM, etc.)
6. **NULL handling**: Use IS NULL / IS NOT NULL apropriadamente

**IMPORTANTE:** Retorne APENAS um JSON válido no formato exato abaixo:

```json
{
  ""success"": true,
  ""refinedCondition"": ""nova condição SQL corrigida"",
  ""explanation"": ""Explicação breve do que foi corrigido"",
  ""confidence"": 95
}
```

**Se não for possível corrigir:**
```json
{
  ""success"": false,
  ""errorMessage"": ""Motivo pelo qual não foi possível corrigir"",
  ""originalCondition"": """ + originalSql.Replace("\"", "\\\"") + @"""
}
```

**Regras Obrigatórias:**
- Use APENAS nomes de colunas que existem no schema
- Mantenha a lógica original da regra sempre que possível
- Use sintaxe PostgreSQL 14+ válida
- Teste mental a condição antes de responder
- Se usar regex, escape caracteres especiais corretamente");

        return sb.ToString();
    }
    
    private SqlRefinementResult ParseRefinementResponse(string response)
    {
        try
        {
            // Remove any potential markdown code blocks
            var cleanResponse = response.Trim();
            if (cleanResponse.StartsWith("```json"))
            {
                cleanResponse = cleanResponse.Substring(7);
            }
            if (cleanResponse.EndsWith("```"))
            {
                cleanResponse = cleanResponse.Substring(0, cleanResponse.Length - 3);
            }
            
            var result = JsonSerializer.Deserialize<SqlRefinementResult>(cleanResponse.Trim());
            return result ?? new SqlRefinementResult 
            { 
                Success = false, 
                ErrorMessage = "Resposta da IA estava vazia" 
            };
        }
        catch (Exception ex)
        {
            return new SqlRefinementResult
            {
                Success = false,
                ErrorMessage = $"Erro ao processar resposta da IA: {ex.Message}. Resposta: {response}",
                OriginalCondition = ""
            };
        }
    }

    private async Task<string> CallLLMAsync(string prompt, string apiKey, string provider)
    {
        object request;
        string endpoint;
        
        if (provider == "openai")
        {
            request = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = "Você é um especialista em Data Quality. Retorne APENAS JSON válido, sem texto adicional." },
                    new { role = "user", content = prompt }
                },
                max_tokens = 2000,
                temperature = 0.1
            };
            endpoint = "https://api.openai.com/v1/chat/completions";
            _httpClient.DefaultRequestHeaders.Authorization = 
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
                    new { role = "user", content = $"Você é um especialista em Data Quality. Retorne APENAS JSON válido, sem texto adicional.\n\n{prompt}" }
                }
            };
            endpoint = "https://api.anthropic.com/v1/messages";
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        }

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync(endpoint, content);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"LLM API error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        
        if (provider == "openai")
        {
            var result = JsonSerializer.Deserialize<OpenAIResponse>(responseJson);
            return result?.Choices?[0]?.Message?.Content ?? throw new Exception("Invalid OpenAI response");
        }
        else
        {
            var result = JsonSerializer.Deserialize<ClaudeResponse>(responseJson);
            return result?.Content?[0]?.Text ?? throw new Exception("Invalid Claude response");
        }
    }

    private DataQualityRules ParseResponse(string response)
    {
        try
        {
            // Remove any potential markdown code blocks
            var cleanResponse = response.Trim();
            if (cleanResponse.StartsWith("```json"))
            {
                cleanResponse = cleanResponse.Substring(7);
            }
            if (cleanResponse.EndsWith("```"))
            {
                cleanResponse = cleanResponse.Substring(0, cleanResponse.Length - 3);
            }
            
            return JsonSerializer.Deserialize<DataQualityRules>(cleanResponse.Trim()) 
                ?? throw new Exception("Failed to deserialize AI response");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to parse AI response: {ex.Message}. Response: {response}");
        }
    }
}

public class ColumnSchema
{
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsNullable { get; set; }
}

public class DataQualityRules
{
    [JsonPropertyName("rules")]
    public List<DataQualityRule> Rules { get; set; } = new();
}

public class DataQualityRule
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";
    
    [JsonPropertyName("dimension")]
    public string Dimension { get; set; } = "";
    
    [JsonPropertyName("column")]
    public string Column { get; set; } = "";
    
    [JsonPropertyName("sqlCondition")]
    public string SqlCondition { get; set; } = "";
    
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "";
    
    [JsonPropertyName("expectedPassRate")]
    public double ExpectedPassRate { get; set; } = 95.0;
}

public class OpenAIResponse
{
    [JsonPropertyName("choices")]
    public List<Choice>? Choices { get; set; }
}

public class Choice
{
    [JsonPropertyName("message")]
    public Message? Message { get; set; }
}

public class Message
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

public class ClaudeResponse
{
    [JsonPropertyName("content")]
    public List<ClaudeContent>? Content { get; set; }
}

public class ClaudeContent
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class SqlRefinementResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("refinedCondition")]
    public string? RefinedCondition { get; set; }
    
    [JsonPropertyName("explanation")]
    public string? Explanation { get; set; }
    
    [JsonPropertyName("confidence")]
    public int Confidence { get; set; } = 0;
    
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
    
    [JsonPropertyName("originalCondition")]
    public string? OriginalCondition { get; set; }
}