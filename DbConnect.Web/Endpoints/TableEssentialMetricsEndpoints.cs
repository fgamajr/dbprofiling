using DbConnect.Core.Models;
using DbConnect.Web.Data;
using DbConnect.Web.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DbConnect.Web.Endpoints;

public static class TableEssentialMetricsEndpoints
{
    public static void MapTableEssentialMetricsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/essential-metrics")
            .WithTags("Essential Table Metrics")
            .RequireAuthorization();

        // Remover endpoint problemático por enquanto
        /*
        group.MapPost("/collect", CollectTableMetrics)
            .WithSummary("Coleta métricas essenciais de uma tabela")
            .WithDescription("Executa análise básica sem IA: contagens, completude, cardinalidade, duplicatas e estatísticas descritivas");
        */

        // Coletar métricas básicas (versão simplificada)
        group.MapPost("/collect-basic", CollectBasicTableMetrics)
            .WithSummary("Coleta métricas básicas de uma tabela (versão rápida)")
            .WithDescription("Versão simplificada que foca nas métricas mais importantes");

        // Coletar métricas avançadas (análise de padrões + outliers + relações)
        group.MapPost("/collect-advanced", CollectAdvancedTableMetrics)
            .WithSummary("Coleta métricas avançadas de uma tabela")
            .WithDescription("Análise de padrões regex, detecção de outliers estatísticos e descoberta de relações entre colunas");

        // Buscar outliers paginados para uma coluna específica
        group.MapGet("/outliers/{schema}/{tableName}/{columnName}", GetOutliersPaginated)
            .WithSummary("Busca outliers paginados para uma coluna específica")
            .WithDescription("Retorna outliers ordenados por distância da média com paginação");

        // Endpoint de teste sem autenticação
        app.MapPost("/api/test-metrics/collect-basic", CollectBasicTableMetricsTest)
            .WithTags("Test Metrics")
            .WithSummary("Teste coleta métricas básicas (sem autenticação)");

        // Endpoint de teste para métricas avançadas sem autenticação
        app.MapPost("/api/test-metrics/collect-advanced", CollectAdvancedTableMetricsTest)
            .WithTags("Test Metrics")
            .WithSummary("Teste coleta métricas avançadas (sem autenticação)");

        // Endpoint de teste para outliers paginados sem autenticação
        app.MapGet("/api/test-metrics/outliers/{schema}/{tableName}/{columnName}", GetOutliersPaginatedTest)
            .WithTags("Test Metrics")
            .WithSummary("Teste outliers paginados (sem autenticação)");

        // Endpoint simplificado para outliers paginados
        app.MapGet("/api/simple-outliers/{schema}/{tableName}/{columnName}", GetOutliersPaginatedSimple)
            .WithTags("Test Metrics")
            .WithSummary("Outliers paginados (endpoint simplificado)");

        // TODO: Implementar outros endpoints após migration
        /*
        group.MapGet("/table/{schema}/{tableName}", GetTableMetrics)
            .WithSummary("Obtém métricas essenciais existentes de uma tabela");

        group.MapGet("/tables", ListTablesWithMetrics)
            .WithSummary("Lista tabelas com métricas essenciais coletadas");
        */
    }

    /*
    // TODO: Implementar após migration
    private static async Task<IResult> CollectTableMetrics(...) { }
    private static async Task<IResult> GetTableMetrics(...) { }
    private static async Task<IResult> ListTablesWithMetrics(...) { }
    */

    private static async Task<IResult> CollectBasicTableMetrics(
        CollectTableMetricsRequest request,
        AppDbContext db,
        SimpleTableMetricsService simpleMetricsService,
        HttpContext httpContext)
    {
        try
        {
            var userId = GetUserId(httpContext);
            if (userId == null) return Results.Unauthorized();

            // Buscar perfil de conexão ativo
            var activeProfileId = httpContext.Session.GetInt32("ActiveProfileId");
            if (!activeProfileId.HasValue)
            {
                return Results.BadRequest(new { message = "Nenhum perfil de conexão ativo. Selecione um perfil primeiro." });
            }

            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == activeProfileId && p.UserId == userId);
            if (profile == null)
            {
                return Results.BadRequest(new { message = "Perfil de conexão não encontrado." });
            }

            Console.WriteLine($"📊 Iniciando coleta de métricas básicas para {request.Schema}.{request.TableName}");

            // Coletar métricas básicas
            var metrics = await simpleMetricsService.CollectBasicMetricsAsync(
                userId.Value, profile, request.Schema, request.TableName);

            Console.WriteLine($"✅ Métricas básicas coletadas com sucesso!");

            return Results.Ok(new
            {
                success = true,
                message = "Métricas básicas coletadas com sucesso",
                data = metrics
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro coletando métricas básicas: {ex.Message}");
            return Results.BadRequest(new
            {
                success = false,
                message = $"Erro ao coletar métricas: {ex.Message}"
            });
        }
    }

    private static async Task<IResult> CollectAdvancedTableMetrics(
        CollectTableMetricsRequest request,
        AppDbContext db,
        IPatternAnalysisService patternAnalysisService,
        HttpContext httpContext)
    {
        try
        {
            var userId = GetUserId(httpContext);
            if (userId == null) return Results.Unauthorized();

            // Buscar perfil de conexão ativo
            var activeProfileId = httpContext.Session.GetInt32("ActiveProfileId");
            if (!activeProfileId.HasValue)
            {
                return Results.BadRequest(new { message = "Nenhum perfil de conexão ativo. Selecione um perfil primeiro." });
            }

            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == activeProfileId && p.UserId == userId);
            if (profile == null)
            {
                return Results.BadRequest(new { message = "Perfil de conexão não encontrado." });
            }

            Console.WriteLine($"🔍 Iniciando análise avançada para {request.Schema}.{request.TableName}");

            var connectionString = BuildConnectionString(profile);
            var startTime = DateTime.UtcNow;

            // Análise de padrões e outliers por coluna
            var columnMetrics = await patternAnalysisService.AnalyzeTablePatterns(connectionString, request.Schema, request.TableName);

            // Análise de relações entre colunas
            var relationshipMetrics = await patternAnalysisService.AnalyzeTableRelationships(connectionString, request.Schema, request.TableName);

            var processingTime = DateTime.UtcNow - startTime;

            var advancedMetrics = new AdvancedTableMetrics
            {
                TableName = request.TableName,
                SchemaName = request.Schema,
                ColumnMetrics = columnMetrics,
                RelationshipMetrics = relationshipMetrics,
                AnalysisTimestamp = DateTime.UtcNow,
                ProcessingTime = processingTime
            };

            Console.WriteLine($"✅ Análise avançada concluída em {processingTime.TotalSeconds:F2}s!");

            return Results.Ok(new
            {
                success = true,
                message = "Métricas avançadas coletadas com sucesso",
                data = advancedMetrics,
                summary = new
                {
                    totalColumns = columnMetrics.Count,
                    columnsWithPatterns = columnMetrics.Count(c => c.PatternMatches.Any()),
                    columnsWithOutliers = columnMetrics.Count(c => c.OutlierAnalysis != null),
                    statusDateRelationships = relationshipMetrics.StatusDateRelationships.Count,
                    strongCorrelations = relationshipMetrics.NumericCorrelations.Count,
                    processingTimeSeconds = processingTime.TotalSeconds
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro na análise avançada: {ex.Message}");
            return Results.BadRequest(new
            {
                success = false,
                message = $"Erro na análise avançada: {ex.Message}",
                details = ex.InnerException?.Message
            });
        }
    }

    private static async Task<IResult> CollectBasicTableMetricsTest(
        CollectTableMetricsRequest request,
        AppDbContext db,
        SimpleTableMetricsService simpleMetricsService)
    {
        try
        {
            Console.WriteLine($"🧪 TESTE: Iniciando coleta de métricas básicas para {request.Schema}.{request.TableName}");

            // Buscar primeiro perfil disponível para teste
            var profile = await db.Profiles.FirstOrDefaultAsync();
            if (profile == null)
            {
                return Results.BadRequest(new { message = "Nenhum perfil de conexão encontrado para teste." });
            }

            // Simular métricas básicas para teste
            var basicMetrics = new
            {
                schema = request.Schema,
                tableName = request.TableName,
                collectedAt = DateTime.UtcNow,
                general = new
                {
                    totalRows = 1000,
                    totalColumns = 5,
                    estimatedSizeBytes = 50000
                },
                message = "Métricas simuladas para teste"
            };

            Console.WriteLine($"✅ TESTE: Métricas básicas simuladas geradas!");

            return Results.Ok(new
            {
                success = true,
                message = "Métricas básicas de teste geradas com sucesso",
                data = basicMetrics,
                isTest = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TESTE: Erro coletando métricas básicas: {ex.Message}");
            return Results.BadRequest(new
            {
                success = false,
                message = $"Erro ao coletar métricas de teste: {ex.Message}",
                isTest = true
            });
        }
    }

    private static async Task<IResult> CollectAdvancedTableMetricsTest(
        CollectTableMetricsRequest request,
        AppDbContext db,
        IPatternAnalysisService patternAnalysisService)
    {
        try
        {
            Console.WriteLine($"🧪 TESTE AVANÇADO: Iniciando análise avançada para {request.Schema}.{request.TableName}");

            // Buscar primeiro perfil disponível para teste
            var profile = await db.Profiles.FirstOrDefaultAsync();
            if (profile == null)
            {
                return Results.BadRequest(new { message = "Nenhum perfil de conexão encontrado para teste." });
            }

            var connectionString = BuildConnectionString(profile);
            var startTime = DateTime.UtcNow;

            // Análise de padrões e outliers por coluna (REAL)
            var columnMetrics = await patternAnalysisService.AnalyzeTablePatterns(connectionString, request.Schema, request.TableName);

            // Análise de relações entre colunas (REAL)
            var relationshipMetrics = await patternAnalysisService.AnalyzeTableRelationships(connectionString, request.Schema, request.TableName);

            var processingTime = DateTime.UtcNow - startTime;

            var advancedMetrics = new AdvancedTableMetrics
            {
                TableName = request.TableName,
                SchemaName = request.Schema,
                ColumnMetrics = columnMetrics,
                RelationshipMetrics = relationshipMetrics,
                AnalysisTimestamp = DateTime.UtcNow,
                ProcessingTime = processingTime
            };

            Console.WriteLine($"✅ TESTE AVANÇADO: Análise concluída em {processingTime.TotalSeconds:F2}s!");

            return Results.Ok(new
            {
                success = true,
                message = "Métricas avançadas de teste coletadas com sucesso",
                data = advancedMetrics,
                summary = new
                {
                    totalColumns = columnMetrics.Count,
                    columnsWithPatterns = columnMetrics.Count(c => c.PatternMatches.Any()),
                    columnsWithOutliers = columnMetrics.Count(c => c.OutlierAnalysis != null),
                    statusDateRelationships = relationshipMetrics.StatusDateRelationships.Count,
                    strongCorrelations = relationshipMetrics.NumericCorrelations.Count,
                    processingTimeSeconds = processingTime.TotalSeconds
                },
                isTest = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TESTE AVANÇADO: Erro na análise: {ex.Message}");
            Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
            return Results.BadRequest(new
            {
                success = false,
                message = $"Erro na análise avançada de teste: {ex.Message}",
                details = ex.InnerException?.Message,
                isTest = true
            });
        }
    }

    private static string BuildConnectionString(ConnectionProfile profile)
    {
        return profile.Kind switch
        {
            DbKind.SqlServer => $"Server={profile.HostOrFile},{profile.Port};Database={profile.Database};User Id={profile.Username};Password={profile.Password};TrustServerCertificate=true;",
            DbKind.PostgreSql => $"Host={profile.HostOrFile};Port={profile.Port};Database={profile.Database};Username={profile.Username};Password={profile.Password};",
            DbKind.MySql => $"Server={profile.HostOrFile};Port={profile.Port};Database={profile.Database};Uid={profile.Username};Pwd={profile.Password};",
            _ => throw new NotSupportedException($"Tipo de conexão {profile.Kind} não suportado para análise avançada")
        };
    }

    private static async Task<IResult> GetOutliersPaginated(
        string schema,
        string tableName,
        string columnName,
        HttpContext httpContext,
        AppDbContext dbContext,
        IPatternAnalysisService patternAnalysisService,
        int page = 0,
        int pageSize = 20)
    {
        try
        {
            var userId = GetUserId(httpContext);
            if (userId == null)
            {
                return Results.Unauthorized();
            }

            // Buscar primeiro perfil do usuário (assumindo que será usado o perfil principal)
            var activeProfile = await dbContext.Profiles
                .Where(p => p.UserId == userId.Value)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            if (activeProfile == null)
            {
                return Results.BadRequest(new { error = "Nenhum perfil de conexão encontrado para o usuário" });
            }

            // Obter outliers paginados do serviço
            using var connection = patternAnalysisService.GetType()
                .GetMethod("CreateConnection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .Invoke(patternAnalysisService, new object[] { activeProfile.ConnectionString }) as System.Data.Common.DbConnection;

            if (connection == null)
            {
                return Results.BadRequest(new { error = "Não foi possível criar conexão" });
            }

            await connection.OpenAsync();

            // Obter metadados das colunas
            var getTableColumnsMethod = patternAnalysisService.GetType()
                .GetMethod("GetTableColumns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var columns = await (Task<List<(string ColumnName, string DataType)>>)getTableColumnsMethod!
                .Invoke(patternAnalysisService, new object[] { connection, schema, tableName })!;

            // Buscar outliers paginados
            var analyzeOutliersMethod = patternAnalysisService.GetType()
                .GetMethod("AnalyzeColumnOutliers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var outlierAnalysis = await (Task<OutlierAnalysis?>)analyzeOutliersMethod!
                .Invoke(patternAnalysisService, new object[] { connection, schema, tableName, columnName, columns, page, pageSize })!;

            if (outlierAnalysis == null)
            {
                return Results.BadRequest(new { error = "Erro ao analisar outliers" });
            }

            return Results.Ok(outlierAnalysis);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Erro interno: {ex.Message}");
        }
    }

    private static async Task<IResult> GetOutliersPaginatedTest(
        string schema,
        string tableName,
        string columnName,
        IPatternAnalysisService patternAnalysisService,
        AppDbContext db,
        int page = 0,
        int pageSize = 20)
    {
        try
        {
            // Buscar primeiro perfil disponível para teste
            var profile = await db.Profiles.FirstOrDefaultAsync();
            if (profile == null)
            {
                return Results.BadRequest(new { message = "Nenhum perfil de conexão encontrado para teste." });
            }

            var testConnectionString = BuildConnectionString(profile);

            using var connection = patternAnalysisService.GetType()
                .GetMethod("CreateConnection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                .Invoke(patternAnalysisService, new object[] { testConnectionString }) as System.Data.Common.DbConnection;

            if (connection == null)
            {
                return Results.BadRequest(new { error = "Não foi possível criar conexão" });
            }

            await connection.OpenAsync();

            // Obter metadados das colunas
            var getTableColumnsMethod = patternAnalysisService.GetType()
                .GetMethod("GetTableColumns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var columns = await (Task<List<(string ColumnName, string DataType)>>)getTableColumnsMethod!
                .Invoke(patternAnalysisService, new object[] { connection, schema, tableName })!;

            // Buscar outliers paginados
            var analyzeOutliersMethod = patternAnalysisService.GetType()
                .GetMethod("AnalyzeColumnOutliers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var outlierAnalysis = await (Task<OutlierAnalysis?>)analyzeOutliersMethod!
                .Invoke(patternAnalysisService, new object[] { connection, schema, tableName, columnName, columns, page, pageSize })!;

            if (outlierAnalysis == null)
            {
                return Results.BadRequest(new { error = "Erro ao analisar outliers" });
            }

            return Results.Ok(outlierAnalysis);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Erro interno: {ex.Message}");
        }
    }

    private static async Task<IResult> GetOutliersPaginatedSimple(
        string schema,
        string tableName,
        string columnName,
        IPatternAnalysisService patternAnalysisService,
        AppDbContext db,
        int page = 0,
        int pageSize = 20)
    {
        try
        {
            // Buscar primeiro perfil disponível para teste
            var profile = await db.Profiles.FirstOrDefaultAsync();
            if (profile == null)
            {
                return Results.BadRequest(new { message = "Nenhum perfil de conexão encontrado para teste." });
            }

            var connectionString = BuildConnectionString(profile);
            var result = await patternAnalysisService.AnalyzeTablePatterns(connectionString, schema, tableName);

            if (result == null)
            {
                return Results.BadRequest(new { error = "Nenhuma métrica encontrada" });
            }

            var columnMetric = result.FirstOrDefault(c => c.ColumnName == columnName);
            if (columnMetric?.OutlierAnalysis == null)
            {
                return Results.BadRequest(new { error = $"Nenhum outlier encontrado para a coluna {columnName}" });
            }

            var outlierAnalysis = columnMetric.OutlierAnalysis;

            // Simular paginação nos dados existentes
            var totalOutliers = outlierAnalysis.OutlierCount;
            var totalPages = Math.Ceiling((double)totalOutliers / pageSize);
            var startIdx = page * pageSize;

            // Para simulação, usar os outliers de exemplo, mas com paginação correta
            var pagedOutliers = outlierAnalysis.OutlierRows?.Skip(startIdx).Take(pageSize).ToList() ?? new List<OutlierRowData>();

            var pagedAnalysis = new OutlierAnalysis
            {
                TotalValues = outlierAnalysis.TotalValues,
                OutlierCount = outlierAnalysis.OutlierCount,
                OutlierPercentage = outlierAnalysis.OutlierPercentage,
                Mean = outlierAnalysis.Mean,
                StandardDeviation = outlierAnalysis.StandardDeviation,
                LowerBound = outlierAnalysis.LowerBound,
                UpperBound = outlierAnalysis.UpperBound,
                SampleOutliers = pagedOutliers.Select(r =>
                    r.OutlierValue is double d ? d :
                    double.TryParse(r.OutlierValue?.ToString(), out var parsed) ? parsed : 0.0).ToList(),
                OutlierRows = pagedOutliers,
                CurrentPage = page,
                PageSize = pageSize
                // TotalPages é calculado automaticamente baseado em OutlierCount e PageSize
            };

            return Results.Ok(pagedAnalysis);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Erro interno: {ex.Message}");
        }
    }

    private static int? GetUserId(HttpContext httpContext)
    {
        var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                          httpContext.User.FindFirstValue("sub");
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public record CollectTableMetricsRequest(
    string Schema,
    string TableName
);