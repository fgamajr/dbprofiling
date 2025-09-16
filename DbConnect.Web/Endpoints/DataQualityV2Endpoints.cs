using DbConnect.Core.Models;
using DbConnect.Web.Data;
using DbConnect.Web.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DbConnect.Web.AI;

namespace DbConnect.Web.Endpoints;

public static class DataQualityV2Endpoints
{
    public static void MapDataQualityV2Endpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dq/v2")
            .WithTags("Data Quality V2")
            .RequireAuthorization();

        // Passo 1: Gerar plano de pré-voo (LLM)
        group.MapPost("/preflight", GeneratePreflightPlan)
            .WithSummary("Gera plano de pré-voo usando LLM")
            .WithDescription("A LLM gera testes de conectividade, queries de sanidade e regras candidatas sem executar nada");

        // Passo 2: Executar testes de pré-voo
        group.MapPost("/preflight/execute", ExecutePreflightPlan)
            .WithSummary("Executa os testes de pré-voo gerados")
            .WithDescription("Executa conectividade, introspecção e queries de sanidade");

        // Passo 3: Coletar métricas padronizadas (sem IA)
        group.MapPost("/metrics/collect", CollectStandardMetrics)
            .WithSummary("Coleta métricas padronizadas da tabela")
            .WithDescription("Executa análise de volume, nulos, distintos, duplicados, etc. sem usar IA");

        // Passo 4: Gerar/atualizar regras IA baseado em métricas
        group.MapPost("/rules/generate", GenerateAIRules)
            .WithSummary("Gera regras de Data Quality usando IA")
            .WithDescription("Usa métricas coletadas para gerar regras mais inteligentes");

        // Passo 5: Executar regras selecionadas
        group.MapPost("/rules/execute", ExecuteSelectedRules)
            .WithSummary("Executa regras selecionadas")
            .WithDescription("Roda as regras aprovadas pelo usuário e persiste resultados");

        // Dashboard integrado
        group.MapGet("/dashboard", GetDashboard)
            .WithSummary("Dashboard com métricas + regras")
            .WithDescription("Retorna métricas padronizadas, regras candidatas e execuções recentes");

        // Histórico e comparações
        group.MapGet("/history/preflight", GetPreflightHistory)
            .WithSummary("Histórico de testes de pré-voo");

        group.MapGet("/history/metrics", GetMetricsHistory)
            .WithSummary("Histórico de métricas coletadas");
    }

    private static async Task<IResult> GeneratePreflightPlan(
        PreflightRequest request,
        HttpContext context,
        PreflightService preflightService)
    {
        try
        {
            var userId = GetUserId(context);
            var preflightPlan = await preflightService.GeneratePreflightPlanAsync(
                userId, request.ProfileId, request.SchemaName, request.TableNames);

            return Results.Ok(new
            {
                success = true,
                data = preflightPlan,
                message = $"Plano de pré-voo gerado: {preflightPlan.PreflightTests.Count} testes, {preflightPlan.SanityQueries.Count} queries, {preflightPlan.RuleCandidates.Count} regras candidatas"
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, error = ex.Message });
        }
    }

    private static async Task<IResult> ExecutePreflightPlan(
        ExecutePreflightRequest request,
        HttpContext context,
        AppDbContext dbContext,
        PreflightService preflightService)
    {
        try
        {
            var userId = GetUserId(context);
            var profile = await dbContext.Profiles.FindAsync(request.ProfileId);

            if (profile == null)
                return Results.NotFound("Profile não encontrado");

            var results = await preflightService.ExecutePreflightTestsAsync(profile, request.PreflightPlan);

            var success = results.All(r => r.Success);
            var failedTests = results.Where(r => !r.Success).Select(r => r.TestName).ToList();

            return Results.Ok(new
            {
                success,
                data = new
                {
                    total_tests = results.Count,
                    passed_tests = results.Count(r => r.Success),
                    failed_tests = failedTests.Count,
                    failed_test_names = failedTests,
                    results
                },
                message = success ? "Todos os testes passaram" : $"{failedTests.Count} testes falharam: {string.Join(", ", failedTests)}"
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, error = ex.Message });
        }
    }

    private static async Task<IResult> CollectStandardMetrics(
        CollectMetricsRequest request,
        HttpContext context,
        AppDbContext dbContext,
        StandardMetricsService metricsService)
    {
        try
        {
            var userId = GetUserId(context);
            var profile = await dbContext.Profiles.FindAsync(request.ProfileId);

            if (profile == null)
                return Results.NotFound("Profile não encontrado");

            // Coletar métricas da tabela
            var tableMetrics = await metricsService.CollectTableMetricsAsync(
                profile, request.SchemaName, request.TableName);

            // Coletar métricas das colunas
            var columnMetrics = await metricsService.CollectColumnMetricsAsync(
                profile, request.SchemaName, request.TableName);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    table_metrics = tableMetrics,
                    column_metrics = columnMetrics,
                    collected_at = tableMetrics.CollectedAt
                },
                message = $"Coletadas métricas da tabela {request.SchemaName}.{request.TableName}: {columnMetrics.Count} colunas analisadas"
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, error = ex.Message });
        }
    }

    private static async Task<IResult> GenerateAIRules(
        GenerateRulesRequest request,
        HttpContext context,
        PreflightService preflightService,
        StandardMetricsService metricsService)
    {
        try
        {
            var userId = GetUserId(context);

            // Obter métricas mais recentes para enriquecer o prompt
            var dashboardData = await metricsService.GetDashboardDataAsync(request.SchemaName, request.TableName);

            // Gerar regras baseadas nas métricas
            var preflightPlan = await preflightService.GeneratePreflightPlanAsync(
                userId, request.ProfileId, request.SchemaName, new List<string> { request.TableName });

            // Persistir regras candidatas
            var persistedRules = await preflightService.PersistRuleCandidatesAsync(
                request.SchemaName, preflightPlan.RuleCandidates);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    rules_generated = persistedRules.Count,
                    rules = persistedRules,
                    metrics_used = new
                    {
                        table_row_count = dashboardData.TableMetrics.RowCount,
                        columns_analyzed = dashboardData.ColumnMetrics.Count
                    }
                },
                message = $"Geradas {persistedRules.Count} regras usando IA para {request.SchemaName}.{request.TableName}"
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, error = ex.Message });
        }
    }

    private static async Task<IResult> ExecuteSelectedRules(
        ExecuteRulesRequest request,
        HttpContext context,
        AppDbContext dbContext,
        DataQualityService dataQualityService)
    {
        try
        {
            var userId = GetUserId(context);
            var profile = await dbContext.Profiles.FindAsync(request.ProfileId);

            if (profile == null)
                return Results.NotFound("Profile não encontrado");

            var results = new List<RuleExecution>();

            foreach (var ruleId in request.RuleIds)
            {
                var rule = await dbContext.RuleCandidates.FindAsync(ruleId);
                if (rule == null) continue;

                var execution = await ExecuteSingleRuleAsync(profile, rule, dataQualityService);
                results.Add(execution);
            }

            // Salvar execuções
            dbContext.RuleExecutions.AddRange(results);
            await dbContext.SaveChangesAsync();

            var successCount = results.Count(r => r.Success);
            var totalIssues = results.Sum(r => r.InvalidRecords);

            return Results.Ok(new
            {
                success = true,
                data = new
                {
                    total_rules_executed = results.Count,
                    successful_executions = successCount,
                    failed_executions = results.Count - successCount,
                    total_quality_issues = totalIssues,
                    executions = results.Select(r => new
                    {
                        rule_name = r.RuleCandidate?.RuleName,
                        success = r.Success,
                        total_records = r.TotalRecords,
                        valid_records = r.ValidRecords,
                        invalid_records = r.InvalidRecords,
                        error_message = r.ErrorMessage,
                        execution_time_ms = r.ExecutionTimeMs
                    })
                },
                message = $"Executadas {results.Count} regras. {successCount} sucessos, {totalIssues} problemas de qualidade encontrados"
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, error = ex.Message });
        }
    }

    private static async Task<IResult> GetDashboard(
        string schemaName,
        string tableName,
        StandardMetricsService metricsService)
    {
        try
        {
            var dashboardData = await metricsService.GetDashboardDataAsync(schemaName, tableName);

            return Results.Ok(new
            {
                success = true,
                data = dashboardData,
                message = "Dashboard carregado"
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, error = ex.Message });
        }
    }

    private static async Task<IResult> GetPreflightHistory(
        string schemaName,
        string? tableName,
        PreflightService preflightService)
    {
        try
        {
            var history = await preflightService.GetPreflightHistoryAsync(schemaName, tableName);

            return Results.Ok(new
            {
                success = true,
                data = history,
                message = $"{history.Count} registros encontrados"
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, error = ex.Message });
        }
    }

    private static async Task<IResult> GetMetricsHistory(
        string schemaName,
        string tableName,
        AppDbContext dbContext)
    {
        try
        {
            var tableHistory = await dbContext.TableMetrics
                .Where(m => m.SchemaName == schemaName && m.TableName == tableName)
                .OrderByDescending(m => m.CollectedAt)
                .Take(20)
                .GroupBy(m => m.CollectedAt)
                .Select(g => new
                {
                    collected_at = g.Key,
                    metrics = g.Select(m => new { m.MetricName, m.MetricValue })
                })
                .ToListAsync();

            return Results.Ok(new
            {
                success = true,
                data = tableHistory,
                message = $"{tableHistory.Count} snapshots encontrados"
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { success = false, error = ex.Message });
        }
    }

    private static async Task<RuleExecution> ExecuteSingleRuleAsync(
        ConnectionProfile profile, RuleCandidate rule, DataQualityService dataQualityService)
    {
        var execution = new RuleExecution
        {
            RuleCandidateId = rule.Id,
            ExecutedAt = DateTime.UtcNow
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Usar serviço existente adaptado ou criar nova lógica
            var result = await dataQualityService.ExecuteCustomRuleAsync(profile, new CustomDataQualityRule
            {
                SqlCondition = rule.CheckSql,
                RuleId = rule.RuleName,
                Schema = rule.SchemaName,
                TableName = rule.TableName
            });

            execution.TotalRecords = result.TotalRecords;
            execution.ValidRecords = result.ValidRecords;
            execution.InvalidRecords = result.InvalidRecords;
            execution.Success = result.Success;
            execution.ErrorMessage = result.ErrorMessage;

        }
        catch (Exception ex)
        {
            execution.Success = false;
            execution.ErrorMessage = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            execution.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;
        }

        return execution;
    }

    private static int GetUserId(HttpContext context) =>
        int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    // DTOs para requests
    public record PreflightRequest(int ProfileId, string SchemaName, List<string> TableNames);
    public record ExecutePreflightRequest(int ProfileId, PreflightResponse PreflightPlan);
    public record CollectMetricsRequest(int ProfileId, string SchemaName, string TableName);
    public record GenerateRulesRequest(int ProfileId, string SchemaName, string TableName);
    public record ExecuteRulesRequest(int ProfileId, List<long> RuleIds);
}