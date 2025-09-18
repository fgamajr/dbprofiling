using DbConnect.Core.Models;
using DbConnect.Web.Data;
using DbConnect.Web.Services.Enhanced;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DbConnect.Web.Endpoints.Enhanced;

/// <summary>
/// Enhanced Data Quality Controller
/// API principal para o sistema de data profiling com IA enhanced
/// Integra: Discovery + Context + AI + Translation + Execution + Visualization
/// </summary>
[ApiController]
[Route("api/enhanced-data-quality")]
public class EnhancedDataQualityController : ControllerBase
{
    private readonly IHybridSQLGenerator _hybridGenerator;
    private readonly IIntelligentVisualizationEngine _visualizationEngine;
    private readonly AppDbContext _db;
    private readonly ILogger<EnhancedDataQualityController> _logger;

    public EnhancedDataQualityController(
        IHybridSQLGenerator hybridGenerator,
        IIntelligentVisualizationEngine visualizationEngine,
        AppDbContext db,
        ILogger<EnhancedDataQualityController> logger)
    {
        _hybridGenerator = hybridGenerator;
        _visualizationEngine = visualizationEngine;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Executar análise completa com IA enhanced
    /// Endpoint principal que faz tudo: discovery, AI, execution, visualization
    /// </summary>
    [HttpPost("analyze-complete")]
    [Authorize]
    public async Task<IActionResult> AnalyzeCompleteAsync([FromBody] EnhancedAnalysisRequest request)
    {
        try
        {
            _logger.LogInformation("🚀 Iniciando análise enhanced completa para tabela: {TableName}", request.TableName);

            // Verificar usuário e perfil
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            var profile = await GetActiveProfileAsync(userId.Value);
            if (profile == null)
            {
                return BadRequest(new { message = "Nenhum perfil ativo encontrado." });
            }

            var connectionString = BuildConnectionString(profile);

            // Executar pipeline completo
            var summary = await _hybridGenerator.ExecuteAndSummarizeAsync(
                connectionString,
                request.TableName,
                request.BusinessContext,
                request.ApiKey
            );

            // Gerar dashboard inteligente
            var dashboard = await _visualizationEngine.GenerateDashboardAsync(summary);

            var response = new
            {
                success = true,
                message = "Análise enhanced concluída com sucesso",
                analysis = new
                {
                    focusTable = summary.FocusTable,
                    executionTime = summary.TotalExecutionTime.TotalSeconds,
                    validationsExecuted = summary.TotalValidationsExecuted,
                    issuesDetected = summary.TotalIssuesDetected,
                    averageQuality = Math.Round(summary.AverageQualityScore, 1),
                    performanceRating = summary.PerformanceMetrics.PerformanceRating
                },
                summary = new
                {
                    totalValidations = summary.TotalValidationsExecuted,
                    successfulExecutions = summary.SuccessfulExecutions,
                    failedExecutions = summary.FailedExecutions,
                    totalIssues = summary.TotalIssuesDetected,
                    highPriorityIssues = summary.HighPriorityIssues.Count,
                    mediumPriorityIssues = summary.MediumPriorityIssues.Count,
                    recommendations = summary.Recommendations
                },
                validations = summary.ExecutedValidations.Select(v => new
                {
                    id = v.ValidationId,
                    description = v.Description,
                    priority = v.Priority,
                    type = v.ValidationType,
                    status = v.ValidationResult.Status,
                    issuesDetected = v.ValidationResult.IssuesDetected,
                    totalRecords = v.ValidationResult.TotalRecords,
                    qualityPercentage = Math.Round(v.ValidationResult.QualityPercentage, 1),
                    executionDuration = v.ExecutionDuration.TotalMilliseconds,
                    sql = request.IncludeSQL ? v.SQL : null
                }),
                dashboard = new
                {
                    id = dashboard.Id,
                    title = dashboard.Title,
                    description = dashboard.Description,
                    layout = dashboard.Layout,
                    visualizations = dashboard.Visualizations.Select(viz => new
                    {
                        id = viz.Id,
                        title = viz.Title,
                        chartType = viz.ChartType.ToString(),
                        data = viz.Data,
                        configuration = viz.Configuration,
                        priority = viz.Priority
                    }),
                    insights = dashboard.Insights
                },
                performance = new
                {
                    discoveryDuration = summary.PerformanceMetrics.DiscoveryDuration.TotalMilliseconds,
                    contextCollectionDuration = summary.PerformanceMetrics.ContextCollectionDuration.TotalMilliseconds,
                    aiGenerationDuration = summary.PerformanceMetrics.AIGenerationDuration.TotalMilliseconds,
                    translationDuration = summary.PerformanceMetrics.TranslationDuration.TotalMilliseconds,
                    totalDuration = summary.PerformanceMetrics.TotalDuration.TotalMilliseconds,
                    rating = summary.PerformanceMetrics.PerformanceRating
                }
            };

            _logger.LogInformation("✅ Análise enhanced concluída: {Issues} problemas detectados em {Duration}ms",
                summary.TotalIssuesDetected, summary.TotalExecutionTime.TotalMilliseconds);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("⚠️ Erro de validação: {Message}", ex.Message);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro durante análise enhanced");
            return StatusCode(500, new
            {
                success = false,
                message = "Erro interno durante análise enhanced",
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Executar apenas a descoberta automática do schema
    /// Endpoint para testar o discovery engine
    /// </summary>
    [HttpPost("discover-schema")]
    [Authorize]
    public async Task<IActionResult> DiscoverSchemaAsync()
    {
        try
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            var profile = await GetActiveProfileAsync(userId.Value);
            if (profile == null)
            {
                return BadRequest(new { message = "Nenhum perfil ativo encontrado." });
            }

            var connectionString = BuildConnectionString(profile);

            // Usar o MCPSchemaDiscoveryEngine diretamente
            var schemaDiscovery = HttpContext.RequestServices.GetRequiredService<IMCPSchemaDiscoveryEngine>();
            var schema = await schemaDiscovery.DiscoverCompleteSchemaAsync(connectionString);

            _logger.LogInformation("🔍 Schema discovered: {TableCount} tables, testing first table breakdown", schema.Tables.Count);
            if (schema.Tables.Any())
            {
                var firstTable = schema.Tables.First();
                _logger.LogInformation("🧮 First table quality: {Score}, breakdown summary: {Summary}",
                    firstTable.DataQualityScore, firstTable.QualityBreakdown.Summary);
            }

            var response = new
            {
                success = true,
                message = "Schema descoberto com sucesso",
                discovery = new
                {
                    databaseName = schema.DatabaseName,
                    discoveredAt = schema.DiscoveredAt,
                    metrics = schema.DiscoveryMetrics
                },
                schema = new
                {
                    tables = schema.Tables.Select(t => new
                    {
                        fullName = t.FullName,
                        schema = t.SchemaName,
                        name = t.TableName,
                        type = t.TableType,
                        columnCount = t.ColumnCount,
                        estimatedRows = t.EstimatedRowCount,
                        tableSize = t.TableSize,
                        hasPrimaryKey = t.HasPrimaryKey,
                        dataQualityScore = Math.Round(t.DataQualityScore, 1),
                        qualityBreakdown = new
                        {
                            summary = t.QualityBreakdown.Summary,
                            detailedTooltip = t.QualityBreakdown.DetailedTooltip,
                            hasPrimaryKey = t.QualityBreakdown.HasPrimaryKey,
                            primaryKeyScore = t.QualityBreakdown.PrimaryKeyScore,
                            nullFraction = Math.Round(t.QualityBreakdown.NullFraction * 100, 1),
                            nullScore = t.QualityBreakdown.NullScore,
                            columnsWithStats = t.QualityBreakdown.ColumnsWithStats,
                            totalColumns = t.QualityBreakdown.TotalColumns,
                            statisticsScore = t.QualityBreakdown.StatisticsScore,
                            foreignKeyCount = t.QualityBreakdown.ForeignKeyCount,
                            foreignKeyScore = t.QualityBreakdown.ForeignKeyScore,
                            appropriateTypeCount = t.QualityBreakdown.AppropriateTypeCount,
                            dataTypeScore = t.QualityBreakdown.DataTypeScore,
                            totalScore = t.QualityBreakdown.TotalScore
                        },
                        columns = t.Columns.Select(c => new
                        {
                            name = c.ColumnName,
                            dataType = c.DataType,
                            isNullable = c.IsNullable,
                            isPrimaryKey = c.IsPrimaryKey,
                            isForeignKey = c.IsForeignKey,
                            foreignTable = c.ForeignTableFullName,
                            classification = c.DataClassification,
                            distinctValues = c.DistinctValues,
                            nullFraction = Math.Round(c.NullFraction * 100, 1)
                        })
                    }),
                    foreignKeys = schema.ForeignKeys.Select(fk => new
                    {
                        source = new { table = fk.SourceFullName, column = fk.SourceColumn },
                        target = new { table = fk.TargetFullName, column = fk.TargetColumn },
                        constraintName = fk.ConstraintName
                    }),
                    implicitRelations = schema.ImplicitRelations.Select(ir => new
                    {
                        source = new { table = ir.SourceTable, column = ir.SourceColumn },
                        target = new { table = ir.TargetTable, column = ir.TargetColumn },
                        confidence = Math.Round(ir.ConfidenceScore * 100, 1),
                        method = ir.DetectionMethod,
                        evidence = ir.Evidence
                    }),
                    relevantRelations = schema.RelevantRelations.Select(rr => new
                    {
                        source = rr.SourceTable,
                        target = rr.TargetTable,
                        joinCondition = rr.JoinCondition,
                        importance = rr.ImportanceScore,
                        type = rr.RelationType,
                        confidence = Math.Round(rr.ConfidenceLevel * 100, 1),
                        validationOpportunities = rr.ValidationOpportunities
                    })
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro durante descoberta de schema");
            return StatusCode(500, new
            {
                success = false,
                message = "Erro durante descoberta de schema",
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Gerar apenas validações IA (sem executar)
    /// Endpoint para testar a geração de validações
    /// </summary>
    [HttpPost("generate-validations")]
    [Authorize]
    public async Task<IActionResult> GenerateValidationsAsync([FromBody] ValidationGenerationRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            var profile = await GetActiveProfileAsync(userId.Value);
            if (profile == null)
            {
                return BadRequest(new { message = "Nenhum perfil ativo encontrado." });
            }

            var connectionString = BuildConnectionString(profile);

            // Gerar apenas as validações (sem executar)
            var hybridResult = await _hybridGenerator.GenerateExecutableValidationsAsync(
                connectionString,
                request.TableName,
                request.BusinessContext,
                request.ApiKey
            );

            var response = new
            {
                success = true,
                message = "Validações geradas com sucesso",
                generation = new
                {
                    focusTable = hybridResult.FocusTable,
                    contextComplexity = hybridResult.RichContext.ContextMetrics.ContextComplexity,
                    relatedTables = hybridResult.RichContext.RelatedTables.Count,
                    sampleSize = hybridResult.RichContext.CrossTableSample.TotalSampleSize,
                    validationsGenerated = hybridResult.TranslatedValidations.Count,
                    successfulTranslations = hybridResult.TranslatedValidations.Count(t => t.IsValidSQL)
                },
                validations = hybridResult.TranslatedValidations.Select(tv => new
                {
                    id = tv.OriginalValidation.Id,
                    number = tv.OriginalValidation.Number,
                    description = tv.OriginalValidation.Description,
                    type = tv.OriginalValidation.ValidationType,
                    priority = tv.OriginalValidation.Priority,
                    complexity = tv.OriginalValidation.Complexity,
                    involvedTables = tv.OriginalValidation.InvolvedTables,
                    relevanceScore = Math.Round(tv.OriginalValidation.RelevanceScore, 1),
                    isValidSQL = tv.IsValidSQL,
                    translationMethod = tv.TranslationMethod,
                    sql = request.IncludeSQL ? tv.TranslatedSQL : null
                }),
                insights = hybridResult.AIValidations.ValidationInsights,
                performance = new
                {
                    discoveryDuration = hybridResult.PerformanceMetrics.DiscoveryDuration.TotalMilliseconds,
                    contextCollectionDuration = hybridResult.PerformanceMetrics.ContextCollectionDuration.TotalMilliseconds,
                    aiGenerationDuration = hybridResult.PerformanceMetrics.AIGenerationDuration.TotalMilliseconds,
                    translationDuration = hybridResult.PerformanceMetrics.TranslationDuration.TotalMilliseconds,
                    totalDuration = hybridResult.PerformanceMetrics.TotalDuration.TotalMilliseconds
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro durante geração de validações");
            return StatusCode(500, new
            {
                success = false,
                message = "Erro durante geração de validações",
                details = ex.Message
            });
        }
    }

    /// <summary>
    /// Obter status do sistema enhanced
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public IActionResult GetEnhancedStatus()
    {
        return Ok(new
        {
            success = true,
            status = "operational",
            version = "1.0.0-enhanced",
            capabilities = new[]
            {
                "automatic_schema_discovery",
                "implicit_relationship_detection",
                "cross_table_context_collection",
                "ai_powered_validation_generation",
                "natural_to_sql_translation",
                "hybrid_execution_pipeline",
                "intelligent_visualization",
                "performance_monitoring"
            },
            performance = new
            {
                avgDiscoveryTime = "1-3 seconds",
                avgContextCollectionTime = "2-5 seconds",
                avgAIGenerationTime = "5-15 seconds",
                avgTranslationTime = "3-10 seconds",
                totalPipelineTime = "15-45 seconds"
            }
        });
    }

    private int? GetUserId()
    {
        var userIdClaim = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                          HttpContext.User.FindFirstValue("sub");
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private async Task<ConnectionProfile?> GetActiveProfileAsync(int userId)
    {
        var activeProfileId = HttpContext.Session.GetInt32("ActiveProfileId");
        if (!activeProfileId.HasValue) return null;

        return await _db.Profiles.FirstOrDefaultAsync(p => p.Id == activeProfileId && p.UserId == userId);
    }

    private static string BuildConnectionString(ConnectionProfile profile)
    {
        return profile.Kind switch
        {
            DbKind.PostgreSql => $"Host={profile.HostOrFile};Port={profile.Port};Database={profile.Database};Username={profile.Username};Password={profile.Password};",
            _ => throw new NotSupportedException($"Enhanced analysis não suporta {profile.Kind}")
        };
    }
}

/// <summary>
/// Request para análise enhanced completa
/// </summary>
public class EnhancedAnalysisRequest
{
    public string TableName { get; set; } = string.Empty;
    public string? BusinessContext { get; set; }
    public string? ApiKey { get; set; }
    public bool IncludeSQL { get; set; } = false;
}

/// <summary>
/// Request para geração de validações
/// </summary>
public class ValidationGenerationRequest
{
    public string TableName { get; set; } = string.Empty;
    public string? BusinessContext { get; set; }
    public string? ApiKey { get; set; }
    public bool IncludeSQL { get; set; } = true;
}