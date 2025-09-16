using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DbConnect.Web.Data;
using DbConnect.Core.Models;
using DbConnect.Web.AI;
using DbConnect.Web.Services;

namespace DbConnect.Web.Endpoints;

public static class ProfilesEndpoints
{
    public record ProfileCreateDto(
        string Name, DbKind Kind, string HostOrFile, int? Port,
        string Database, string Username, string? Password
    );

    public record ProfileUpdateDto(
        string Name, DbKind Kind, string HostOrFile, int? Port,
        string Database, string Username, string? Password // null/empty => mantém a antiga
    );

    public static IEndpointRouteBuilder MapProfilesEndpoints(this IEndpointRouteBuilder app)
    {
        var u = app.MapGroup("/api/u").RequireAuthorization();

        // Criar (salva + testa)
        u.MapPost("/profiles", async (ProfileCreateDto input, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // ConnectionProfile(string name, DbKind kind, string hostOrFile, int? port, string database, string username, string? password, DateTime createdAtUtc)
            var entity = new ConnectionProfile(
                input.Name.Trim(),
                input.Kind,
                input.HostOrFile.Trim(),
                input.Port,
                input.Database.Trim(),
                input.Username.Trim(),
                string.IsNullOrEmpty(input.Password) ? null : input.Password,
                DateTime.UtcNow
            )
            {
                UserId = uid
            };

            var (ok, err) = await TestConnectionAsync(entity);
            if (!ok) return Results.BadRequest(new { message = err });

            db.Profiles.Add(entity);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Salvo e testado com sucesso.", id = entity.Id });
        });

        // Editar (recria record + testa + salva)
        u.MapPut("/profiles/{id:int}", async (int id, ProfileUpdateDto input, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // buscamos sem tracking para poder recriar a entidade
            var current = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
            if (current is null || current.UserId != uid)
                return Results.NotFound(new { message = "Perfil não encontrado." });

            var newPassword = string.IsNullOrEmpty(input.Password) ? current.Password : input.Password;

            var updated = new ConnectionProfile(
                input.Name.Trim(),
                input.Kind,
                input.HostOrFile.Trim(),
                input.Port,
                input.Database.Trim(),
                input.Username.Trim(),
                newPassword,
                current.CreatedAtUtc // preserva data de criação
            )
            {
                Id = current.Id,     // preserva PK
                UserId = current.UserId
            };

            var (ok, err) = await TestConnectionAsync(updated);
            if (!ok) return Results.BadRequest(new { message = err });

            db.Attach(updated);
            db.Entry(updated).State = EntityState.Modified;
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Atualizado e testado com sucesso." });
        });

        // Apagar
        u.MapDelete("/profiles/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var p = await db.Profiles.FindAsync(id);
            if (p is null || p.UserId != uid)
                return Results.NotFound(new { message = "Perfil não encontrado." });

            db.Profiles.Remove(p);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Perfil removido." });
        });

        // Testar sem salvar (antigo endpoint)
        u.MapPost("/profiles/{id:int}/test", async (int id, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var p = await db.Profiles.FindAsync(id);
            if (p is null || p.UserId != uid)
                return Results.NotFound(new { message = "Perfil não encontrado." });

            var (ok, err) = await TestConnectionAsync(p);
            return ok ? Results.Ok(new { message = "Conexão OK." }) : Results.BadRequest(new { message = err });
        });

        // Conectar e definir como ativo (novo endpoint)
        u.MapPost("/profiles/{id:int}/connect", async (int id, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var p = await db.Profiles.FindAsync(id);
            if (p is null || p.UserId != uid)
                return Results.NotFound(new { message = "Perfil não encontrado." });

            var (ok, err) = await TestConnectionAsync(p);
            if (!ok) return Results.BadRequest(new { message = err });

            // Armazena o perfil ativo na sessão
            http.Session.SetInt32("ActiveProfileId", p.Id);
            
            return Results.Ok(new { message = "Conectado com sucesso!", profileId = p.Id });
        });

        // Obter perfil ativo
        u.MapGet("/profiles/active", async (AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.Ok(new { activeProfileId = (int?)null });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.Ok(new { activeProfileId = (int?)null });
            }

            return Results.Ok(new { activeProfileId = activeProfileId.Value });
        });

        // Desconectar (limpar sessão)
        u.MapPost("/profiles/disconnect", (HttpContext http) =>
        {
            http.Session.Remove("ActiveProfileId");
            return Results.Ok(new { message = "Desconectado com sucesso." });
        });

        // Listar tabelas do banco conectado
        u.MapGet("/database/tables", async (AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            try
            {
                var tables = await GetDatabaseTablesAsync(p);
                return Results.Ok(tables);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting tables: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao obter tabelas: {ex.Message}" });
            }
        });

        // Obter detalhes de uma tabela específica
        u.MapGet("/database/tables/{schema}/{tableName}", async (string schema, string tableName, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            try
            {
                var tableDetails = await GetTableDetailsAsync(p, schema, tableName);
                return Results.Ok(tableDetails);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting table details: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao obter detalhes da tabela: {ex.Message}" });
            }
        });

        // Obter informações do banco conectado
        u.MapGet("/database/info", async (AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            Console.WriteLine($"🔍 Database info requested - User: {uid}, ActiveProfileId: {activeProfileId}");
            
            if (activeProfileId == null)
            {
                Console.WriteLine("❌ No active profile found in session");
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });
            }

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                Console.WriteLine($"❌ Profile not found or not owned by user - Profile: {p}, UserId: {p?.UserId}");
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            Console.WriteLine($"✅ Profile found: {p.Name} ({p.Database})");
            
            try
            {
                var dbInfo = await GetDatabaseInfoAsync(p);
                Console.WriteLine("✅ Database info collected successfully");
                return Results.Ok(dbInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error collecting database info: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                return Results.BadRequest(new { message = $"Erro ao obter informações do banco: {ex.Message}" });
            }
        });

        // Data profiling avançado - análise automática de uma coluna específica
        u.MapGet("/database/tables/{schema}/{tableName}/columns/{columnName}/profile", async (string schema, string tableName, string columnName, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            try
            {
                var profilingResult = await ProfileColumnDataAsync(p, schema, tableName, columnName);
                return Results.Ok(profilingResult);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error profiling column: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao analisar coluna: {ex.Message}" });
            }
        });

        // Data profiling avançado - análise automática de todas as colunas de uma tabela
        u.MapGet("/database/tables/{schema}/{tableName}/profile", async (string schema, string tableName, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            try
            {
                var profilingResult = await ProfileTableDataAsync(p, schema, tableName);
                return Results.Ok(profilingResult);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error profiling table: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao analisar tabela: {ex.Message}" });
            }
        });

        // Salvar regras YAML customizadas para uma tabela
        u.MapPost("/database/tables/{schema}/{tableName}/rules", async (string schema, string tableName, YamlRulesRequest request, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            try
            {
                // TODO: Salvar regras YAML em base de dados ou arquivo
                // Por enquanto, apenas loggar
                Console.WriteLine($"💾 Saving YAML rules for {schema}.{tableName}:");
                Console.WriteLine(request.YamlContent);
                
                return Results.Ok(new { 
                    message = "Regras YAML salvas com sucesso!",
                    savedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving YAML rules: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao salvar regras: {ex.Message}" });
            }
        });

        // Carregar regras YAML customizadas de uma tabela
        u.MapGet("/database/tables/{schema}/{tableName}/rules", async (string schema, string tableName, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            try
            {
                // TODO: Carregar regras YAML da base de dados ou arquivo
                // Por enquanto, retornar template padrão
                var defaultYaml = $@"# Regras para {schema}.{tableName}
rules:
  - name: ""exemplo""
    description: ""Regra de exemplo""
    column: ""exemplo_coluna""
    type: ""regex""
    pattern: "".*""
    severity: ""warning""";
                
                return Results.Ok(new { 
                    yamlContent = defaultYaml,
                    lastModified = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading YAML rules: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao carregar regras: {ex.Message}" });
            }
        });

        // API Keys Management
        u.MapPost("/api-keys/validate", async (ApiKeyRequest request, AppDbContext db, HttpContext http, ApiKeyService apiKeyService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            var isValid = await apiKeyService.ValidateApiKeyAsync(uid, request.Provider, request.ApiKey);
            if (isValid)
            {
                await apiKeyService.SaveApiKeyAsync(uid, request.Provider, request.ApiKey);
                return Results.Ok(new { valid = true, message = "API Key validada e salva com sucesso!" });
            }
            else
            {
                return Results.BadRequest(new { valid = false, message = "API Key inválida" });
            }
        });

        u.MapGet("/api-keys/status", async (AppDbContext db, HttpContext http, ApiKeyService apiKeyService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            var hasOpenAI = await apiKeyService.HasValidApiKeyAsync(uid, "openai");
            var hasClaude = await apiKeyService.HasValidApiKeyAsync(uid, "claude");
            
            return Results.Ok(new { 
                openai = hasOpenAI, 
                claude = hasClaude,
                hasAnyKey = hasOpenAI || hasClaude 
            });
        });

        u.MapPost("/account/change-password", async (ChangePasswordRequest request, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            var user = await db.Users.FindAsync(uid);
            if (user == null)
            {
                return Results.BadRequest(new { success = false, message = "Usuário não encontrado" });
            }

            // Verificar senha atual
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return Results.BadRequest(new { success = false, message = "Senha atual incorreta" });
            }

            // Validar nova senha
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            {
                return Results.BadRequest(new { success = false, message = "Nova senha deve ter pelo menos 6 caracteres" });
            }

            // Alterar senha
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await db.SaveChangesAsync();

            return Results.Ok(new { success = true, message = "Senha alterada com sucesso!" });
        });

        // AI Data Quality Assessment
        u.MapPost("/database/tables/{schema}/{tableName}/ai-quality", async (string schema, string tableName, AppDbContext db, HttpContext http, DataQualityAI aiService, ApiKeyService apiKeyService, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            // Verificar API Key disponível
            var openAiKey = await apiKeyService.GetApiKeyAsync(uid, "openai");
            var claudeKey = await apiKeyService.GetApiKeyAsync(uid, "claude");
            
            if (openAiKey == null && claudeKey == null)
            {
                return Results.BadRequest(new { message = "Nenhuma API Key configurada. Configure uma API Key válida para usar a análise AI." });
            }

            try
            {
                // 1. Obter schema das colunas
                var columns = await GetTableSchemaAsync(p, schema, tableName);
                
                // 2. Obter dados de amostra (primeiras 10 linhas)  
                var sampleData = await GetSampleDataAsync(p, schema, tableName, 10);
                
                // 3. Determinar provider e API key
                string provider = openAiKey != null ? "openai" : "claude";
                string apiKey = openAiKey ?? claudeKey!;
                
                // 4. Gerar regras AI
                var aiRules = await aiService.GenerateRulesAsync(tableName, schema, columns, sampleData, apiKey, provider);
                
                // 5. Executar regras com persistência usando o novo serviço
                var (analysis, ruleResults) = await dataQualityService.ExecuteDataQualityAnalysisAsync(uid, p, schema, tableName, aiRules, provider);
                
                return Results.Ok(new { 
                    analysisId = analysis.Id,
                    rules = aiRules.Rules,
                    results = ruleResults,
                    status = analysis.Status,
                    provider = provider.ToUpper(),
                    generatedAt = analysis.CreatedAtUtc,
                    completedAt = analysis.CompletedAtUtc
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in AI Data Quality: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro na análise AI: {ex.Message}" });
            }
        });

        // Obter histórico de análises de Data Quality
        u.MapGet("/data-quality/analyses", async (AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            try
            {
                var analyses = await dataQualityService.GetAnalysisHistoryAsync(uid, activeProfileId);
                return Results.Ok(analyses);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting analysis history: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao obter histórico: {ex.Message}" });
            }
        });

        // Obter detalhes de uma análise específica com resultados
        u.MapGet("/data-quality/analyses/{analysisId:int}", async (int analysisId, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            try
            {
                var analysis = await dataQualityService.GetAnalysisWithResultsAsync(analysisId, uid);
                if (analysis == null)
                    return Results.NotFound(new { message = "Análise não encontrada." });
                    
                return Results.Ok(analysis);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting analysis details: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao obter detalhes da análise: {ex.Message}" });
            }
        });

        // Re-executar uma análise existente (funcionalidade de atualização sob demanda)
        u.MapPost("/database/tables/{schema}/{tableName}/ai-quality/refresh", async (string schema, string tableName, AppDbContext db, HttpContext http, DataQualityAI aiService, ApiKeyService apiKeyService, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            // Verificar API Key disponível
            var openAiKey = await apiKeyService.GetApiKeyAsync(uid, "openai");
            var claudeKey = await apiKeyService.GetApiKeyAsync(uid, "claude");
            
            if (openAiKey == null && claudeKey == null)
            {
                return Results.BadRequest(new { message = "Nenhuma API Key configurada. Configure uma API Key válida para usar a análise AI." });
            }

            try
            {
                // 1. Obter schema das colunas
                var columns = await GetTableSchemaAsync(p, schema, tableName);
                
                // 2. Obter dados de amostra (primeiras 10 linhas)  
                var sampleData = await GetSampleDataAsync(p, schema, tableName, 10);
                
                // 3. Determinar provider e API key
                string provider = openAiKey != null ? "openai" : "claude";
                string apiKey = openAiKey ?? claudeKey!;
                
                // 4. Gerar regras AI (novas regras a cada execução para garantir frescor)
                var aiRules = await aiService.GenerateRulesAsync(tableName, schema, columns, sampleData, apiKey, provider);
                
                // 5. Executar regras com persistência usando o novo serviço
                var (analysis, ruleResults) = await dataQualityService.ExecuteDataQualityAnalysisAsync(uid, p, schema, tableName, aiRules, provider);
                
                return Results.Ok(new { 
                    analysisId = analysis.Id,
                    rules = aiRules.Rules,
                    results = ruleResults,
                    status = analysis.Status,
                    provider = provider.ToUpper(),
                    generatedAt = analysis.CreatedAtUtc,
                    completedAt = analysis.CompletedAtUtc,
                    refreshed = true,
                    message = "Análise atualizada com sucesso!"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error refreshing AI Data Quality: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao atualizar análise AI: {ex.Message}" });
            }
        });

        // GESTÃO DE REGRAS CUSTOMIZADAS

        // Validar SQL de uma regra antes de salvar
        u.MapPost("/database/tables/{schema}/{tableName}/rules/validate", async (string schema, string tableName, ValidateRuleDto request, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            try
            {
                var (isValid, errorMessage) = await dataQualityService.ValidateSqlRuleAsync(p, schema, tableName, request.SqlCondition);
                
                return Results.Ok(new { 
                    valid = isValid, 
                    message = isValid ? "SQL válido!" : errorMessage 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error validating rule: {ex.Message}");
                return Results.BadRequest(new { valid = false, message = $"Erro ao validar regra: {ex.Message}" });
            }
        });

        // Salvar nova regra customizada
        u.MapPost("/database/tables/{schema}/{tableName}/custom-rules", async (string schema, string tableName, CreateCustomRuleDto request, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            try
            {
                // Primeiro validar o SQL
                var (isValid, errorMessage) = await dataQualityService.ValidateSqlRuleAsync(p, schema, tableName, request.SqlCondition);
                if (!isValid)
                {
                    return Results.BadRequest(new { message = $"SQL inválido: {errorMessage}" });
                }

                // Salvar a regra
                var rule = await dataQualityService.SaveCustomRuleAsync(uid, activeProfileId.Value, schema, tableName, request);
                
                return Results.Ok(new { 
                    message = "Regra salva com sucesso!", 
                    ruleId = rule.Id,
                    rule = rule
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving custom rule: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao salvar regra: {ex.Message}" });
            }
        });

        // Obter regras customizadas para uma tabela
        u.MapGet("/database/tables/{schema}/{tableName}/custom-rules", async (string schema, string tableName, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            try
            {
                var rules = await dataQualityService.GetCustomRulesAsync(uid, activeProfileId, schema, tableName);
                return Results.Ok(rules);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting custom rules: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao obter regras: {ex.Message}" });
            }
        });

        // Editar regra customizada
        u.MapPut("/data-quality/rules/{ruleId:int}", async (int ruleId, UpdateCustomRuleDto request, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            try
            {
                var rule = await dataQualityService.UpdateCustomRuleAsync(ruleId, uid, request);
                if (rule == null)
                {
                    return Results.NotFound(new { message = "Regra não encontrada." });
                }
                
                return Results.Ok(new { 
                    message = "Regra atualizada com sucesso!", 
                    rule = rule
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating custom rule: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao atualizar regra: {ex.Message}" });
            }
        });

        // Deletar regra customizada
        u.MapDelete("/data-quality/rules/{ruleId:int}", async (int ruleId, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            try
            {
                var deleted = await dataQualityService.DeleteCustomRuleAsync(ruleId, uid);
                if (!deleted)
                {
                    return Results.NotFound(new { message = "Regra não encontrada." });
                }
                
                return Results.Ok(new { message = "Regra removida com sucesso!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error deleting custom rule: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao remover regra: {ex.Message}" });
            }
        });

        // Executar regra customizada individual (botão "play")
        u.MapPost("/data-quality/rules/{ruleId:int}/execute", async (int ruleId, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            try
            {
                // Buscar a regra
                var customRules = await dataQualityService.GetCustomRulesAsync(uid);
                var rule = customRules.FirstOrDefault(r => r.Id == ruleId);
                
                if (rule == null)
                {
                    return Results.NotFound(new { message = "Regra não encontrada." });
                }

                // Buscar o perfil
                var profile = await db.Profiles.FindAsync(rule.ProfileId);
                if (profile == null || profile.UserId != uid)
                {
                    return Results.BadRequest(new { message = "Perfil não encontrado ou sem acesso." });
                }

                // Executar a regra
                var result = await dataQualityService.ExecuteSingleCustomRuleAsync(profile, rule.Schema, rule.TableName, rule);
                
                return Results.Ok(new { 
                    message = "Regra executada com sucesso!", 
                    result = result,
                    executedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error executing custom rule: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao executar regra: {ex.Message}" });
            }
        });

        // Obter todas as regras customizadas do usuário
        u.MapGet("/data-quality/rules", async (AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            try
            {
                var rules = await dataQualityService.GetCustomRulesAsync(uid, activeProfileId);
                return Results.Ok(rules);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting all custom rules: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao obter regras: {ex.Message}" });
            }
        });

        // VERSIONAMENTO DE REGRAS

        // Obter histórico de versões de uma regra específica
        u.MapGet("/data-quality/rules/{ruleId}/versions", async (string ruleId, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            try
            {
                // Obter todas as versões da regra
                var versions = await db.CustomDataQualityRules
                    .Where(r => r.UserId == uid && r.ProfileId == activeProfileId && r.RuleId == ruleId && r.IsActive)
                    .OrderByDescending(r => r.Version)
                    .Select(r => new {
                        r.Id,
                        r.Version,
                        r.Name,
                        r.Description,
                        r.SqlCondition,
                        r.Severity,
                        r.ExpectedPassRate,
                        r.IsLatestVersion,
                        r.ChangeReason,
                        r.CreatedAtUtc,
                        r.Notes
                    })
                    .ToListAsync();

                return Results.Ok(new { 
                    ruleId = ruleId,
                    versions = versions,
                    totalVersions = versions.Count,
                    latestVersion = versions.FirstOrDefault(v => v.IsLatestVersion)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting rule versions: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao obter versões: {ex.Message}" });
            }
        }).RequireAuthorization();

        // Reverter para uma versão específica de uma regra
        u.MapPost("/data-quality/rules/{ruleId}/revert/{version:int}", async (string ruleId, int version, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            try
            {
                // Encontrar a versão específica para reverter
                var targetVersion = await db.CustomDataQualityRules
                    .FirstOrDefaultAsync(r => r.UserId == uid && r.ProfileId == activeProfileId && 
                                            r.RuleId == ruleId && r.Version == version && r.IsActive);

                if (targetVersion == null)
                    return Results.NotFound(new { message = "Versão não encontrada" });

                // Encontrar a versão atual (latest)
                var currentVersion = await db.CustomDataQualityRules
                    .FirstOrDefaultAsync(r => r.UserId == uid && r.ProfileId == activeProfileId && 
                                            r.RuleId == ruleId && r.IsLatestVersion && r.IsActive);

                if (currentVersion == null)
                    return Results.NotFound(new { message = "Versão atual não encontrada" });

                // Obter próxima versão
                var maxVersion = await db.CustomDataQualityRules
                    .Where(r => r.UserId == uid && r.ProfileId == activeProfileId && 
                               r.RuleId == ruleId)
                    .MaxAsync(r => r.Version);

                var nextVersion = maxVersion + 1;

                // Desativar versão atual como "latest"
                currentVersion.IsLatestVersion = false;
                currentVersion.UpdatedAtUtc = DateTime.UtcNow;

                // Criar nova versão baseada na versão target
                var revertedRule = new CustomDataQualityRule
                {
                    UserId = targetVersion.UserId,
                    ProfileId = targetVersion.ProfileId,
                    Schema = targetVersion.Schema,
                    TableName = targetVersion.TableName,
                    RuleId = targetVersion.RuleId,
                    Version = nextVersion,
                    Name = targetVersion.Name,
                    Description = targetVersion.Description,
                    Dimension = targetVersion.Dimension,
                    Column = targetVersion.Column,
                    SqlCondition = targetVersion.SqlCondition,
                    Severity = targetVersion.Severity,
                    ExpectedPassRate = targetVersion.ExpectedPassRate,
                    Source = targetVersion.Source,
                    Notes = targetVersion.Notes,
                    ChangeReason = $"Reversão para versão {version}",
                    CreatedAtUtc = DateTime.UtcNow,
                    IsActive = true,
                    IsLatestVersion = true
                };

                db.CustomDataQualityRules.Add(revertedRule);
                await db.SaveChangesAsync();

                return Results.Ok(new { 
                    ok = true, 
                    message = $"Regra revertida para versão {version}",
                    newVersion = nextVersion,
                    rule = new {
                        revertedRule.Id,
                        revertedRule.Version,
                        revertedRule.Name,
                        revertedRule.SqlCondition
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error reverting rule: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao reverter regra: {ex.Message}" });
            }
        }).RequireAuthorization();

        // ASSISTENTE DE REFINAMENTO DE SQL

        // Refinar regra SQL com erro usando IA
        u.MapPost("/data-quality/rules/refine", async (RefineSqlRuleRequest request, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            try
            {
                var result = await dataQualityService.RefineSqlRuleAsync(
                    uid, 
                    activeProfileId.Value, 
                    request.Schema, 
                    request.TableName, 
                    request.RuleId, 
                    request.OriginalSqlCondition, 
                    request.ErrorMessage
                );

                if (result.Success)
                {
                    return Results.Ok(new { 
                        success = true,
                        refinedCondition = result.RefinedCondition,
                        explanation = result.Explanation,
                        confidence = result.Confidence,
                        message = "SQL refinado com sucesso pela IA"
                    });
                }
                else
                {
                    return Results.BadRequest(new { 
                        success = false,
                        message = result.ErrorMessage,
                        originalCondition = result.OriginalCondition
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error refining SQL rule: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao refinar regra: {ex.Message}" });
            }
        }).RequireAuthorization();

        // PARSER SQL AVANÇADO

        // Validação SQL avançada com parser
        u.MapPost("/data-quality/sql/validate", async (ValidateSqlRequest request, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            try
            {
                var result = await dataQualityService.ValidateSqlConditionAsync(
                    uid, 
                    activeProfileId.Value, 
                    request.Schema, 
                    request.TableName, 
                    request.SqlCondition
                );

                return Results.Ok(new { 
                    isValid = result.IsValid,
                    errors = result.Errors,
                    warnings = result.Warnings,
                    correctedSql = result.CorrectedSql,
                    complexityScore = result.ComplexityScore,
                    message = result.IsValid ? "SQL válido" : "SQL inválido - veja erros e sugestões"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error validating SQL: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao validar SQL: {ex.Message}" });
            }
        }).RequireAuthorization();

        // AMOSTRAGEM INTELIGENTE

        // Obter estratégia de amostragem otimizada para uma tabela
        u.MapGet("/database/tables/{schema}/{tableName}/sampling-strategy", async (string schema, string tableName, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            try
            {
                var strategy = await dataQualityService.GetOptimalSamplingStrategyAsync(uid, activeProfileId.Value, schema, tableName);
                
                return Results.Ok(new { 
                    samplingType = strategy.SamplingType.ToString(),
                    sampleSize = strategy.SampleSize,
                    reason = strategy.Reason,
                    primaryKeyColumn = strategy.PrimaryKeyColumn,
                    tableStats = strategy.TableStats != null ? new {
                        totalRows = strategy.TableStats.TotalRows,
                        sizeMB = strategy.TableStats.SizeMB,
                        averageRowWidth = strategy.TableStats.AverageRowWidth,
                        columnCount = strategy.TableStats.ColumnStatistics.Count
                    } : null,
                    createdAt = strategy.CreatedAt
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting sampling strategy: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao obter estratégia: {ex.Message}" });
            }
        }).RequireAuthorization();

        // Obter amostra inteligente de dados
        u.MapGet("/database/tables/{schema}/{tableName}/intelligent-sample", async (string schema, string tableName, AppDbContext db, HttpContext http, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            try
            {
                // Primeiro obter a estratégia
                var strategy = await dataQualityService.GetOptimalSamplingStrategyAsync(uid, activeProfileId.Value, schema, tableName);
                
                // Então obter a amostra
                var sample = await dataQualityService.GetIntelligentSampleAsync(uid, activeProfileId.Value, schema, tableName, strategy);
                
                return Results.Ok(new { 
                    strategy = new {
                        type = strategy.SamplingType.ToString(),
                        size = strategy.SampleSize,
                        reason = strategy.Reason,
                        tableStats = strategy.TableStats != null ? new {
                            totalRows = strategy.TableStats.TotalRows,
                            sizeMB = Math.Round(strategy.TableStats.SizeMB, 2)
                        } : null
                    },
                    sample = sample,
                    actualSampleSize = sample.Count,
                    message = $"Amostra obtida usando estratégia: {strategy.SamplingType}"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting intelligent sample: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao obter amostra: {ex.Message}" });
            }
        }).RequireAuthorization();

        // TEMPLATES PRÉ-DEFINIDOS DE DATA QUALITY

        // Obter todos os templates disponíveis
        u.MapGet("/data-quality/templates", (DataQualityTemplateService templateService) =>
        {
            try
            {
                var templates = templateService.GetPreDefinedTemplates();
                var grouped = templates.GroupBy(t => t.Category)
                    .ToDictionary(g => g.Key, g => g.ToList());
                
                return Results.Ok(new { 
                    templates = templates,
                    byCategory = grouped,
                    totalCount = templates.Count
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting templates: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao obter templates: {ex.Message}" });
            }
        });

        // Obter templates aplicáveis a uma coluna específica
        u.MapGet("/database/tables/{schema}/{tableName}/columns/{columnName}/templates", async (string schema, string tableName, string columnName, AppDbContext db, HttpContext http, DataQualityTemplateService templateService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            try
            {
                // Obter schema das colunas
                var columns = await GetTableSchemaAsync(p, schema, tableName);
                var targetColumn = columns.FirstOrDefault(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                
                if (targetColumn == null)
                {
                    return Results.NotFound(new { message = "Coluna não encontrada." });
                }

                // Obter templates aplicáveis
                var applicableTemplates = templateService.GetApplicableTemplates(targetColumn, columns);
                
                return Results.Ok(new { 
                    column = targetColumn,
                    applicableTemplates = applicableTemplates,
                    totalApplicable = applicableTemplates.Count
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting applicable templates: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao obter templates aplicáveis: {ex.Message}" });
            }
        });

        // Aplicar template pré-definido a uma coluna
        u.MapPost("/database/tables/{schema}/{tableName}/columns/{columnName}/apply-template", async (string schema, string tableName, string columnName, ApplyTemplateRequest request, AppDbContext db, HttpContext http, DataQualityTemplateService templateService, DataQualityService dataQualityService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            try
            {
                // Obter schema das colunas
                var columns = await GetTableSchemaAsync(p, schema, tableName);
                var targetColumn = columns.FirstOrDefault(c => c.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                
                if (targetColumn == null)
                {
                    return Results.NotFound(new { message = "Coluna não encontrada." });
                }

                // Obter template
                var templates = templateService.GetPreDefinedTemplates();
                var template = templates.FirstOrDefault(t => t.Id == request.TemplateId);
                
                if (template == null)
                {
                    return Results.NotFound(new { message = "Template não encontrado." });
                }

                // Aplicar template
                var rule = templateService.ApplyTemplate(template, targetColumn, request.ReferenceColumn);
                
                // Validar SQL gerado
                var (isValid, errorMessage) = await dataQualityService.ValidateSqlRuleAsync(p, schema, tableName, rule.SqlCondition);
                if (!isValid)
                {
                    return Results.BadRequest(new { 
                        message = $"Template gerou SQL inválido: {errorMessage}",
                        generatedSql = rule.SqlCondition 
                    });
                }

                // Converter para DTO e salvar como regra customizada
                var customRuleDto = new CreateCustomRuleDto
                {
                    RuleId = rule.Id,
                    Name = rule.Name,
                    Description = rule.Description,
                    Dimension = rule.Dimension,
                    Column = rule.Column,
                    SqlCondition = rule.SqlCondition,
                    Severity = rule.Severity,
                    ExpectedPassRate = rule.ExpectedPassRate,
                    Notes = $"Gerado a partir do template: {template.Name}"
                };

                var savedRule = await dataQualityService.SaveCustomRuleAsync(uid, activeProfileId.Value, schema, tableName, customRuleDto);
                
                return Results.Ok(new { 
                    message = "Template aplicado com sucesso!",
                    rule = savedRule,
                    generatedSql = rule.SqlCondition
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error applying template: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro ao aplicar template: {ex.Message}" });
            }
        });

        // Gerar análise híbrida (AI + Templates)
        u.MapPost("/database/tables/{schema}/{tableName}/hybrid-quality", async (string schema, string tableName, AppDbContext db, HttpContext http, DataQualityAI aiService, ApiKeyService apiKeyService, DataQualityService dataQualityService, DataQualityTemplateService templateService) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var activeProfileId = http.Session.GetInt32("ActiveProfileId");
            
            if (activeProfileId == null)
                return Results.BadRequest(new { message = "Nenhum perfil conectado." });

            var p = await db.Profiles.FindAsync(activeProfileId.Value);
            if (p == null || p.UserId != uid)
            {
                http.Session.Remove("ActiveProfileId");
                return Results.BadRequest(new { message = "Perfil ativo não encontrado." });
            }

            // Verificar API Key disponível
            var openAiKey = await apiKeyService.GetApiKeyAsync(uid, "openai");
            var claudeKey = await apiKeyService.GetApiKeyAsync(uid, "claude");
            
            if (openAiKey == null && claudeKey == null)
            {
                return Results.BadRequest(new { message = "Nenhuma API Key configurada. Configure uma API Key válida para usar a análise AI." });
            }

            try
            {
                // 1. Obter schema das colunas
                var columns = await GetTableSchemaAsync(p, schema, tableName);
                
                // 2. Obter dados de amostra  
                var sampleData = await GetSampleDataAsync(p, schema, tableName, 10);
                
                // 3. Determinar provider e API key
                string provider = openAiKey != null ? "openai" : "claude";
                string apiKey = openAiKey ?? claudeKey!;
                
                // 4. Gerar regras AI
                var aiRules = await aiService.GenerateRulesAsync(tableName, schema, columns, sampleData, apiKey, provider);
                
                // 5. Gerar regras de templates para cada coluna
                var templateRules = new List<DataQualityRule>();
                foreach (var column in columns)
                {
                    var applicableTemplates = templateService.GetApplicableTemplates(column, columns);
                    
                    // Aplicar alguns templates mais relevantes (máximo 2 por coluna)
                    foreach (var template in applicableTemplates.Take(2))
                    {
                        var templateRule = templateService.ApplyTemplate(template, column);
                        templateRules.Add(templateRule);
                    }
                }

                // 6. Combinar regras AI + Templates
                var allRules = new List<DataQualityRule>();
                allRules.AddRange(aiRules.Rules);
                allRules.AddRange(templateRules);

                // 7. Remover duplicatas por similaridade de SQL
                var uniqueRules = allRules
                    .GroupBy(r => r.SqlCondition.Replace(" ", "").ToLower())
                    .Select(g => g.First())
                    .ToList();

                var hybridRules = new DataQualityRules { Rules = uniqueRules };
                
                // 8. Executar regras híbridas
                var (analysis, ruleResults) = await dataQualityService.ExecuteDataQualityAnalysisAsync(uid, p, schema, tableName, hybridRules, $"{provider}_hybrid");
                
                return Results.Ok(new { 
                    analysisId = analysis.Id,
                    aiRulesCount = aiRules.Rules.Count,
                    templateRulesCount = templateRules.Count,
                    totalRulesGenerated = allRules.Count,
                    uniqueRulesExecuted = uniqueRules.Count,
                    rules = uniqueRules,
                    results = ruleResults,
                    status = analysis.Status,
                    provider = provider.ToUpper(),
                    mode = "HYBRID",
                    generatedAt = analysis.CreatedAtUtc,
                    completedAt = analysis.CompletedAtUtc
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error in Hybrid Data Quality: {ex.Message}");
                return Results.BadRequest(new { message = $"Erro na análise híbrida: {ex.Message}" });
            }
        });

        return app;
    }

    public record YamlRulesRequest(string YamlContent);
    public record ApiKeyRequest(string Provider, string ApiKey);
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    public record ApplyTemplateRequest(string TemplateId, string? ReferenceColumn);

    // Helper: testa conexão de acordo com o DbKind
    private static async Task<(bool ok, string? error)> TestConnectionAsync(ConnectionProfile p, CancellationToken ct = default)
    {
        try
        {
            switch (p.Kind)
            {
                case DbKind.PostgreSql:
                    var csb = new NpgsqlConnectionStringBuilder
                    {
                        Host = p.HostOrFile,
                        Port = p.Port ?? 5432,
                        Database = p.Database,
                        Username = p.Username,
                        Password = p.Password ?? ""
                    };
                    await using (var conn = new NpgsqlConnection(csb.ConnectionString))
                    {
                        await conn.OpenAsync(ct);
                        await using var cmd = new NpgsqlCommand("select 1", conn);
                        await cmd.ExecuteScalarAsync(ct);
                    }
                    break;

                // TODO: implementar SqlServer / MySql / Sqlite quando habilitados
                default:
                    break;
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // Helper: coleta informações do banco de dados
    private static async Task<object> GetDatabaseInfoAsync(ConnectionProfile p, CancellationToken ct = default)
    {
        switch (p.Kind)
        {
            case DbKind.PostgreSql:
                return await GetPostgreSqlInfoAsync(p, ct);
            
            // TODO: implementar SqlServer / MySql / Sqlite quando habilitados
            default:
                throw new NotSupportedException($"Tipo de banco {p.Kind} não suportado para informações.");
        }
    }

    // Helper: informações específicas do PostgreSQL
    private static async Task<object> GetPostgreSqlInfoAsync(ConnectionProfile p, CancellationToken ct = default)
    {
        Console.WriteLine("🔍 Starting PostgreSQL info collection...");
        
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = p.HostOrFile,
            Port = p.Port ?? 5432,
            Database = p.Database,
            Username = p.Username,
            Password = p.Password ?? ""
        };

        Console.WriteLine($"🔗 Connecting to: {csb.Host}:{csb.Port}/{csb.Database}");

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync(ct);
        
        Console.WriteLine("✅ Connection established");

        // Informações básicas do banco
        var basicInfo = new Dictionary<string, object>();
        
        // Nome e versão do PostgreSQL
        try
        {
            await using (var cmd = new NpgsqlCommand("SELECT version()", conn))
            {
                var version = await cmd.ExecuteScalarAsync(ct) as string ?? "Desconhecido";
                basicInfo["serverVersion"] = version;
                Console.WriteLine($"✅ Server version: {version}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to get server version: {ex.Message}");
            basicInfo["serverVersion"] = "Desconhecido";
        }

        // Nome do banco atual
        basicInfo["databaseName"] = p.Database;
        
        // Tamanho do banco
        try
        {
            await using (var cmd = new NpgsqlCommand("SELECT pg_size_pretty(pg_database_size(current_database()))", conn))
            {
                var size = await cmd.ExecuteScalarAsync(ct) as string ?? "0 bytes";
                basicInfo["databaseSize"] = size;
                Console.WriteLine($"✅ Database size: {size}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Failed to get database size: {ex.Message}");
            basicInfo["databaseSize"] = "Indisponível";
        }

        // Estatísticas de objetos
        var objectStats = new Dictionary<string, object>();
        
        // Número de tabelas (excluindo system tables)
        await using (var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*) 
            FROM information_schema.tables 
            WHERE table_schema NOT IN ('information_schema', 'pg_catalog') 
            AND table_type = 'BASE TABLE'", conn))
        {
            objectStats["tablesCount"] = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        // Número de views
        await using (var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*) 
            FROM information_schema.views 
            WHERE table_schema NOT IN ('information_schema', 'pg_catalog')", conn))
        {
            objectStats["viewsCount"] = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        // Número de índices (excluindo system indexes)
        await using (var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*) 
            FROM pg_indexes 
            WHERE schemaname NOT IN ('information_schema', 'pg_catalog')", conn))
        {
            objectStats["indexesCount"] = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        // Número de funções/procedures
        await using (var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*) 
            FROM information_schema.routines 
            WHERE routine_schema NOT IN ('information_schema', 'pg_catalog')", conn))
        {
            objectStats["functionsCount"] = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        // Número de triggers
        await using (var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*) 
            FROM information_schema.triggers", conn))
        {
            objectStats["triggersCount"] = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        // Estatísticas de dados - tabelas com mais registros (top 5)
        var topTables = new List<object>();
        await using (var cmd = new NpgsqlCommand(@"
            SELECT 
                schemaname,
                relname as tablename,
                n_tup_ins + n_tup_upd + n_tup_del as estimated_rows
            FROM pg_stat_user_tables 
            ORDER BY estimated_rows DESC 
            LIMIT 5", conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                topTables.Add(new
                {
                    schema = reader.GetString(0),
                    table = reader.GetString(1),
                    estimatedRows = reader.GetInt64(2)
                });
            }
        }

        // Informações de performance
        var performanceInfo = new Dictionary<string, object>();
        
        // Número de conexões ativas
        await using (var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*) 
            FROM pg_stat_activity 
            WHERE state = 'active'", conn))
        {
            performanceInfo["activeConnections"] = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        // Uptime do servidor
        await using (var cmd = new NpgsqlCommand(@"
            SELECT EXTRACT(EPOCH FROM (now() - pg_postmaster_start_time()))", conn))
        {
            var uptimeSeconds = Convert.ToDouble(await cmd.ExecuteScalarAsync(ct));
            var uptime = TimeSpan.FromSeconds(uptimeSeconds);
            performanceInfo["uptime"] = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
        }

        // Cache hit ratio
        await using (var cmd = new NpgsqlCommand(@"
            SELECT 
                ROUND(
                    100.0 * sum(blks_hit) / (sum(blks_hit) + sum(blks_read)), 2
                ) as cache_hit_ratio
            FROM pg_stat_database", conn))
        {
            var hitRatio = await cmd.ExecuteScalarAsync(ct);
            performanceInfo["cacheHitRatio"] = hitRatio != DBNull.Value ? $"{hitRatio}%" : "N/A";
        }

        // Schemas disponíveis
        var schemas = new List<string>();
        await using (var cmd = new NpgsqlCommand(@"
            SELECT schema_name 
            FROM information_schema.schemata 
            WHERE schema_name NOT IN ('information_schema', 'pg_catalog', 'pg_toast', 'pg_temp_1', 'pg_toast_temp_1')
            ORDER BY schema_name", conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                schemas.Add(reader.GetString(0));
            }
        }

        return new
        {
            basic = basicInfo,
            objects = objectStats,
            performance = performanceInfo,
            topTables = topTables,
            schemas = schemas,
            collectedAt = DateTime.UtcNow
        };
    }

    // Helper: lista tabelas do banco de dados
    private static async Task<object> GetDatabaseTablesAsync(ConnectionProfile p, CancellationToken ct = default)
    {
        switch (p.Kind)
        {
            case DbKind.PostgreSql:
                return await GetPostgreSqlTablesAsync(p, ct);
            
            default:
                throw new NotSupportedException($"Tipo de banco {p.Kind} não suportado para listagem de tabelas.");
        }
    }

    // Helper: lista tabelas específicas do PostgreSQL
    private static async Task<object> GetPostgreSqlTablesAsync(ConnectionProfile p, CancellationToken ct = default)
    {
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = p.HostOrFile,
            Port = p.Port ?? 5432,
            Database = p.Database,
            Username = p.Username,
            Password = p.Password ?? ""
        };

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync(ct);

        var tables = new List<object>();
        await using (var cmd = new NpgsqlCommand(@"
            SELECT 
                t.table_schema,
                t.table_name,
                COALESCE(s.n_tup_ins + s.n_tup_upd + s.n_tup_del, 0) as estimated_rows,
                pg_size_pretty(pg_total_relation_size(quote_ident(t.table_schema)||'.'||quote_ident(t.table_name))) as table_size,
                obj_description(c.oid) as table_comment
            FROM information_schema.tables t
            LEFT JOIN pg_stat_user_tables s ON s.schemaname = t.table_schema AND s.relname = t.table_name
            LEFT JOIN pg_class c ON c.relname = t.table_name
            LEFT JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = t.table_schema
            WHERE t.table_type = 'BASE TABLE'
            AND t.table_schema NOT IN ('information_schema', 'pg_catalog')
            ORDER BY estimated_rows DESC, t.table_schema, t.table_name", conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                tables.Add(new
                {
                    schema = reader.GetString(0),
                    name = reader.GetString(1),
                    estimatedRows = reader.GetInt64(2),
                    size = reader.GetString(3),
                    comment = reader.IsDBNull(4) ? null : reader.GetString(4)
                });
            }
        }

        return new
        {
            tables = tables,
            totalTables = tables.Count,
            collectedAt = DateTime.UtcNow
        };
    }

    // Helper: obter detalhes de uma tabela específica
    private static async Task<object> GetTableDetailsAsync(ConnectionProfile p, string schema, string tableName, CancellationToken ct = default)
    {
        switch (p.Kind)
        {
            case DbKind.PostgreSql:
                return await GetPostgreSqlTableDetailsAsync(p, schema, tableName, ct);
            
            default:
                throw new NotSupportedException($"Tipo de banco {p.Kind} não suportado para detalhes de tabela.");
        }
    }

    // Helper: detalhes específicos do PostgreSQL
    private static async Task<object> GetPostgreSqlTableDetailsAsync(ConnectionProfile p, string schema, string tableName, CancellationToken ct = default)
    {
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = p.HostOrFile,
            Port = p.Port ?? 5432,
            Database = p.Database,
            Username = p.Username,
            Password = p.Password ?? ""
        };

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync(ct);

        // 1. Estrutura da tabela (colunas)
        var columns = new List<object>();
        await using (var cmd = new NpgsqlCommand(@"
            SELECT 
                c.column_name,
                c.data_type,
                c.character_maximum_length,
                c.is_nullable,
                c.column_default,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_primary_key
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT ku.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage ku ON tc.constraint_name = ku.constraint_name
                WHERE tc.table_schema = @schema AND tc.table_name = @tableName AND tc.constraint_type = 'PRIMARY KEY'
            ) pk ON pk.column_name = c.column_name
            WHERE c.table_schema = @schema AND c.table_name = @tableName
            ORDER BY c.ordinal_position", conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("tableName", tableName);
            
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                columns.Add(new
                {
                    name = reader.GetString(0),
                    dataType = reader.GetString(1),
                    maxLength = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                    nullable = reader.GetString(3) == "YES",
                    defaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                    isPrimaryKey = reader.GetBoolean(5)
                });
            }
        }

        // 2. Índices da tabela
        var indexes = new List<object>();
        await using (var cmd = new NpgsqlCommand(@"
            SELECT 
                i.relname as index_name,
                array_agg(a.attname ORDER BY k.keynum) as column_names,
                ind.indisunique as is_unique,
                ind.indisprimary as is_primary
            FROM pg_index ind
            JOIN pg_class i ON i.oid = ind.indexrelid
            JOIN pg_class t ON t.oid = ind.indrelid
            JOIN pg_namespace n ON n.oid = t.relnamespace
            JOIN unnest(ind.indkey) WITH ORDINALITY k(attnum, keynum) ON true
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
            WHERE n.nspname = @schema AND t.relname = @tableName
            GROUP BY i.relname, ind.indisunique, ind.indisprimary
            ORDER BY ind.indisprimary DESC, ind.indisunique DESC", conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("tableName", tableName);
            
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var columnNames = reader.GetValue(1) as string[] ?? Array.Empty<string>();
                indexes.Add(new
                {
                    name = reader.GetString(0),
                    columns = columnNames,
                    isUnique = reader.GetBoolean(2),
                    isPrimary = reader.GetBoolean(3)
                });
            }
        }

        // 3. Primeiros 10 registros
        var sampleData = new List<Dictionary<string, object?>>();
        var quotedTableName = $"\"{schema}\".\"{tableName}\"";
        await using (var cmd = new NpgsqlCommand($"SELECT * FROM {quotedTableName} LIMIT 10", conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[columnName] = value;
                }
                sampleData.Add(row);
            }
        }

        // 4. Estatísticas básicas (data profiling)
        var stats = new Dictionary<string, object>();
        await using (var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {quotedTableName}", conn))
        {
            var totalRows = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
            stats["totalRows"] = totalRows;
        }

        return new
        {
            schema = schema,
            tableName = tableName,
            columns = columns,
            indexes = indexes,
            sampleData = sampleData,
            statistics = stats,
            collectedAt = DateTime.UtcNow
        };
    }

    // Helper: análise de profiling avançado de uma coluna específica
    private static async Task<object> ProfileColumnDataAsync(ConnectionProfile p, string schema, string tableName, string columnName, CancellationToken ct = default)
    {
        switch (p.Kind)
        {
            case DbKind.PostgreSql:
                return await ProfilePostgreSqlColumnAsync(p, schema, tableName, columnName, ct);
            
            default:
                throw new NotSupportedException($"Tipo de banco {p.Kind} não suportado para profiling de colunas.");
        }
    }

    // Helper: análise de profiling avançado de todas as colunas de uma tabela
    private static async Task<object> ProfileTableDataAsync(ConnectionProfile p, string schema, string tableName, CancellationToken ct = default)
    {
        switch (p.Kind)
        {
            case DbKind.PostgreSql:
                return await ProfilePostgreSqlTableAsync(p, schema, tableName, ct);
            
            default:
                throw new NotSupportedException($"Tipo de banco {p.Kind} não suportado para profiling de tabelas.");
        }
    }

    // Helper: profiling específico PostgreSQL - coluna individual
    private static async Task<object> ProfilePostgreSqlColumnAsync(ConnectionProfile p, string schema, string tableName, string columnName, CancellationToken ct = default)
    {
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = p.HostOrFile,
            Port = p.Port ?? 5432,
            Database = p.Database,
            Username = p.Username,
            Password = p.Password ?? ""
        };

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync(ct);

        var quotedTableName = $"\"{schema}\".\"{tableName}\"";
        var quotedColumnName = $"\"{columnName}\"";

        var profile = new Dictionary<string, object?>();

        // 1. Informações básicas da coluna
        await using (var cmd = new NpgsqlCommand(@"
            SELECT 
                c.data_type,
                c.character_maximum_length,
                c.is_nullable,
                c.column_default
            FROM information_schema.columns c
            WHERE c.table_schema = @schema AND c.table_name = @tableName AND c.column_name = @columnName", conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("tableName", tableName);
            cmd.Parameters.AddWithValue("columnName", columnName);
            
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                profile["dataType"] = reader.GetString(0);
                profile["maxLength"] = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                profile["nullable"] = reader.GetString(2) == "YES";
                profile["defaultValue"] = reader.IsDBNull(3) ? null : reader.GetString(3);
            }
        }

        // 2. Estatísticas básicas
        var stats = new Dictionary<string, object>();
        
        // Total de registros
        await using (var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {quotedTableName}", conn))
        {
            stats["totalRows"] = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
        }

        // Valores nulos
        await using (var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {quotedTableName} WHERE {quotedColumnName} IS NULL", conn))
        {
            var nullCount = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
            stats["nullCount"] = nullCount;
            stats["nullPercentage"] = (long)stats["totalRows"] > 0 ? Math.Round((double)nullCount / (long)stats["totalRows"] * 100, 2) : 0;
        }

        // Valores únicos
        await using (var cmd = new NpgsqlCommand($"SELECT COUNT(DISTINCT {quotedColumnName}) FROM {quotedTableName} WHERE {quotedColumnName} IS NOT NULL", conn))
        {
            var uniqueCount = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
            stats["uniqueCount"] = uniqueCount;
            var nonNullCount = (long)stats["totalRows"] - (long)stats["nullCount"];
            stats["uniquePercentage"] = nonNullCount > 0 ? Math.Round((double)uniqueCount / nonNullCount * 100, 2) : 0;
        }

        // Valores duplicados (mais frequentes)
        var topValues = new List<object>();
        await using (var cmd = new NpgsqlCommand($@"
            SELECT {quotedColumnName}, COUNT(*) as frequency
            FROM {quotedTableName} 
            WHERE {quotedColumnName} IS NOT NULL
            GROUP BY {quotedColumnName}
            ORDER BY frequency DESC 
            LIMIT 10", conn))
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                topValues.Add(new
                {
                    value = reader.GetValue(0),
                    frequency = reader.GetInt64(1),
                    percentage = (long)stats["totalRows"] > 0 ? Math.Round((double)reader.GetInt64(1) / (long)stats["totalRows"] * 100, 2) : 0
                });
            }
        }
        stats["topValues"] = topValues;

        // 3. Análise específica por tipo de dados
        var patterns = new Dictionary<string, object>();
        var dataType = profile["dataType"]?.ToString()?.ToLower() ?? "";

        if (dataType.Contains("text") || dataType.Contains("varchar") || dataType.Contains("char"))
        {
            // Análise de padrões para campos de texto
            
            // Padrão de email
            await using (var cmd = new NpgsqlCommand($@"
                SELECT COUNT(*) 
                FROM {quotedTableName} 
                WHERE {quotedColumnName} ~ '^[A-Za-z0-9._%-]+@[A-Za-z0-9.-]+\.[A-Za-z]{{2,4}}$'", conn))
            {
                try
                {
                    var emailCount = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
                    var nonNullCount = (long)stats["totalRows"] - (long)stats["nullCount"];
                    patterns["emailPattern"] = new
                    {
                        count = emailCount,
                        percentage = nonNullCount > 0 ? Math.Round((double)emailCount / nonNullCount * 100, 2) : 0,
                        description = "Registros que seguem padrão de email"
                    };
                }
                catch
                {
                    patterns["emailPattern"] = new { count = 0, percentage = 0, description = "Erro ao verificar padrão de email" };
                }
            }

            // Padrão CPF (brasileiro)
            await using (var cmd = new NpgsqlCommand($@"
                SELECT COUNT(*) 
                FROM {quotedTableName} 
                WHERE {quotedColumnName} ~ '^[0-9]{{3}}\.[0-9]{{3}}\.[0-9]{{3}}-[0-9]{{2}}$'
                   OR {quotedColumnName} ~ '^[0-9]{{11}}$'", conn))
            {
                try
                {
                    var cpfCount = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
                    var nonNullCount = (long)stats["totalRows"] - (long)stats["nullCount"];
                    patterns["cpfPattern"] = new
                    {
                        count = cpfCount,
                        percentage = nonNullCount > 0 ? Math.Round((double)cpfCount / nonNullCount * 100, 2) : 0,
                        description = "Registros que seguem padrão de CPF (XXX.XXX.XXX-XX ou 11 dígitos)"
                    };
                }
                catch
                {
                    patterns["cpfPattern"] = new { count = 0, percentage = 0, description = "Erro ao verificar padrão de CPF" };
                }
            }

            // Padrão CNPJ (brasileiro)
            await using (var cmd = new NpgsqlCommand($@"
                SELECT COUNT(*) 
                FROM {quotedTableName} 
                WHERE {quotedColumnName} ~ '^[0-9]{{2}}\.[0-9]{{3}}\.[0-9]{{3}}/[0-9]{{4}}-[0-9]{{2}}$'
                   OR {quotedColumnName} ~ '^[0-9]{{14}}$'", conn))
            {
                try
                {
                    var cnpjCount = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
                    var nonNullCount = (long)stats["totalRows"] - (long)stats["nullCount"];
                    patterns["cnpjPattern"] = new
                    {
                        count = cnpjCount,
                        percentage = nonNullCount > 0 ? Math.Round((double)cnpjCount / nonNullCount * 100, 2) : 0,
                        description = "Registros que seguem padrão de CNPJ (XX.XXX.XXX/XXXX-XX ou 14 dígitos)"
                    };
                }
                catch
                {
                    patterns["cnpjPattern"] = new { count = 0, percentage = 0, description = "Erro ao verificar padrão de CNPJ" };
                }
            }

            // Padrão telefone brasileiro
            await using (var cmd = new NpgsqlCommand($@"
                SELECT COUNT(*) 
                FROM {quotedTableName} 
                WHERE {quotedColumnName} ~ '^(\([0-9]{{2}}\)|[0-9]{{2}})[\s\-]?[9]?[0-9]{{4}}[\s\-]?[0-9]{{4}}$'", conn))
            {
                try
                {
                    var phoneCount = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
                    var nonNullCount = (long)stats["totalRows"] - (long)stats["nullCount"];
                    patterns["phonePattern"] = new
                    {
                        count = phoneCount,
                        percentage = nonNullCount > 0 ? Math.Round((double)phoneCount / nonNullCount * 100, 2) : 0,
                        description = "Registros que seguem padrão de telefone brasileiro"
                    };
                }
                catch
                {
                    patterns["phonePattern"] = new { count = 0, percentage = 0, description = "Erro ao verificar padrão de telefone" };
                }
            }

            // Comprimento de strings
            await using (var cmd = new NpgsqlCommand($@"
                SELECT 
                    MIN(LENGTH({quotedColumnName})) as min_length,
                    MAX(LENGTH({quotedColumnName})) as max_length,
                    AVG(LENGTH({quotedColumnName})) as avg_length
                FROM {quotedTableName} 
                WHERE {quotedColumnName} IS NOT NULL", conn))
            {
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    patterns["stringLength"] = new
                    {
                        min = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        max = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        average = reader.IsDBNull(2) ? 0 : Math.Round(reader.GetDouble(2), 2),
                        description = "Estatísticas de comprimento das strings"
                    };
                }
            }
        }
        else if (dataType.Contains("timestamp") || dataType.Contains("date"))
        {
            // Análise para campos de data
            await using (var cmd = new NpgsqlCommand($@"
                SELECT 
                    MIN({quotedColumnName}) as min_date,
                    MAX({quotedColumnName}) as max_date
                FROM {quotedTableName} 
                WHERE {quotedColumnName} IS NOT NULL", conn))
            {
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    patterns["dateRange"] = new
                    {
                        minDate = reader.IsDBNull(0) ? null : reader.GetValue(0),
                        maxDate = reader.IsDBNull(1) ? null : reader.GetValue(1),
                        description = "Faixa de datas encontradas"
                    };
                }
            }
        }
        else if (dataType.Contains("numeric") || dataType.Contains("integer") || dataType.Contains("decimal"))
        {
            // Análise para campos numéricos
            await using (var cmd = new NpgsqlCommand($@"
                SELECT 
                    MIN({quotedColumnName}) as min_val,
                    MAX({quotedColumnName}) as max_val,
                    AVG({quotedColumnName}) as avg_val,
                    STDDEV({quotedColumnName}) as stddev_val
                FROM {quotedTableName} 
                WHERE {quotedColumnName} IS NOT NULL", conn))
            {
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    patterns["numericStats"] = new
                    {
                        min = reader.IsDBNull(0) ? null : reader.GetValue(0),
                        max = reader.IsDBNull(1) ? null : reader.GetValue(1),
                        average = reader.IsDBNull(2) ? (double?)null : Math.Round(reader.GetDouble(2), 4),
                        standardDeviation = reader.IsDBNull(3) ? (double?)null : Math.Round(reader.GetDouble(3), 4),
                        description = "Estatísticas numéricas básicas"
                    };
                }
            }
        }

        return new
        {
            schema = schema,
            tableName = tableName,
            columnName = columnName,
            columnInfo = profile,
            statistics = stats,
            patterns = patterns,
            collectedAt = DateTime.UtcNow
        };
    }

    // Helper: profiling específico PostgreSQL - tabela completa
    private static async Task<object> ProfilePostgreSqlTableAsync(ConnectionProfile p, string schema, string tableName, CancellationToken ct = default)
    {
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = p.HostOrFile,
            Port = p.Port ?? 5432,
            Database = p.Database,
            Username = p.Username,
            Password = p.Password ?? ""
        };

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync(ct);

        // Obter lista de colunas
        var columns = new List<string>();
        await using (var cmd = new NpgsqlCommand(@"
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @tableName
            ORDER BY ordinal_position", conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("tableName", tableName);
            
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                columns.Add(reader.GetString(0));
            }
        }

        // Profiling inteligente de todas as colunas (sem limite artificial)
        var columnsProfile = new List<object>();
        foreach (var column in columns)
        {
            try
            {
                var columnProfile = await ProfilePostgreSqlColumnAsync(p, schema, tableName, column, ct);
                columnsProfile.Add(columnProfile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error profiling column {column}: {ex.Message}");
                // Continua com outras colunas mesmo se uma der erro
            }
        }

        return new
        {
            schema = schema,
            tableName = tableName,
            totalColumns = columns.Count,
            profiledColumns = columnsProfile.Count,
            columnsProfile = columnsProfile,
            collectedAt = DateTime.UtcNow,
            note = columnsProfile.Count < columns.Count ? $"Algumas colunas não foram analisadas devido a erros" : null
        };
    }

    // Helper: Obter schema de colunas para AI
    private static async Task<List<ColumnSchema>> GetTableSchemaAsync(ConnectionProfile p, string schema, string tableName, CancellationToken ct = default)
    {
        if (p.Kind != DbKind.PostgreSql)
            throw new NotSupportedException("Apenas PostgreSQL suportado");

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = p.HostOrFile,
            Port = p.Port ?? 5432,
            Database = p.Database,
            Username = p.Username,
            Password = p.Password ?? ""
        };

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync(ct);

        var sql = @"
            SELECT 
                column_name,
                data_type,
                is_nullable
            FROM information_schema.columns 
            WHERE table_schema = @schema 
            AND table_name = @tableName
            ORDER BY ordinal_position";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@tableName", tableName);

        var columns = new List<ColumnSchema>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        
        while (await reader.ReadAsync(ct))
        {
            columns.Add(new ColumnSchema
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES"
            });
        }

        return columns;
    }

    // Helper: Obter dados de amostra para AI
    private static async Task<List<Dictionary<string, object?>>> GetSampleDataAsync(ConnectionProfile p, string schema, string tableName, int limit = 10, CancellationToken ct = default)
    {
        if (p.Kind != DbKind.PostgreSql)
            throw new NotSupportedException("Apenas PostgreSQL suportado");

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = p.HostOrFile,
            Port = p.Port ?? 5432,
            Database = p.Database,
            Username = p.Username,
            Password = p.Password ?? ""
        };

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync(ct);

        var quotedTableName = $"\"{schema}\".\"{tableName}\"";
        var sql = $"SELECT * FROM {quotedTableName} LIMIT @limit";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@limit", limit);

        var sampleData = new List<Dictionary<string, object?>>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[columnName] = value;
            }
            sampleData.Add(row);
        }

        return sampleData;
    }

}
