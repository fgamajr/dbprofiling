using DbConnect.Web.Services.Enhanced;
using System.Text.Json;

namespace DbConnect.Web.Services.Enhanced;

/// <summary>
/// Intelligent Visualization Engine
/// Gera visualizações automáticas baseadas nos resultados das validações
/// Analisa tipos de dados e sugere os gráficos mais apropriados
/// </summary>
public interface IIntelligentVisualizationEngine
{
    Task<List<VisualizationConfiguration>> GenerateVisualizationsAsync(ValidationExecutionSummary summary);
    Task<VisualizationConfiguration> GenerateSingleVisualizationAsync(ExecutableValidation validation);
    Task<DashboardConfiguration> GenerateDashboardAsync(ValidationExecutionSummary summary);
}

public class IntelligentVisualizationEngine : IIntelligentVisualizationEngine
{
    private readonly ILogger<IntelligentVisualizationEngine> _logger;

    public IntelligentVisualizationEngine(ILogger<IntelligentVisualizationEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gerar visualizações para todas as validações
    /// </summary>
    public async Task<List<VisualizationConfiguration>> GenerateVisualizationsAsync(ValidationExecutionSummary summary)
    {
        _logger.LogInformation("📊 Gerando visualizações para {Count} validações executadas", summary.ExecutedValidations.Count);

        var visualizations = new List<VisualizationConfiguration>();

        // 1. Gráfico de overview (sempre primeiro)
        var overviewViz = GenerateOverviewVisualization(summary);
        visualizations.Add(overviewViz);

        // 2. Visualizações individuais para cada validação
        foreach (var validation in summary.ExecutedValidations.Where(v => v.ExecutionStatus == "SUCCESS"))
        {
            var viz = await GenerateSingleVisualizationAsync(validation);
            if (viz != null)
            {
                visualizations.Add(viz);
            }
        }

        // 3. Visualizações agregadas por tipo
        var typeAggregations = GenerateTypeAggregationVisualizations(summary);
        visualizations.AddRange(typeAggregations);

        // 4. Visualização de relacionamentos (se existirem validações cruzadas)
        var relationshipViz = GenerateRelationshipVisualization(summary);
        if (relationshipViz != null)
        {
            visualizations.Add(relationshipViz);
        }

        _logger.LogInformation("✅ Geradas {Count} visualizações automáticas", visualizations.Count);
        return visualizations;
    }

    /// <summary>
    /// Gerar visualização para uma validação individual
    /// </summary>
    public Task<VisualizationConfiguration> GenerateSingleVisualizationAsync(ExecutableValidation validation)
    {
        var chartType = DetermineChartType(validation);
        var data = ProcessValidationData(validation);

        var visualization = new VisualizationConfiguration
        {
            Id = Guid.NewGuid(),
            Title = validation.Description,
            ChartType = chartType,
            Data = data,
            Configuration = GenerateChartConfiguration(chartType, validation),
            Priority = validation.Priority,
            ValidationType = validation.ValidationType,
            CreatedAt = DateTime.UtcNow
        };

        return Task.FromResult(visualization);
    }

    /// <summary>
    /// Gerar dashboard completo
    /// </summary>
    public async Task<DashboardConfiguration> GenerateDashboardAsync(ValidationExecutionSummary summary)
    {
        _logger.LogInformation("🎯 Gerando dashboard inteligente para {FocusTable}", summary.FocusTable);

        var visualizations = await GenerateVisualizationsAsync(summary);

        var dashboard = new DashboardConfiguration
        {
            Id = Guid.NewGuid(),
            Title = $"Data Quality Dashboard - {summary.FocusTable}",
            Description = GenerateDashboardDescription(summary),
            FocusTable = summary.FocusTable,
            Layout = GenerateOptimalLayout(visualizations),
            Visualizations = visualizations,
            Summary = summary,
            Insights = GenerateDashboardInsights(summary),
            CreatedAt = DateTime.UtcNow
        };

        return dashboard;
    }

    /// <summary>
    /// Determinar tipo de gráfico baseado nos dados da validação
    /// </summary>
    private ChartType DetermineChartType(ExecutableValidation validation)
    {
        // Analisar os resultados para decidir o melhor gráfico
        if (!validation.ValidationResult.RawResults.Any())
        {
            return ChartType.InfoCard;
        }

        var firstResult = (IDictionary<string, object>)validation.ValidationResult.RawResults.First();
        var columnNames = firstResult.Keys.ToList();

        // Regras inteligentes para seleção de gráfico

        // 1. Se tem percentual ou ratio → Gauge
        if (HasPercentageData(columnNames))
        {
            return ChartType.QualityGauge;
        }

        // 2. Se tem dados temporais → Timeline
        if (HasTemporalData(columnNames) || validation.ValidationType == "TEMPORAL_CONSISTENCY")
        {
            return ChartType.Timeline;
        }

        // 3. Se tem categorias ou status → Pie Chart
        if (HasCategoricalData(columnNames) || validation.ValidationType == "STATUS_CONSISTENCY")
        {
            return ChartType.PieChart;
        }

        // 4. Se tem distribuição numérica → Histogram
        if (HasNumericDistribution(validation.ValidationResult.RawResults))
        {
            return ChartType.Histogram;
        }

        // 5. Se é validação de relacionamento → Network
        if (validation.ValidationType == "REFERENTIAL_INTEGRITY" || validation.Description.ToLower().Contains("relacionamento"))
        {
            return ChartType.NetworkGraph;
        }

        // 6. Se tem múltiplas métricas → Bar Chart
        if (columnNames.Count > 2)
        {
            return ChartType.BarChart;
        }

        // 7. Default → Info Card
        return ChartType.InfoCard;
    }

    /// <summary>
    /// Processar dados da validação para visualização
    /// </summary>
    private object ProcessValidationData(ExecutableValidation validation)
    {
        var result = validation.ValidationResult;

        // Dados padrão para qualquer visualização
        var baseData = new
        {
            totalRecords = result.TotalRecords,
            issuesDetected = result.IssuesDetected,
            qualityPercentage = result.QualityPercentage,
            status = result.Status,
            validRecords = result.TotalRecords - result.IssuesDetected
        };

        // Processar dados específicos baseado no tipo de gráfico
        var chartType = DetermineChartType(validation);

        return chartType switch
        {
            ChartType.QualityGauge => ProcessGaugeData(baseData, result),
            ChartType.PieChart => ProcessPieChartData(baseData, result),
            ChartType.BarChart => ProcessBarChartData(result),
            ChartType.Timeline => ProcessTimelineData(result),
            ChartType.Histogram => ProcessHistogramData(result),
            ChartType.NetworkGraph => ProcessNetworkData(result),
            _ => baseData
        };
    }

    /// <summary>
    /// Gerar configuração específica do gráfico
    /// </summary>
    private object GenerateChartConfiguration(ChartType chartType, ExecutableValidation validation)
    {
        var baseConfig = new
        {
            responsive = true,
            maintainAspectRatio = false,
            title = validation.Description,
            subtitle = $"Prioridade: {validation.Priority}/10 | Tipo: {validation.ValidationType}"
        };

        return chartType switch
        {
            ChartType.QualityGauge => new
            {
                baseConfig,
                gauge = new
                {
                    min = 0,
                    max = 100,
                    unit = "%",
                    thresholds = new[] {
                        new { value = 50, color = "#ef4444" },  // Vermelho < 50%
                        new { value = 80, color = "#f59e0b" },  // Amarelo 50-80%
                        new { value = 100, color = "#10b981" }  // Verde > 80%
                    }
                }
            },

            ChartType.PieChart => new
            {
                baseConfig,
                pie = new
                {
                    colors = new[] { "#10b981", "#ef4444", "#6b7280" }, // Verde, Vermelho, Cinza
                    showLabels = true,
                    showPercentages = true
                }
            },

            ChartType.BarChart => new
            {
                baseConfig,
                bar = new
                {
                    orientation = "vertical",
                    showValues = true,
                    color = "#3b82f6"
                }
            },

            ChartType.Timeline => new
            {
                baseConfig,
                timeline = new
                {
                    showPoints = true,
                    lineColor = "#8b5cf6",
                    fillArea = true
                }
            },

            _ => baseConfig
        };
    }

    /// <summary>
    /// Gerar visualização de overview
    /// </summary>
    private VisualizationConfiguration GenerateOverviewVisualization(ValidationExecutionSummary summary)
    {
        var data = new
        {
            totalValidations = summary.TotalValidationsExecuted,
            successfulValidations = summary.SuccessfulExecutions,
            failedValidations = summary.FailedExecutions,
            totalIssues = summary.TotalIssuesDetected,
            averageQuality = summary.AverageQualityScore,
            highPriorityIssues = summary.HighPriorityIssues.Count,
            executionTime = summary.TotalExecutionTime.TotalSeconds,

            // Dados para gráfico de rosca
            validationStatus = new[]
            {
                new { label = "Passou", value = summary.ExecutedValidations.Count(v => v.ValidationResult.Status == "PASS"), color = "#10b981" },
                new { label = "Problemas", value = summary.ExecutedValidations.Count(v => v.ValidationResult.Status == "ISSUES_FOUND"), color = "#f59e0b" },
                new { label = "Crítico", value = summary.ExecutedValidations.Count(v => v.ValidationResult.Status == "CRITICAL"), color = "#ef4444" },
                new { label = "Erro", value = summary.ExecutedValidations.Count(v => v.ValidationResult.Status == "ERROR"), color = "#6b7280" }
            }
        };

        return new VisualizationConfiguration
        {
            Id = Guid.NewGuid(),
            Title = $"Overview de Qualidade - {summary.FocusTable}",
            ChartType = ChartType.DashboardOverview,
            Data = data,
            Configuration = new
            {
                showMetrics = true,
                showDonutChart = true,
                highlightCritical = summary.HighPriorityIssues.Any()
            },
            Priority = 10, // Máxima prioridade
            ValidationType = "OVERVIEW",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gerar visualizações agregadas por tipo
    /// </summary>
    private List<VisualizationConfiguration> GenerateTypeAggregationVisualizations(ValidationExecutionSummary summary)
    {
        var visualizations = new List<VisualizationConfiguration>();

        // Agregar por tipo de validação
        var typeGroups = summary.ExecutedValidations
            .GroupBy(v => v.ValidationType)
            .Where(g => g.Count() > 1) // Só se tem mais de uma validação do tipo
            .ToList();

        foreach (var group in typeGroups)
        {
            var data = new
            {
                validationType = group.Key,
                validations = group.Select(v => new
                {
                    description = v.Description,
                    issues = v.ValidationResult.IssuesDetected,
                    quality = v.ValidationResult.QualityPercentage,
                    status = v.ValidationResult.Status
                }).ToList(),
                totalIssues = group.Sum(v => v.ValidationResult.IssuesDetected),
                averageQuality = group.Average(v => v.ValidationResult.QualityPercentage)
            };

            visualizations.Add(new VisualizationConfiguration
            {
                Id = Guid.NewGuid(),
                Title = $"Agregação: {GetValidationTypeDisplayName(group.Key)}",
                ChartType = ChartType.BarChart,
                Data = data,
                Configuration = new
                {
                    groupedBar = true,
                    showTotals = true
                },
                Priority = 6,
                ValidationType = group.Key,
                CreatedAt = DateTime.UtcNow
            });
        }

        return visualizations;
    }

    /// <summary>
    /// Gerar visualização de relacionamentos
    /// </summary>
    private VisualizationConfiguration? GenerateRelationshipVisualization(ValidationExecutionSummary summary)
    {
        var crossTableValidations = summary.ExecutedValidations
            .Where(v => v.ValidationType == "REFERENTIAL_INTEGRITY" || v.Description.ToLower().Contains("relacionamento"))
            .ToList();

        if (!crossTableValidations.Any()) return null;

        var data = new
        {
            nodes = ExtractTableNodes(crossTableValidations),
            edges = ExtractRelationshipEdges(crossTableValidations),
            metrics = crossTableValidations.Select(v => new
            {
                relationship = v.Description,
                quality = v.ValidationResult.QualityPercentage,
                issues = v.ValidationResult.IssuesDetected
            })
        };

        return new VisualizationConfiguration
        {
            Id = Guid.NewGuid(),
            Title = "Mapa de Relacionamentos e Qualidade",
            ChartType = ChartType.NetworkGraph,
            Data = data,
            Configuration = new
            {
                showNodeLabels = true,
                edgeWeightByQuality = true,
                highlightIssues = true
            },
            Priority = 8,
            ValidationType = "RELATIONSHIP_MAP",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gerar layout otimizado do dashboard
    /// </summary>
    private DashboardLayout GenerateOptimalLayout(List<VisualizationConfiguration> visualizations)
    {
        var layout = new DashboardLayout
        {
            Rows = new List<DashboardRow>()
        };

        // Linha 1: Overview (largura total)
        var overviewViz = visualizations.FirstOrDefault(v => v.ChartType == ChartType.DashboardOverview);
        if (overviewViz != null)
        {
            layout.Rows.Add(new DashboardRow
            {
                Height = "300px",
                Columns = new List<DashboardColumn>
                {
                    new() { Width = "100%", VisualizationId = overviewViz.Id }
                }
            });
        }

        // Linha 2: Gráficos importantes (2 colunas)
        var importantViz = visualizations
            .Where(v => v.Priority >= 8 && v.ChartType != ChartType.DashboardOverview)
            .Take(2)
            .ToList();

        if (importantViz.Any())
        {
            layout.Rows.Add(new DashboardRow
            {
                Height = "400px",
                Columns = importantViz.Select(v => new DashboardColumn
                {
                    Width = "50%",
                    VisualizationId = v.Id
                }).ToList()
            });
        }

        // Linha 3: Outros gráficos (3 colunas)
        var otherViz = visualizations
            .Where(v => v.Priority < 8 && v.ChartType != ChartType.DashboardOverview)
            .Skip(importantViz.Count)
            .Take(3)
            .ToList();

        if (otherViz.Any())
        {
            layout.Rows.Add(new DashboardRow
            {
                Height = "300px",
                Columns = otherViz.Select(v => new DashboardColumn
                {
                    Width = "33.33%",
                    VisualizationId = v.Id
                }).ToList()
            });
        }

        return layout;
    }

    // Métodos auxiliares para análise de dados

    private bool HasPercentageData(List<string> columnNames)
    {
        return columnNames.Any(name => name.ToLower().Contains("percentage") ||
                                     name.ToLower().Contains("percent") ||
                                     name.ToLower().Contains("ratio"));
    }

    private bool HasTemporalData(List<string> columnNames)
    {
        return columnNames.Any(name => name.ToLower().Contains("date") ||
                                     name.ToLower().Contains("time") ||
                                     name.ToLower().Contains("temporal"));
    }

    private bool HasCategoricalData(List<string> columnNames)
    {
        return columnNames.Any(name => name.ToLower().Contains("status") ||
                                     name.ToLower().Contains("category") ||
                                     name.ToLower().Contains("type"));
    }

    private bool HasNumericDistribution(List<dynamic> results)
    {
        return results.Count > 5; // Se tem muitos resultados, pode ser distribuição
    }

    private object ProcessGaugeData(object baseData, ValidationResult result)
    {
        return new
        {
            value = Math.Round(result.QualityPercentage, 1),
            max = 100,
            label = "Qualidade (%)",
            color = result.QualityPercentage switch
            {
                >= 90 => "#10b981", // Verde
                >= 70 => "#f59e0b", // Amarelo
                _ => "#ef4444"      // Vermelho
            }
        };
    }

    private object ProcessPieChartData(object baseData, ValidationResult result)
    {
        return new
        {
            data = new[]
            {
                new { label = "Válidos", value = result.TotalRecords - result.IssuesDetected, color = "#10b981" },
                new { label = "Problemas", value = result.IssuesDetected, color = "#ef4444" }
            }
        };
    }

    private object ProcessBarChartData(ValidationResult result)
    {
        // Processar resultados raw para bar chart
        return new { data = result.RawResults.Take(10) }; // Limitar para não poluir
    }

    private object ProcessTimelineData(ValidationResult result)
    {
        // Implementar processamento de dados temporais
        return new { data = result.RawResults };
    }

    private object ProcessHistogramData(ValidationResult result)
    {
        // Implementar processamento de distribuição
        return new { data = result.RawResults };
    }

    private object ProcessNetworkData(ValidationResult result)
    {
        // Implementar processamento de dados de rede
        return new { nodes = new List<object>(), edges = new List<object>() };
    }

    private List<object> ExtractTableNodes(List<ExecutableValidation> validations)
    {
        // Extrair nós (tabelas) dos relacionamentos
        return new List<object>();
    }

    private List<object> ExtractRelationshipEdges(List<ExecutableValidation> validations)
    {
        // Extrair arestas (relacionamentos) entre tabelas
        return new List<object>();
    }

    private string GetValidationTypeDisplayName(string validationType)
    {
        return validationType switch
        {
            "REFERENTIAL_INTEGRITY" => "Integridade Referencial",
            "TEMPORAL_CONSISTENCY" => "Consistência Temporal",
            "STATUS_CONSISTENCY" => "Consistência de Status",
            "UNIQUENESS" => "Unicidade",
            "FORMAT_VALIDATION" => "Validação de Formato",
            "ANOMALY_DETECTION" => "Detecção de Anomalias",
            "BUSINESS_RULE" => "Regras de Negócio",
            _ => validationType
        };
    }

    private string GenerateDashboardDescription(ValidationExecutionSummary summary)
    {
        return $"Dashboard gerado automaticamente com {summary.TotalValidationsExecuted} validações executadas. " +
               $"Qualidade média: {summary.AverageQualityScore:F1}%. " +
               (summary.TotalIssuesDetected > 0
                   ? $"{summary.TotalIssuesDetected} problemas detectados."
                   : "Nenhum problema detectado.");
    }

    private List<string> GenerateDashboardInsights(ValidationExecutionSummary summary)
    {
        var insights = new List<string>();

        if (summary.TotalIssuesDetected == 0)
        {
            insights.Add("✅ Excelente! Todas as validações passaram sem problemas");
        }
        else
        {
            insights.Add($"📊 {summary.TotalIssuesDetected} problemas de qualidade detectados");
        }

        if (summary.AverageQualityScore >= 90)
        {
            insights.Add("🏆 Qualidade excepcional dos dados (>90%)");
        }
        else if (summary.AverageQualityScore < 70)
        {
            insights.Add("⚠️ Qualidade dos dados precisa de atenção (<70%)");
        }

        if (summary.HighPriorityIssues.Any())
        {
            insights.Add($"🚨 {summary.HighPriorityIssues.Count} problemas de alta prioridade requerem ação imediata");
        }

        var executionTime = summary.TotalExecutionTime.TotalSeconds;
        if (executionTime < 10)
        {
            insights.Add("⚡ Análise executada rapidamente (<10s)");
        }
        else if (executionTime > 60)
        {
            insights.Add("🐌 Análise demorou mais que o esperado (>60s)");
        }

        return insights;
    }
}

/// <summary>
/// Tipos de gráficos suportados
/// </summary>
public enum ChartType
{
    // Dados temporais
    Timeline,           // Para datas, períodos, tendências

    // Dados categóricos
    PieChart,          // Para boolean, status, categorias
    BarChart,          // Para contagens por categoria

    // Dados numéricos
    Histogram,         // Para distribuição de valores
    BoxPlot,           // Para outliers e quartis
    ScatterPlot,       // Para correlações

    // Dados relacionais
    NetworkGraph,      // Para relacionamentos entre tabelas
    SankeyDiagram,     // Para fluxos de dados

    // Dados de qualidade
    QualityGauge,      // Para percentuais de qualidade
    HeatMap,           // Para padrões de inconsistência

    // Layouts especiais
    DashboardOverview, // Para visão geral do dashboard
    InfoCard           // Para informações textuais
}

/// <summary>
/// Configuração de visualização
/// </summary>
public class VisualizationConfiguration
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ChartType ChartType { get; set; }
    public object Data { get; set; } = new();
    public object Configuration { get; set; } = new();
    public int Priority { get; set; }
    public string ValidationType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Configuração do dashboard
/// </summary>
public class DashboardConfiguration
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FocusTable { get; set; } = string.Empty;
    public DashboardLayout Layout { get; set; } = new();
    public List<VisualizationConfiguration> Visualizations { get; set; } = new();
    public ValidationExecutionSummary Summary { get; set; } = new();
    public List<string> Insights { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Layout do dashboard
/// </summary>
public class DashboardLayout
{
    public List<DashboardRow> Rows { get; set; } = new();
}

public class DashboardRow
{
    public string Height { get; set; } = "300px";
    public List<DashboardColumn> Columns { get; set; } = new();
}

public class DashboardColumn
{
    public string Width { get; set; } = "100%";
    public Guid VisualizationId { get; set; }
}