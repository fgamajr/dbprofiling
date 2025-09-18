using DbConnect.Web.AI.Enhanced;
using DbConnect.Web.Models.SchemaDiscovery;
using DbConnect.Web.Services.Enhanced;
using Dapper;
using Npgsql;

namespace DbConnect.Web.Services.Enhanced;

/// <summary>
/// Hybrid SQL Generator
/// Combina IA criativa + MCP preciso para gerar SQL otimizado
/// Este é o componente que une tudo: Discovery + Context + AI + Translation
/// </summary>
public interface IHybridSQLGenerator
{
    Task<HybridValidationResult> GenerateExecutableValidationsAsync(string connectionString, string focusTable, string? businessContext, string? apiKey);
    Task<List<ExecutableValidation>> ExecuteValidationsAsync(string connectionString, List<TranslatedValidation> validations);
    Task<ValidationExecutionSummary> ExecuteAndSummarizeAsync(string connectionString, string focusTable, string? businessContext, string? apiKey);
}

public class HybridSQLGenerator : IHybridSQLGenerator
{
    private readonly IMCPSchemaDiscoveryEngine _schemaDiscovery;
    private readonly IEnhancedContextCollector _contextCollector;
    private readonly IEnhancedDataQualityAI _enhancedAI;
    private readonly IMCPTranslationService _translationService;
    private readonly ILogger<HybridSQLGenerator> _logger;

    public HybridSQLGenerator(
        IMCPSchemaDiscoveryEngine schemaDiscovery,
        IEnhancedContextCollector contextCollector,
        IEnhancedDataQualityAI enhancedAI,
        IMCPTranslationService translationService,
        ILogger<HybridSQLGenerator> logger)
    {
        _schemaDiscovery = schemaDiscovery;
        _contextCollector = contextCollector;
        _enhancedAI = enhancedAI;
        _translationService = translationService;
        _logger = logger;
    }

    /// <summary>
    /// Pipeline completo: Discovery → Context → AI → Translation
    /// Este é o método principal que orquestra todo o processo
    /// </summary>
    public async Task<HybridValidationResult> GenerateExecutableValidationsAsync(
        string connectionString,
        string focusTable,
        string? businessContext,
        string? apiKey)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("🚀 Iniciando pipeline híbrido para tabela: {FocusTable}", focusTable);

        try
        {
            // FASE 1: Descoberta automática do schema (1-3 segundos)
            _logger.LogInformation("📊 FASE 1: Descoberta automática do schema");
            var discoveryStart = DateTime.UtcNow;
            var databaseSchema = await _schemaDiscovery.DiscoverCompleteSchemaAsync(connectionString);
            var discoveryDuration = DateTime.UtcNow - discoveryStart;
            _logger.LogInformation("✅ Schema descoberto em {Duration}ms: {Tables} tabelas, {FKs} FKs, {Implicit} implícitos",
                discoveryDuration.TotalMilliseconds, databaseSchema.Tables.Count, databaseSchema.ForeignKeys.Count, databaseSchema.ImplicitRelations.Count);

            // FASE 2: Coleta de contexto rico (2-5 segundos)
            _logger.LogInformation("🧠 FASE 2: Coleta de contexto rico multi-tabela");
            var contextStart = DateTime.UtcNow;
            var richContext = await _contextCollector.BuildMultiTableContextAsync(connectionString, focusTable, businessContext);
            var contextDuration = DateTime.UtcNow - contextStart;
            _logger.LogInformation("✅ Contexto coletado em {Duration}ms: {RelatedTables} tabelas relacionadas, {SampleSize} amostras",
                contextDuration.TotalMilliseconds, richContext.RelatedTables.Count, richContext.CrossTableSample.TotalSampleSize);

            // FASE 3: Geração de validações via IA (5-15 segundos)
            _logger.LogInformation("🤖 FASE 3: Geração de validações cruzadas via IA");
            var aiStart = DateTime.UtcNow;
            var aiValidations = await _enhancedAI.GenerateCrossTableValidationsAsync(richContext, apiKey);
            var aiDuration = DateTime.UtcNow - aiStart;
            _logger.LogInformation("✅ Validações geradas em {Duration}ms: {Count} validações cruzadas",
                aiDuration.TotalMilliseconds, aiValidations.TotalValidationsGenerated);

            // FASE 4: Tradução para SQL executável (3-10 segundos)
            _logger.LogInformation("🔄 FASE 4: Tradução para SQL executável via MCP");
            var translationStart = DateTime.UtcNow;
            var translatedValidations = await _translationService.TranslateMultipleValidationsAsync(
                aiValidations.CrossTableValidations, databaseSchema, apiKey);
            var translationDuration = DateTime.UtcNow - translationStart;
            var successfulTranslations = translatedValidations.Count(t => t.IsValidSQL);
            _logger.LogInformation("✅ Traduções concluídas em {Duration}ms: {Success}/{Total} sucessos",
                translationDuration.TotalMilliseconds, successfulTranslations, translatedValidations.Count);

            var totalDuration = DateTime.UtcNow - startTime;

            var result = new HybridValidationResult
            {
                FocusTable = focusTable,
                DatabaseSchema = databaseSchema,
                RichContext = richContext,
                AIValidations = aiValidations,
                TranslatedValidations = translatedValidations,
                PerformanceMetrics = new PipelinePerformanceMetrics
                {
                    DiscoveryDuration = discoveryDuration,
                    ContextCollectionDuration = contextDuration,
                    AIGenerationDuration = aiDuration,
                    TranslationDuration = translationDuration,
                    TotalDuration = totalDuration
                },
                GeneratedAt = DateTime.UtcNow,
                BusinessContext = businessContext ?? ""
            };

            _logger.LogInformation("🎯 Pipeline híbrido concluído em {Duration}ms: {Success} validações SQL prontas para execução",
                totalDuration.TotalMilliseconds, successfulTranslations);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro no pipeline híbrido para tabela {FocusTable}", focusTable);
            throw;
        }
    }

    /// <summary>
    /// Executar validações SQL no banco de dados
    /// </summary>
    public async Task<List<ExecutableValidation>> ExecuteValidationsAsync(
        string connectionString,
        List<TranslatedValidation> validations)
    {
        _logger.LogInformation("⚡ Executando {Count} validações SQL no banco", validations.Count);

        var executableValidations = new List<ExecutableValidation>();

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        // Executar validações em paralelo (limitado para não sobrecarregar o banco)
        var semaphore = new SemaphoreSlim(5, 5); // Máximo 5 queries simultâneas
        var executionTasks = validations.Where(v => v.IsValidSQL).Select(async validation =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await ExecuteSingleValidationAsync(connection, validation);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(executionTasks);
        executableValidations.AddRange(results);

        var successCount = executableValidations.Count(v => v.ExecutionStatus == "SUCCESS");
        _logger.LogInformation("✅ Execução concluída: {Success}/{Total} validações executadas com sucesso",
            successCount, validations.Count);

        return executableValidations;
    }

    /// <summary>
    /// Pipeline completo: gerar + executar + sumarizar
    /// Método conveniente que faz tudo em uma chamada
    /// </summary>
    public async Task<ValidationExecutionSummary> ExecuteAndSummarizeAsync(
        string connectionString,
        string focusTable,
        string? businessContext,
        string? apiKey)
    {
        _logger.LogInformation("🎯 Executando pipeline completo para {FocusTable}", focusTable);

        // 1. Gerar validações
        var hybridResult = await GenerateExecutableValidationsAsync(connectionString, focusTable, businessContext, apiKey);

        // 2. Executar validações
        var executedValidations = await ExecuteValidationsAsync(connectionString, hybridResult.TranslatedValidations);

        // 3. Sumarizar resultados
        var summary = GenerateExecutionSummary(hybridResult, executedValidations);

        _logger.LogInformation("✅ Pipeline completo finalizado: {IssuesFound} problemas detectados em {Duration}ms",
            summary.TotalIssuesDetected, summary.TotalExecutionTime.TotalMilliseconds);

        return summary;
    }

    /// <summary>
    /// Executar uma validação individual
    /// </summary>
    private async Task<ExecutableValidation> ExecuteSingleValidationAsync(
        NpgsqlConnection connection,
        TranslatedValidation validation)
    {
        var startTime = DateTime.UtcNow;
        var executableValidation = new ExecutableValidation
        {
            ValidationId = validation.OriginalValidation.Id,
            Description = validation.OriginalValidation.Description,
            SQL = validation.TranslatedSQL,
            Priority = validation.OriginalValidation.Priority,
            ValidationType = validation.OriginalValidation.ValidationType,
            ExecutedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogDebug("🔍 Executando: {Description}", validation.OriginalValidation.Description);

            // Executar SQL com timeout
            var results = await connection.QueryAsync(validation.TranslatedSQL, commandTimeout: 60);
            var resultsList = results.ToList();

            executableValidation.ExecutionStatus = "SUCCESS";
            executableValidation.ExecutionDuration = DateTime.UtcNow - startTime;
            executableValidation.ResultCount = resultsList.Count;

            // Analisar resultados para detectar problemas
            executableValidation.ValidationResult = AnalyzeValidationResults(resultsList, validation.OriginalValidation);

            _logger.LogDebug("✅ Validação executada: {Description} - {Status}",
                validation.OriginalValidation.Description, executableValidation.ValidationResult.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao executar validação: {Description}", validation.OriginalValidation.Description);

            executableValidation.ExecutionStatus = "ERROR";
            executableValidation.ErrorMessage = ex.Message;
            executableValidation.ExecutionDuration = DateTime.UtcNow - startTime;
            executableValidation.ValidationResult = new ValidationResult
            {
                Status = "ERROR",
                Message = $"Erro na execução: {ex.Message}",
                IssuesDetected = 0
            };
        }

        return executableValidation;
    }

    /// <summary>
    /// Analisar resultados da validação para detectar problemas
    /// </summary>
    private ValidationResult AnalyzeValidationResults(List<dynamic> results, ContextualValidation validation)
    {
        if (!results.Any())
        {
            return new ValidationResult
            {
                Status = "NO_DATA",
                Message = "Consulta não retornou dados",
                IssuesDetected = 0,
                TotalRecords = 0
            };
        }

        var firstResult = (IDictionary<string, object>)results.First();

        // Tentar extrair métricas comuns dos resultados
        var totalRecords = ExtractMetricFromResult(firstResult, new[] { "total_records", "total", "count" });
        var validRecords = ExtractMetricFromResult(firstResult, new[] { "valid_records", "valid", "consistent" });
        var invalidRecords = ExtractMetricFromResult(firstResult, new[] { "invalid_records", "invalid", "inconsistent", "issues_found", "orphaned_records" });

        var issuesDetected = invalidRecords > 0 ? invalidRecords : 0;
        var qualityPercentage = totalRecords > 0 ? (double)(totalRecords - issuesDetected) / totalRecords * 100 : 100;

        var status = issuesDetected == 0 ? "PASS" : (qualityPercentage < 50 ? "CRITICAL" : "ISSUES_FOUND");

        return new ValidationResult
        {
            Status = status,
            Message = GenerateResultMessage(validation, totalRecords, issuesDetected, qualityPercentage),
            IssuesDetected = issuesDetected,
            TotalRecords = totalRecords,
            QualityPercentage = qualityPercentage,
            RawResults = results
        };
    }

    /// <summary>
    /// Extrair métrica dos resultados usando diferentes nomes possíveis
    /// </summary>
    private long ExtractMetricFromResult(IDictionary<string, object> result, string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            if (result.ContainsKey(name) && result[name] != null)
            {
                if (long.TryParse(result[name].ToString(), out var value))
                {
                    return value;
                }
            }
        }
        return 0;
    }

    /// <summary>
    /// Gerar mensagem de resultado
    /// </summary>
    private string GenerateResultMessage(ContextualValidation validation, long totalRecords, long issuesDetected, double qualityPercentage)
    {
        if (issuesDetected == 0)
        {
            return $"✅ Validação passou: {totalRecords:N0} registros verificados, nenhum problema detectado";
        }

        return $"⚠️ {issuesDetected:N0} problemas detectados em {totalRecords:N0} registros ({qualityPercentage:F1}% de qualidade)";
    }

    /// <summary>
    /// Gerar sumário da execução
    /// </summary>
    private ValidationExecutionSummary GenerateExecutionSummary(
        HybridValidationResult hybridResult,
        List<ExecutableValidation> executedValidations)
    {
        var summary = new ValidationExecutionSummary
        {
            FocusTable = hybridResult.FocusTable,
            TotalValidationsGenerated = hybridResult.TranslatedValidations.Count,
            TotalValidationsExecuted = executedValidations.Count,
            SuccessfulExecutions = executedValidations.Count(v => v.ExecutionStatus == "SUCCESS"),
            FailedExecutions = executedValidations.Count(v => v.ExecutionStatus == "ERROR"),
            TotalIssuesDetected = executedValidations.Sum(v => v.ValidationResult.IssuesDetected),
            AverageQualityScore = executedValidations.Where(v => v.ExecutionStatus == "SUCCESS")
                                                   .Average(v => v.ValidationResult.QualityPercentage),
            TotalExecutionTime = hybridResult.PerformanceMetrics.TotalDuration,
            ExecutedValidations = executedValidations,
            PerformanceMetrics = hybridResult.PerformanceMetrics,
            GeneratedAt = DateTime.UtcNow
        };

        // Classificar problemas por prioridade
        summary.HighPriorityIssues = executedValidations
            .Where(v => v.ValidationResult.Status == "CRITICAL" || (v.Priority >= 8 && v.ValidationResult.IssuesDetected > 0))
            .ToList();

        summary.MediumPriorityIssues = executedValidations
            .Where(v => v.ValidationResult.Status == "ISSUES_FOUND" && v.Priority >= 5 && v.Priority < 8)
            .ToList();

        // Gerar recomendações
        summary.Recommendations = GenerateRecommendations(summary);

        return summary;
    }

    /// <summary>
    /// Gerar recomendações baseadas nos resultados
    /// </summary>
    private List<string> GenerateRecommendations(ValidationExecutionSummary summary)
    {
        var recommendations = new List<string>();

        if (summary.TotalIssuesDetected == 0)
        {
            recommendations.Add("✅ Excelente! Nenhum problema de qualidade detectado");
        }
        else
        {
            if (summary.HighPriorityIssues.Any())
            {
                recommendations.Add($"🚨 CRÍTICO: {summary.HighPriorityIssues.Count} problemas de alta prioridade requerem atenção imediata");
            }

            if (summary.AverageQualityScore < 80)
            {
                recommendations.Add("⚠️ Qualidade geral abaixo do ideal - considere implementar validações automáticas");
            }

            var referentialIssues = summary.ExecutedValidations.Count(v => v.ValidationType == "REFERENTIAL_INTEGRITY" && v.ValidationResult.IssuesDetected > 0);
            if (referentialIssues > 0)
            {
                recommendations.Add($"🔗 {referentialIssues} problemas de integridade referencial - verificar FKs e relacionamentos");
            }

            var temporalIssues = summary.ExecutedValidations.Count(v => v.ValidationType == "TEMPORAL_CONSISTENCY" && v.ValidationResult.IssuesDetected > 0);
            if (temporalIssues > 0)
            {
                recommendations.Add($"📅 {temporalIssues} problemas de consistência temporal - verificar datas e cronologia");
            }
        }

        if (summary.FailedExecutions > 0)
        {
            recommendations.Add($"🔧 {summary.FailedExecutions} validações falharam na execução - verificar SQL e esquema");
        }

        return recommendations;
    }
}

/// <summary>
/// Resultado do pipeline híbrido completo
/// </summary>
public class HybridValidationResult
{
    public string FocusTable { get; set; } = string.Empty;
    public EnhancedDatabaseSchema DatabaseSchema { get; set; } = new();
    public RichAnalysisContext RichContext { get; set; } = new();
    public CrossTableValidationResult AIValidations { get; set; } = new();
    public List<TranslatedValidation> TranslatedValidations { get; set; } = new();
    public PipelinePerformanceMetrics PerformanceMetrics { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public string BusinessContext { get; set; } = string.Empty;
}

/// <summary>
/// Validação executável
/// </summary>
public class ExecutableValidation
{
    public Guid ValidationId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string SQL { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string ValidationType { get; set; } = string.Empty;
    public string ExecutionStatus { get; set; } = string.Empty; // "SUCCESS", "ERROR"
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionDuration { get; set; }
    public int ResultCount { get; set; }
    public ValidationResult ValidationResult { get; set; } = new();
    public DateTime ExecutedAt { get; set; }
}

/// <summary>
/// Resultado de uma validação
/// </summary>
public class ValidationResult
{
    public string Status { get; set; } = string.Empty; // "PASS", "ISSUES_FOUND", "CRITICAL", "ERROR", "NO_DATA"
    public string Message { get; set; } = string.Empty;
    public long IssuesDetected { get; set; }
    public long TotalRecords { get; set; }
    public double QualityPercentage { get; set; }
    public List<dynamic> RawResults { get; set; } = new();
}

/// <summary>
/// Sumário da execução de validações
/// </summary>
public class ValidationExecutionSummary
{
    public string FocusTable { get; set; } = string.Empty;
    public int TotalValidationsGenerated { get; set; }
    public int TotalValidationsExecuted { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public long TotalIssuesDetected { get; set; }
    public double AverageQualityScore { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public List<ExecutableValidation> ExecutedValidations { get; set; } = new();
    public List<ExecutableValidation> HighPriorityIssues { get; set; } = new();
    public List<ExecutableValidation> MediumPriorityIssues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public PipelinePerformanceMetrics PerformanceMetrics { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Métricas de performance do pipeline
/// </summary>
public class PipelinePerformanceMetrics
{
    public TimeSpan DiscoveryDuration { get; set; }
    public TimeSpan ContextCollectionDuration { get; set; }
    public TimeSpan AIGenerationDuration { get; set; }
    public TimeSpan TranslationDuration { get; set; }
    public TimeSpan TotalDuration { get; set; }

    public string PerformanceRating => TotalDuration.TotalSeconds switch
    {
        < 10 => "EXCELLENT",
        < 30 => "GOOD",
        < 60 => "FAIR",
        _ => "SLOW"
    };
}