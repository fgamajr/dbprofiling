using DbConnect.Web.Services.Enhanced;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DbConnect.Web.AI.Enhanced;

/// <summary>
/// Enhanced Data Quality AI
/// Evolução do DataQualityAI original para gerar validações cruzadas contextuais
/// Usa contexto rico multi-tabela para validações impossíveis com regras estáticas
/// </summary>
public interface IEnhancedDataQualityAI
{
    Task<CrossTableValidationResult> GenerateCrossTableValidationsAsync(RichAnalysisContext context, string? apiKey);
    Task<List<ContextualValidation>> GenerateContextualValidationsAsync(RichAnalysisContext context, string? apiKey);
    Task<ValidationInsights> AnalyzeValidationPatternsAsync(List<ContextualValidation> validations);
}

public class EnhancedDataQualityAI : IEnhancedDataQualityAI
{
    private readonly ILogger<EnhancedDataQualityAI> _logger;
    private readonly HttpClient _httpClient;

    public EnhancedDataQualityAI(ILogger<EnhancedDataQualityAI> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gerar validações cruzadas usando contexto rico
    /// Este é o coração do sistema de IA enhanced
    /// </summary>
    public async Task<CrossTableValidationResult> GenerateCrossTableValidationsAsync(RichAnalysisContext context, string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentException("API Key é obrigatória para geração de validações IA");
        }

        _logger.LogInformation("🧠 Gerando validações cruzadas para tabela {FocusTable} com contexto {Complexity}",
            context.FocusTable, context.ContextMetrics.ContextComplexity);

        try
        {
            // 1. Gerar prompt contextual rico
            var prompt = BuildEnhancedContextualPrompt(context);
            _logger.LogInformation("📝 Prompt contextual gerado: {Length} caracteres", prompt.Length);

            // 2. Chamar IA para gerar validações
            var aiResponse = await CallOpenAIForValidationsAsync(prompt, apiKey);
            _logger.LogInformation("🤖 Resposta da IA recebida: {Length} caracteres", aiResponse.Length);

            // 3. Parsear e estruturar validações
            var validations = ParseAIValidationsResponse(aiResponse);
            _logger.LogInformation("✅ Parseadas {Count} validações da resposta IA", validations.Count);

            // 4. Enriquecer validações com contexto adicional
            var enrichedValidations = await EnrichValidationsWithContextAsync(validations, context);

            // 5. Analisar padrões e insights
            var insights = await AnalyzeValidationPatternsAsync(enrichedValidations);

            var result = new CrossTableValidationResult
            {
                FocusTable = context.FocusTable,
                ContextComplexity = context.ContextMetrics.ContextComplexity,
                TotalValidationsGenerated = enrichedValidations.Count,
                CrossTableValidations = enrichedValidations,
                ValidationInsights = insights,
                GeneratedAt = DateTime.UtcNow,
                AIModel = "gpt-4",
                ContextMetrics = context.ContextMetrics
            };

            _logger.LogInformation("🎯 Validações cruzadas geradas com sucesso: {Count} validações, {Insights} insights",
                result.TotalValidationsGenerated, result.ValidationInsights.KeyInsights.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao gerar validações cruzadas para {FocusTable}", context.FocusTable);
            throw;
        }
    }

    /// <summary>
    /// Gerar validações contextuais (interface simplificada)
    /// </summary>
    public async Task<List<ContextualValidation>> GenerateContextualValidationsAsync(RichAnalysisContext context, string? apiKey)
    {
        var result = await GenerateCrossTableValidationsAsync(context, apiKey);
        return result.CrossTableValidations;
    }

    /// <summary>
    /// Analisar padrões nas validações geradas
    /// </summary>
    public Task<ValidationInsights> AnalyzeValidationPatternsAsync(List<ContextualValidation> validations)
    {
        var insights = new ValidationInsights();

        // 1. Analisar distribuição por tipo
        var typeDistribution = validations
            .GroupBy(v => v.ValidationType)
            .ToDictionary(g => g.Key, g => g.Count());

        insights.TypeDistribution = typeDistribution;

        // 2. Identificar validações de alta prioridade
        insights.HighPriorityValidations = validations
            .Where(v => v.Priority >= 8)
            .Select(v => v.Description)
            .ToList();

        // 3. Analisar relacionamentos envolvidos
        var relationshipTypes = validations
            .SelectMany(v => v.InvolvedTables)
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToDictionary(g => g.Key, g => g.Count());

        insights.MostInvolvedTables = relationshipTypes;

        // 4. Identificar padrões de complexidade
        var complexityPatterns = validations
            .GroupBy(v => v.Complexity)
            .ToDictionary(g => g.Key, g => g.Count());

        insights.ComplexityDistribution = complexityPatterns;

        // 5. Gerar insights principais
        insights.KeyInsights = GenerateKeyInsights(validations, typeDistribution, relationshipTypes);

        return Task.FromResult(insights);
    }

    /// <summary>
    /// Construir prompt contextual enhanced
    /// Muito mais rico que o prompt original
    /// </summary>
    private string BuildEnhancedContextualPrompt(RichAnalysisContext context)
    {
        var basePrompt = context.GenerateContextualPrompt();

        // Adicionar instruções específicas para validações cruzadas
        var enhancedPrompt = basePrompt + @"

## INSTRUÇÕES ESPECÍFICAS PARA VALIDAÇÕES CRUZADAS:

### FOQUE EM:
1. **Integridade Referencial**: Registros órfãos, FKs inválidas
2. **Consistência Temporal**: Datas que não fazem sentido cronologicamente
3. **Consistência de Status**: Estados incompatíveis entre tabelas relacionadas
4. **Regras de Negócio Implícitas**: Padrões identificados nos dados
5. **Anomalias Cross-Table**: Outliers detectados via relacionamentos

### EVITE:
- Validações isoladas de uma tabela só
- Regras genéricas que já existem
- Validações impossíveis de executar

### FORMATO DE RESPOSTA:
Retorne EXATAMENTE 15 validações numeradas, uma por linha:
1. [Descrição da validação em linguagem natural]
2. [Descrição da validação em linguagem natural]
...
15. [Descrição da validação em linguagem natural]

IMPORTANTE: Use nomes EXATOS das tabelas e colunas do contexto fornecido.
";

        return enhancedPrompt;
    }

    /// <summary>
    /// Chamar OpenAI para gerar validações
    /// </summary>
    private async Task<string> CallOpenAIForValidationsAsync(string prompt, string apiKey)
    {
        var requestPayload = new
        {
            model = "gpt-4o",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = @"Você é um especialista em qualidade de dados e análise de bancos de dados PostgreSQL.
Sua especialidade é identificar validações de qualidade que só são possíveis quando você entende as relações entre tabelas.
Você sempre retorna validações práticas, executáveis e que fazem sentido para o negócio.
Foque em validações CRUZADAS entre tabelas, não em validações isoladas."
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            temperature = 0.3, // Baixa criatividade para consistência
            max_tokens = 2000,
            top_p = 0.9
        };

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var jsonPayload = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions { WriteIndented = false });
        var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"OpenAI API error: {response.StatusCode} - {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var responseObj = JsonSerializer.Deserialize<JsonElement>(responseContent);

        return responseObj
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    /// <summary>
    /// Parsear resposta da IA em validações estruturadas
    /// </summary>
    private List<ContextualValidation> ParseAIValidationsResponse(string aiResponse)
    {
        var validations = new List<ContextualValidation>();

        // Regex para capturar validações numeradas
        var regex = new Regex(@"^\s*(\d+)\.\s*(.+)$", RegexOptions.Multiline);
        var matches = regex.Matches(aiResponse);

        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                var number = int.Parse(match.Groups[1].Value);
                var description = match.Groups[2].Value.Trim();

                var validation = new ContextualValidation
                {
                    Id = Guid.NewGuid(),
                    Number = number,
                    Description = description,
                    ValidationType = ClassifyValidationType(description),
                    Priority = CalculateValidationPriority(description),
                    Complexity = CalculateValidationComplexity(description),
                    InvolvedTables = ExtractInvolvedTables(description),
                    GeneratedBy = "enhanced-ai",
                    CreatedAt = DateTime.UtcNow
                };

                validations.Add(validation);
            }
        }

        return validations.OrderBy(v => v.Number).ToList();
    }

    /// <summary>
    /// Enriquecer validações com contexto adicional
    /// </summary>
    private Task<List<ContextualValidation>> EnrichValidationsWithContextAsync(
        List<ContextualValidation> validations,
        RichAnalysisContext context)
    {
        foreach (var validation in validations)
        {
            // Identificar relacionamentos envolvidos
            var involvedRelationships = context.RelatedTables
                .Where(rt => validation.InvolvedTables.Any(table => rt.TableName.Contains(table, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            validation.InvolvedRelationships = involvedRelationships.Select(rt => new ValidationRelationship
            {
                TableName = rt.TableName,
                RelationshipType = rt.RelationshipType,
                JoinCondition = rt.JoinCondition,
                ConfidenceLevel = rt.ConfidenceLevel
            }).ToList();

            // Adicionar tags baseadas no contexto
            validation.Tags = GenerateValidationTags(validation, context);

            // Calcular score de relevância
            validation.RelevanceScore = CalculateRelevanceScore(validation, context);
        }

        return Task.FromResult(validations);
    }

    /// <summary>
    /// Classificar tipo de validação
    /// </summary>
    private string ClassifyValidationType(string description)
    {
        var lowerDesc = description.ToLower();

        if (lowerDesc.Contains("órfão") || lowerDesc.Contains("referencial") || lowerDesc.Contains("existe"))
            return "REFERENTIAL_INTEGRITY";

        if (lowerDesc.Contains("data") || lowerDesc.Contains("temporal") || lowerDesc.Contains("anterior") || lowerDesc.Contains("posterior"))
            return "TEMPORAL_CONSISTENCY";

        if (lowerDesc.Contains("status") || lowerDesc.Contains("ativo") || lowerDesc.Contains("inativo"))
            return "STATUS_CONSISTENCY";

        if (lowerDesc.Contains("duplicat") || lowerDesc.Contains("único"))
            return "UNIQUENESS";

        if (lowerDesc.Contains("formato") || lowerDesc.Contains("padrão") || lowerDesc.Contains("válid"))
            return "FORMAT_VALIDATION";

        if (lowerDesc.Contains("outlier") || lowerDesc.Contains("anomal"))
            return "ANOMALY_DETECTION";

        return "BUSINESS_RULE";
    }

    /// <summary>
    /// Calcular prioridade da validação
    /// </summary>
    private int CalculateValidationPriority(string description)
    {
        var lowerDesc = description.ToLower();
        var priority = 5; // Base

        // Alta prioridade
        if (lowerDesc.Contains("órfão") || lowerDesc.Contains("referencial")) priority += 3;
        if (lowerDesc.Contains("crítico") || lowerDesc.Contains("obrigatório")) priority += 2;
        if (lowerDesc.Contains("inconsistent") || lowerDesc.Contains("inválid")) priority += 2;

        // Prioridade média
        if (lowerDesc.Contains("status") || lowerDesc.Contains("temporal")) priority += 1;
        if (lowerDesc.Contains("duplicat")) priority += 1;

        return Math.Min(10, priority);
    }

    /// <summary>
    /// Calcular complexidade da validação
    /// </summary>
    private string CalculateValidationComplexity(string description)
    {
        var lowerDesc = description.ToLower();
        var joinCount = Regex.Matches(lowerDesc, @"\bjoin\b|\be\b").Count;
        var tableCount = Regex.Matches(lowerDesc, @"\btabel\w*\b").Count;

        if (joinCount >= 2 || tableCount >= 3) return "HIGH";
        if (joinCount >= 1 || tableCount >= 2) return "MEDIUM";
        return "LOW";
    }

    /// <summary>
    /// Extrair tabelas envolvidas na validação
    /// </summary>
    private List<string> ExtractInvolvedTables(string description)
    {
        var tables = new List<string>();

        // Regex para capturar nomes de tabelas (simplificado)
        var regex = new Regex(@"\b([a-zA-Z_][a-zA-Z0-9_]*)\.[a-zA-Z_][a-zA-Z0-9_]*\b");
        var matches = regex.Matches(description);

        foreach (Match match in matches)
        {
            var tableName = match.Groups[1].Value;
            if (!tables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
            {
                tables.Add(tableName);
            }
        }

        return tables;
    }

    /// <summary>
    /// Gerar tags para validação
    /// </summary>
    private List<string> GenerateValidationTags(ContextualValidation validation, RichAnalysisContext context)
    {
        var tags = new List<string>();

        // Tags baseadas no tipo
        tags.Add(validation.ValidationType.ToLower());

        // Tags baseadas na complexidade
        tags.Add($"complexity-{validation.Complexity.ToLower()}");

        // Tags baseadas na prioridade
        if (validation.Priority >= 8) tags.Add("high-priority");
        if (validation.Priority <= 4) tags.Add("low-priority");

        // Tags baseadas no contexto
        if (validation.InvolvedTables.Count > 2) tags.Add("multi-table");
        if (validation.Description.ToLower().Contains("cross")) tags.Add("cross-table");

        return tags;
    }

    /// <summary>
    /// Calcular score de relevância
    /// </summary>
    private double CalculateRelevanceScore(ContextualValidation validation, RichAnalysisContext context)
    {
        var score = 0.0;

        // Score baseado na prioridade (0-40 pontos)
        score += validation.Priority * 4;

        // Score baseado no número de relacionamentos (0-20 pontos)
        score += Math.Min(20, validation.InvolvedRelationships.Count * 5);

        // Score baseado no tipo de validação (0-20 pontos)
        score += validation.ValidationType switch
        {
            "REFERENTIAL_INTEGRITY" => 20,
            "TEMPORAL_CONSISTENCY" => 15,
            "STATUS_CONSISTENCY" => 15,
            "UNIQUENESS" => 10,
            _ => 5
        };

        // Score baseado na complexidade (0-20 pontos)
        score += validation.Complexity switch
        {
            "HIGH" => 20,
            "MEDIUM" => 10,
            _ => 5
        };

        return Math.Min(100, score);
    }

    /// <summary>
    /// Gerar insights principais
    /// </summary>
    private List<string> GenerateKeyInsights(
        List<ContextualValidation> validations,
        Dictionary<string, int> typeDistribution,
        Dictionary<string, int> relationshipTypes)
    {
        var insights = new List<string>();

        // Insight sobre distribuição de tipos
        var mostCommonType = typeDistribution.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
        if (mostCommonType.Key != null)
        {
            insights.Add($"Tipo de validação mais comum: {mostCommonType.Key} ({mostCommonType.Value} validações)");
        }

        // Insight sobre prioridade
        var highPriorityCount = validations.Count(v => v.Priority >= 8);
        if (highPriorityCount > 0)
        {
            insights.Add($"{highPriorityCount} validações de alta prioridade identificadas");
        }

        // Insight sobre complexidade
        var complexValidations = validations.Count(v => v.Complexity == "HIGH");
        if (complexValidations > 0)
        {
            insights.Add($"{complexValidations} validações de alta complexidade requerem joins múltiplos");
        }

        // Insight sobre tabelas mais envolvidas
        var mostInvolvedTable = relationshipTypes.FirstOrDefault();
        if (mostInvolvedTable.Key != null)
        {
            insights.Add($"Tabela mais envolvida nas validações: {mostInvolvedTable.Key} ({mostInvolvedTable.Value} validações)");
        }

        return insights;
    }
}

/// <summary>
/// Resultado das validações cruzadas
/// </summary>
public class CrossTableValidationResult
{
    public string FocusTable { get; set; } = string.Empty;
    public string ContextComplexity { get; set; } = string.Empty;
    public int TotalValidationsGenerated { get; set; }
    public List<ContextualValidation> CrossTableValidations { get; set; } = new();
    public ValidationInsights ValidationInsights { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public string AIModel { get; set; } = string.Empty;
    public ContextMetrics ContextMetrics { get; set; } = new();
}

/// <summary>
/// Validação contextual
/// </summary>
public class ContextualValidation
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ValidationType { get; set; } = string.Empty;
    public int Priority { get; set; } // 1-10
    public string Complexity { get; set; } = string.Empty; // LOW, MEDIUM, HIGH
    public List<string> InvolvedTables { get; set; } = new();
    public List<ValidationRelationship> InvolvedRelationships { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public double RelevanceScore { get; set; } // 0-100
    public string GeneratedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Será preenchido na próxima fase (tradução para SQL)
    public string? TranslatedSQL { get; set; }
    public string? TranslationMethod { get; set; } // "mcp", "template", "manual"
}

/// <summary>
/// Relacionamento envolvido na validação
/// </summary>
public class ValidationRelationship
{
    public string TableName { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public string JoinCondition { get; set; } = string.Empty;
    public double ConfidenceLevel { get; set; }
}

/// <summary>
/// Insights das validações
/// </summary>
public class ValidationInsights
{
    public Dictionary<string, int> TypeDistribution { get; set; } = new();
    public Dictionary<string, int> ComplexityDistribution { get; set; } = new();
    public Dictionary<string, int> MostInvolvedTables { get; set; } = new();
    public List<string> HighPriorityValidations { get; set; } = new();
    public List<string> KeyInsights { get; set; } = new();
}