using DbConnect.Core.Models;
using DbConnect.Web.AI;
using DbConnect.Web.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DbConnect.Web.Services;

public class DataQualityService
{
    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly IntelligentSampler _sampler;

    public DataQualityService(AppDbContext db, HttpClient httpClient, IntelligentSampler sampler)
    {
        _db = db;
        _httpClient = httpClient;
        _sampler = sampler;
    }

    public async Task<(DataQualityAnalysis analysis, List<DataQualityRuleResult> results)> ExecuteDataQualityAnalysisAsync(
        int userId, ConnectionProfile profile, string schema, string tableName, 
        DataQualityRules rules, string provider, CancellationToken ct = default)
    {
        // 1. Criar registro de análise
        var analysis = new DataQualityAnalysis
        {
            UserId = userId,
            ProfileId = profile.Id,
            TableName = tableName,
            Schema = schema,
            Provider = provider.ToUpper(),
            CreatedAtUtc = DateTime.UtcNow,
            Status = "running"
        };

        _db.DataQualityAnalyses.Add(analysis);
        await _db.SaveChangesAsync(ct);

        var results = new List<DataQualityRuleResult>();
        var executionResults = new List<DataQualityResult>();

        try
        {
            // 2. Executar regras contra o banco PostgreSQL
            if (profile.Kind != DbKind.PostgreSql)
                throw new NotSupportedException("Apenas PostgreSQL suportado");

            var csb = new NpgsqlConnectionStringBuilder
            {
                Host = profile.HostOrFile,
                Port = profile.Port ?? 5432,
                Database = profile.Database,
                Username = profile.Username,
                Password = profile.Password ?? ""
            };

            Console.WriteLine($"🔗 Conectando ao PostgreSQL: {profile.HostOrFile}:{profile.Port ?? 5432}/{profile.Database}");
            var maskedConnectionString = csb.ConnectionString;
            if (!string.IsNullOrEmpty(profile.Password))
            {
                maskedConnectionString = maskedConnectionString.Replace(profile.Password, "***");
            }
            Console.WriteLine($"🔗 ConnectionString: {maskedConnectionString}");

            await using var conn = new NpgsqlConnection(csb.ConnectionString);
            await conn.OpenAsync(ct);

            var quotedTableName = $"\"{schema}\".\"{tableName}\"";

            // 3. Obter total de registros
            var totalCountSql = $"SELECT COUNT(*) FROM {quotedTableName}";
            Console.WriteLine($"🔍 SQL de contagem: {totalCountSql}");

            await using var totalCmd = new NpgsqlCommand(totalCountSql, conn);
            var totalRecords = Convert.ToInt64(await totalCmd.ExecuteScalarAsync(ct));

            Console.WriteLine($"📊 Total de registros na tabela {quotedTableName}: {totalRecords:N0}");

            // 4. Executar cada regra
            foreach (var rule in rules.Rules)
            {
                var result = await ExecuteSingleRuleAsync(conn, quotedTableName, rule, totalRecords, ct);
                results.Add(result);

                // 5. Salvar resultado no banco
                var dbResult = new DataQualityResult
                {
                    AnalysisId = analysis.Id,
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Description = rule.Description,
                    Dimension = rule.Dimension,
                    Column = rule.Column,
                    SqlCondition = rule.SqlCondition,
                    Severity = rule.Severity,
                    ExpectedPassRate = rule.ExpectedPassRate,
                    Status = result.Status.ToLower(),
                    ActualPassRate = result.PassRate,
                    TotalRecords = result.TotalRecords,
                    ValidRecords = result.ValidRecords,
                    InvalidRecords = result.InvalidRecords,
                    ErrorMessage = result.ErrorMessage,
                    ExecutedAtUtc = DateTime.UtcNow
                };

                executionResults.Add(dbResult);
            }

            // 6. Salvar todos os resultados no banco
            _db.DataQualityResults.AddRange(executionResults);
            
            // 7. Marcar análise como completa
            analysis.CompletedAtUtc = DateTime.UtcNow;
            analysis.Status = "completed";
            
            await _db.SaveChangesAsync(ct);

            Console.WriteLine($"✅ Análise concluída: {results.Count} regras executadas");
            Console.WriteLine($"📈 Resultados: {results.Count(r => r.Status == "PASS")} passou, {results.Count(r => r.Status == "FAIL")} falhou, {results.Count(r => r.Status == "ERROR")} com erro");

            // 🚨 ALERTAS DE INCONGRUÊNCIA
            var alerts = DetectQualityAlerts(results, totalRecords);
            if (alerts.Any())
            {
                Console.WriteLine("🚨 ALERTAS DE QUALIDADE DETECTADOS:");
                foreach (var alert in alerts)
                {
                    Console.WriteLine($"   ⚠️ {alert.Type}: {alert.Message}");
                }
            }

            return (analysis, results);
        }
        catch (Exception ex)
        {
            // 8. Marcar análise como erro
            analysis.Status = "error";
            analysis.ErrorMessage = ex.Message;
            analysis.CompletedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            Console.WriteLine($"❌ Erro na análise: {ex.Message}");
            throw;
        }
    }

    private async Task<DataQualityRuleResult> ExecuteSingleRuleAsync(
        NpgsqlConnection conn, string quotedTableName, DataQualityRule rule, 
        long totalRecords, CancellationToken ct)
    {
        try
        {
            Console.WriteLine($"🔍 Executando regra: {rule.Name} ({rule.Id})");
            Console.WriteLine($"   SQL: SELECT COUNT(*) FROM {quotedTableName} WHERE {rule.SqlCondition}");
            
            // Construir e executar SQL de validação
            var validationSql = $"SELECT COUNT(*) FROM {quotedTableName} WHERE {rule.SqlCondition}";
            
            await using var validCmd = new NpgsqlCommand(validationSql, conn);
            var validRecords = Convert.ToInt64(await validCmd.ExecuteScalarAsync(ct));
            
            var passRate = totalRecords > 0 ? (double)validRecords / totalRecords * 100.0 : 0.0;
            var status = passRate >= rule.ExpectedPassRate ? "PASS" : "FAIL";
            
            Console.WriteLine($"   ✅ Resultado: {validRecords:N0}/{totalRecords:N0} registros válidos ({passRate:F1}%) - {status}");

            return new DataQualityRuleResult
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                Dimension = rule.Dimension,
                Status = status,
                PassRate = Math.Round(passRate, 2),
                ExpectedPassRate = rule.ExpectedPassRate,
                TotalRecords = totalRecords,
                ValidRecords = validRecords,
                InvalidRecords = totalRecords - validRecords,
                Severity = rule.Severity,
                ExecutedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Erro na regra {rule.Name}: {ex.Message}");
            
            return new DataQualityRuleResult
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                Dimension = rule.Dimension,
                Status = "ERROR",
                PassRate = 0,
                ExpectedPassRate = rule.ExpectedPassRate,
                TotalRecords = totalRecords,
                ValidRecords = 0,
                InvalidRecords = totalRecords,
                Severity = "error",
                ErrorMessage = ex.Message,
                ExecutedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<List<DataQualityAnalysis>> GetAnalysisHistoryAsync(int userId, int? profileId = null)
    {
        var query = _db.DataQualityAnalyses
            .Include(a => a.Results)
            .Include(a => a.Profile)
            .Where(a => a.UserId == userId);

        if (profileId.HasValue)
            query = query.Where(a => a.ProfileId == profileId);

        return await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(50) // Últimas 50 análises
            .ToListAsync();
    }

    public async Task<DataQualityAnalysis?> GetAnalysisWithResultsAsync(int analysisId, int userId)
    {
        return await _db.DataQualityAnalyses
            .Include(a => a.Results)
            .Include(a => a.Profile)
            .FirstOrDefaultAsync(a => a.Id == analysisId && a.UserId == userId);
    }

    public async Task<(bool isValid, string? errorMessage)> ValidateSqlRuleAsync(ConnectionProfile profile, string schema, string tableName, string sqlCondition)
    {
        if (profile.Kind != DbKind.PostgreSql)
            return (false, "Apenas PostgreSQL suportado");

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = profile.HostOrFile,
            Port = profile.Port ?? 5432,
            Database = profile.Database,
            Username = profile.Username,
            Password = profile.Password ?? ""
        };

        try
        {
            await using var conn = new NpgsqlConnection(csb.ConnectionString);
            await conn.OpenAsync();
            
            var quotedTableName = $"\"{schema}\".\"{tableName}\"";
            
            // Tentar executar a query com LIMIT 1 para validar sintaxe
            var validationSql = $"SELECT COUNT(*) FROM {quotedTableName} WHERE {sqlCondition} LIMIT 1";
            
            await using var cmd = new NpgsqlCommand(validationSql, conn);
            await cmd.ExecuteScalarAsync();
            
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<CustomDataQualityRule> SaveCustomRuleAsync(int userId, int profileId, string schema, string tableName, CreateCustomRuleDto dto)
    {
        // Verificar se já existe uma versão da regra
        var existingRule = await _db.CustomDataQualityRules
            .Where(r => r.UserId == userId && r.ProfileId == profileId && r.Schema == schema && 
                       r.TableName == tableName && r.RuleId == dto.RuleId && r.IsLatestVersion)
            .FirstOrDefaultAsync();

        int nextVersion = 1;
        
        if (existingRule != null)
        {
            // Obter próxima versão
            var maxVersion = await _db.CustomDataQualityRules
                .Where(r => r.UserId == userId && r.ProfileId == profileId && r.Schema == schema && 
                           r.TableName == tableName && r.RuleId == dto.RuleId)
                .MaxAsync(r => r.Version);
            nextVersion = maxVersion + 1;
            
            // Desativar versão anterior como "latest"
            existingRule.IsLatestVersion = false;
            existingRule.UpdatedAtUtc = DateTime.UtcNow;
        }

        var rule = new CustomDataQualityRule
        {
            UserId = userId,
            ProfileId = profileId,
            Schema = schema,
            TableName = tableName,
            RuleId = dto.RuleId,
            Version = nextVersion,
            Name = dto.Name,
            Description = dto.Description,
            Dimension = dto.Dimension,
            Column = dto.Column ?? "",
            SqlCondition = dto.SqlCondition,
            Severity = dto.Severity,
            ExpectedPassRate = dto.ExpectedPassRate,
            Source = "custom",
            Notes = dto.Notes,
            ChangeReason = dto.ChangeReason ?? (nextVersion > 1 ? "Atualização de regra existente" : "Criação inicial"),
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true,
            IsLatestVersion = true
        };

        _db.CustomDataQualityRules.Add(rule);
        await _db.SaveChangesAsync();
        
        return rule;
    }

    public async Task<List<CustomDataQualityRule>> GetCustomRulesAsync(int userId, int? profileId = null, string? schema = null, string? tableName = null, bool latestVersionOnly = true)
    {
        var query = _db.CustomDataQualityRules
            .Include(r => r.Profile)
            .Where(r => r.UserId == userId && r.IsActive);

        // Por padrão, retornar apenas as versões mais recentes
        if (latestVersionOnly)
            query = query.Where(r => r.IsLatestVersion);

        if (profileId.HasValue)
            query = query.Where(r => r.ProfileId == profileId);

        if (!string.IsNullOrEmpty(schema))
            query = query.Where(r => r.Schema == schema);

        if (!string.IsNullOrEmpty(tableName))
            query = query.Where(r => r.TableName == tableName);

        return await query
            .OrderBy(r => r.Schema)
            .ThenBy(r => r.TableName)
            .ThenBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<CustomDataQualityRule?> UpdateCustomRuleAsync(int ruleId, int userId, UpdateCustomRuleDto dto)
    {
        var existingRule = await _db.CustomDataQualityRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.UserId == userId && r.IsLatestVersion);
            
        if (existingRule == null)
            return null;

        // Criar nova versão da regra
        var maxVersion = await _db.CustomDataQualityRules
            .Where(r => r.UserId == userId && r.ProfileId == existingRule.ProfileId && 
                       r.Schema == existingRule.Schema && r.TableName == existingRule.TableName && 
                       r.RuleId == existingRule.RuleId)
            .MaxAsync(r => r.Version);

        var nextVersion = maxVersion + 1;

        // Desativar versão anterior como "latest"
        existingRule.IsLatestVersion = false;
        existingRule.UpdatedAtUtc = DateTime.UtcNow;

        // Criar nova versão
        var newRule = new CustomDataQualityRule
        {
            UserId = existingRule.UserId,
            ProfileId = existingRule.ProfileId,
            Schema = existingRule.Schema,
            TableName = existingRule.TableName,
            RuleId = existingRule.RuleId,
            Version = nextVersion,
            Name = dto.Name,
            Description = dto.Description,
            Dimension = dto.Dimension,
            Column = dto.Column ?? "",
            SqlCondition = dto.SqlCondition,
            Severity = dto.Severity,
            ExpectedPassRate = dto.ExpectedPassRate,
            Source = existingRule.Source,
            Notes = dto.Notes,
            ChangeReason = dto.ChangeReason ?? "Atualização da regra",
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true,
            IsLatestVersion = true
        };

        _db.CustomDataQualityRules.Add(newRule);
        await _db.SaveChangesAsync();
        return newRule;
    }

    public async Task<bool> DeleteCustomRuleAsync(int ruleId, int userId)
    {
        var rule = await _db.CustomDataQualityRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.UserId == userId);
            
        if (rule == null)
            return false;

        // Soft delete - apenas marca como inativo
        rule.IsActive = false;
        rule.UpdatedAtUtc = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<DataQualityRuleResult> ExecuteSingleCustomRuleAsync(ConnectionProfile profile, string schema, string tableName, CustomDataQualityRule rule)
    {
        if (profile.Kind != DbKind.PostgreSql)
            throw new NotSupportedException("Apenas PostgreSQL suportado");

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = profile.HostOrFile,
            Port = profile.Port ?? 5432,
            Database = profile.Database,
            Username = profile.Username,
            Password = profile.Password ?? ""
        };

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync();
        
        var quotedTableName = $"\"{schema}\".\"{tableName}\"";
        
        // Obter total de registros
        var totalCountSql = $"SELECT COUNT(*) FROM {quotedTableName}";
        await using var totalCmd = new NpgsqlCommand(totalCountSql, conn);
        var totalRecords = Convert.ToInt64(await totalCmd.ExecuteScalarAsync());

        try
        {
            Console.WriteLine($"🔍 Executando regra customizada: {rule.Name} ({rule.RuleId})");
            Console.WriteLine($"   SQL: SELECT COUNT(*) FROM {quotedTableName} WHERE {rule.SqlCondition}");
            
            // Construir e executar SQL de validação
            var validationSql = $"SELECT COUNT(*) FROM {quotedTableName} WHERE {rule.SqlCondition}";
            
            await using var validCmd = new NpgsqlCommand(validationSql, conn);
            var validRecords = Convert.ToInt64(await validCmd.ExecuteScalarAsync());
            
            var passRate = totalRecords > 0 ? (double)validRecords / totalRecords * 100.0 : 0.0;
            var status = passRate >= rule.ExpectedPassRate ? "PASS" : "FAIL";
            
            Console.WriteLine($"   ✅ Resultado: {validRecords:N0}/{totalRecords:N0} registros válidos ({passRate:F1}%) - {status}");

            return new DataQualityRuleResult
            {
                RuleId = rule.RuleId,
                RuleName = rule.Name,
                Dimension = rule.Dimension,
                Status = status,
                PassRate = Math.Round(passRate, 2),
                ExpectedPassRate = rule.ExpectedPassRate,
                TotalRecords = totalRecords,
                ValidRecords = validRecords,
                InvalidRecords = totalRecords - validRecords,
                Severity = rule.Severity,
                ExecutedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Erro na regra {rule.Name}: {ex.Message}");
            
            return new DataQualityRuleResult
            {
                RuleId = rule.RuleId,
                RuleName = rule.Name,
                Dimension = rule.Dimension,
                Status = "ERROR",
                PassRate = 0,
                ExpectedPassRate = rule.ExpectedPassRate,
                TotalRecords = totalRecords,
                ValidRecords = 0,
                InvalidRecords = totalRecords,
                Severity = "error",
                ErrorMessage = ex.Message,
                ExecutedAt = DateTime.UtcNow
            };
        }
    }

    private List<QualityAlert> DetectQualityAlerts(List<DataQualityRuleResult> results, long totalRecords)
    {
        var alerts = new List<QualityAlert>();

        // 🚨 Alerta 1: Nenhum registro analisado
        if (totalRecords == 0)
        {
            alerts.Add(new QualityAlert
            {
                Type = "ZERO_RECORDS",
                Severity = "ERROR",
                Message = "Tabela não possui registros para análise"
            });
        }

        // 🚨 Alerta 2: Todas as regras falharam (possível problema de conectividade)
        var errorCount = results.Count(r => r.Status == "ERROR");
        if (errorCount == results.Count && results.Count > 0)
        {
            alerts.Add(new QualityAlert
            {
                Type = "ALL_RULES_FAILED",
                Severity = "CRITICAL",
                Message = $"Todas as {results.Count} regras falharam na execução. Verifique conectividade e permissões."
            });
        }

        // 🚨 Alerta 3: Alto percentual de erros de SQL
        var errorRate = results.Count > 0 ? (double)errorCount / results.Count * 100 : 0;
        if (errorRate > 50 && results.Count > 2)
        {
            alerts.Add(new QualityAlert
            {
                Type = "HIGH_ERROR_RATE",
                Severity = "WARNING",
                Message = $"{errorRate:F1}% das regras falharam na execução. Possível incompatibilidade de SQL."
            });
        }

        // 🚨 Alerta 4: 100% de registros válidos sem análise real
        var perfectRules = results.Where(r => r.Status == "PASS" && 
                                           r.PassRate == 100.0 && 
                                           r.ValidRecords == r.TotalRecords && 
                                           r.ValidRecords > 0).ToList();
        
        if (perfectRules.Count == results.Count(r => r.Status == "PASS") && 
            perfectRules.Count > 3 && 
            totalRecords > 1000)
        {
            alerts.Add(new QualityAlert
            {
                Type = "SUSPICIOUSLY_PERFECT",
                Severity = "WARNING",
                Message = $"Todas as {perfectRules.Count} regras retornaram 100% válidos. Verifique se as condições SQL estão corretas."
            });
        }

        // 🚨 Alerta 5: Registros válidos inconsistentes entre regras
        var validRecordCounts = results.Where(r => r.Status != "ERROR")
                                      .Select(r => r.ValidRecords)
                                      .Distinct()
                                      .Count();
        
        if (validRecordCounts > results.Count * 0.8 && results.Count > 5)
        {
            alerts.Add(new QualityAlert
            {
                Type = "INCONSISTENT_COUNTS",
                Severity = "INFO",
                Message = "Muita variação nos contadores de registros válidos entre regras. Isso pode indicar regras muito específicas."
            });
        }

        // 🚨 Alerta 6: Regras com zero registros válidos (possível lógica invertida)
        var zeroValidRules = results.Where(r => r.ValidRecords == 0 && r.TotalRecords > 0 && r.Status != "ERROR").ToList();
        if (zeroValidRules.Count > 0)
        {
            alerts.Add(new QualityAlert
            {
                Type = "ZERO_VALID_RECORDS",
                Severity = "WARNING", 
                Message = $"{zeroValidRules.Count} regra(s) retornaram zero registros válidos: {string.Join(", ", zeroValidRules.Select(r => r.RuleName))}"
            });
        }

        return alerts;
    }
    
    public async Task<SqlRefinementResult> RefineSqlRuleAsync(int userId, int profileId, string schema, string tableName, string ruleId, string originalSqlCondition, string errorMessage)
    {
        try
        {
            // Obter perfil do usuário
            var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId && p.UserId == userId);
            if (profile == null)
                return new SqlRefinementResult { Success = false, ErrorMessage = "Perfil não encontrado" };

            // Obter schema da tabela
            var columns = await GetTableSchemaAsync(profile, schema, tableName);

            // Obter configurações de API da IA
            var apiSettings = await _db.UserApiSettings.FirstOrDefaultAsync(s => s.UserId == userId);
            if (apiSettings == null)
                return new SqlRefinementResult { Success = false, ErrorMessage = "Configurações de API não encontradas" };

            // Chamar IA para refinamento
            var aiService = new DataQualityAI(_httpClient);
            var result = await aiService.RefineFailedRuleAsync(
                originalSqlCondition, 
                errorMessage, 
                tableName, 
                schema, 
                columns.ToList(), 
                apiSettings.ApiKeyEncrypted, // Usar a chave encriptada diretamente (assumindo desencriptação interna)
                apiSettings.Provider
            );

            return result;
        }
        catch (Exception ex)
        {
            return new SqlRefinementResult
            {
                Success = false,
                ErrorMessage = $"Erro durante refinamento: {ex.Message}",
                OriginalCondition = originalSqlCondition
            };
        }
    }
    
    private async Task<List<ColumnSchema>> GetTableSchemaAsync(ConnectionProfile profile, string schema, string tableName)
    {
        var columns = new List<ColumnSchema>();
        
        if (profile.Kind != DbKind.PostgreSql)
            return columns;

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = profile.HostOrFile,
            Port = profile.Port ?? 5432,
            Database = profile.Database,
            Username = profile.Username,
            Password = profile.Password ?? ""
        };

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync();

        var sql = @"
            SELECT column_name, data_type, is_nullable 
            FROM information_schema.columns 
            WHERE table_schema = @schema AND table_name = @table_name
            ORDER BY ordinal_position";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table_name", tableName);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnSchema
            {
                Name = reader.GetString(0), // "column_name"
                DataType = reader.GetString(1), // "data_type"
                IsNullable = reader.GetString(2) == "YES" // "is_nullable"
            });
        }

        return columns;
    }
    
    public async Task<SqlValidationResult> ValidateSqlConditionAsync(int userId, int profileId, string schema, string tableName, string sqlCondition)
    {
        try
        {
            // Obter perfil do usuário
            var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId && p.UserId == userId);
            if (profile == null)
                return new SqlValidationResult { IsValid = false, Errors = new() { "Perfil não encontrado" } };

            // Obter schema da tabela
            var columns = await GetTableSchemaAsync(profile, schema, tableName);

            // Usar parser SQL para validação avançada
            var sqlParser = new SqlParser();
            var validationResult = sqlParser.ValidateCondition(sqlCondition, columns, tableName);
            
            // Adicionar correções sugeridas
            if (!validationResult.IsValid || validationResult.Warnings.Any())
            {
                validationResult.CorrectedSql = sqlParser.SuggestCorrections(sqlCondition, columns);
            }

            return validationResult;
        }
        catch (Exception ex)
        {
            return new SqlValidationResult
            {
                IsValid = false,
                Errors = new() { $"Erro durante validação SQL: {ex.Message}" }
            };
        }
    }

    public async Task<SamplingStrategy> GetOptimalSamplingStrategyAsync(int userId, int profileId, string schema, string tableName)
    {
        try
        {
            // Obter perfil do usuário
            var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId && p.UserId == userId);
            if (profile == null)
            {
                return new SamplingStrategy 
                { 
                    SamplingType = SamplingType.RandomSample, 
                    SampleSize = 1000, 
                    Reason = "Perfil não encontrado - usando padrão" 
                };
            }

            // Usar amostragem inteligente
            return await _sampler.DetermineSamplingStrategyAsync(profile, schema, tableName);
        }
        catch (Exception ex)
        {
            return new SamplingStrategy 
            { 
                SamplingType = SamplingType.RandomSample, 
                SampleSize = 1000, 
                Reason = $"Erro ao determinar estratégia: {ex.Message}" 
            };
        }
    }

    public async Task<List<Dictionary<string, object?>>> GetIntelligentSampleAsync(int userId, int profileId, string schema, string tableName, SamplingStrategy? strategy = null)
    {
        try
        {
            // Obter perfil do usuário
            var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId && p.UserId == userId);
            if (profile == null)
                return new List<Dictionary<string, object?>>();

            // Determinar estratégia se não fornecida
            strategy ??= await _sampler.DetermineSamplingStrategyAsync(profile, schema, tableName);

            // Gerar query de amostragem otimizada
            var samplingQuery = _sampler.GenerateSamplingQuery(schema, tableName, strategy);

            // Executar query e retornar dados
            return await ExecuteSamplingQueryAsync(profile, samplingQuery);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error getting intelligent sample: {ex.Message}");
            return new List<Dictionary<string, object?>>();
        }
    }

    private async Task<List<Dictionary<string, object?>>> ExecuteSamplingQueryAsync(ConnectionProfile profile, string query)
    {
        var sampleData = new List<Dictionary<string, object?>>();
        
        if (profile.Kind != DbKind.PostgreSql)
            return sampleData;

        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = profile.HostOrFile,
            Port = profile.Port ?? 5432,
            Database = profile.Database,
            Username = profile.Username,
            Password = profile.Password ?? ""
        };

        await using var conn = new NpgsqlConnection(csb.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.CommandTimeout = 300; // 5 minutos timeout para amostras grandes
        
        await using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
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

    public async Task<DataQualityRuleResult> ExecuteCustomRuleAsync(ConnectionProfile profile, CustomDataQualityRule rule)
    {
        return await ExecuteSingleCustomRuleAsync(profile, rule.Schema, rule.TableName, rule);
    }
}

public class QualityAlert
{
    public string Type { get; set; } = "";
    public string Severity { get; set; } = ""; // CRITICAL, ERROR, WARNING, INFO
    public string Message { get; set; } = "";
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

// DTOs para gerenciamento de regras customizadas
public class CreateCustomRuleDto
{
    public string RuleId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Dimension { get; set; } = "";
    public string? Column { get; set; }
    public string SqlCondition { get; set; } = "";
    public string Severity { get; set; } = "";
    public double ExpectedPassRate { get; set; } = 95.0;
    public string? Notes { get; set; }
    public string? ChangeReason { get; set; }
}

public class UpdateCustomRuleDto
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Dimension { get; set; } = "";
    public string? Column { get; set; }
    public string SqlCondition { get; set; } = "";
    public string Severity { get; set; } = "";
    public double ExpectedPassRate { get; set; } = 95.0;
    public string? Notes { get; set; }
    public string? ChangeReason { get; set; }
}

public class ValidateRuleDto
{
    public string SqlCondition { get; set; } = "";
}

public class RefineSqlRuleRequest
{
    public string Schema { get; set; } = "";
    public string TableName { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string OriginalSqlCondition { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}

public class ValidateSqlRequest
{
    public string Schema { get; set; } = "";
    public string TableName { get; set; } = "";
    public string SqlCondition { get; set; } = "";
}

// Modelo de resultado para compatibilidade com o código existente
public class DataQualityRuleResult
{
    public string RuleId { get; set; } = "";
    public string RuleName { get; set; } = "";
    public string Dimension { get; set; } = "";
    public string Status { get; set; } = "";
    public double PassRate { get; set; }
    public double ExpectedPassRate { get; set; }
    public long TotalRecords { get; set; }
    public long ValidRecords { get; set; }
    public long InvalidRecords { get; set; }
    public string Severity { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAt { get; set; }
    public bool Success => Status == "PASS";
}