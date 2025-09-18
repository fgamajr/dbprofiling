using DbConnect.Core.Models;
using DbConnect.Web.Data;
using DbConnect.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DbConnect.Web.Endpoints;

[ApiController]
[Route("api/schema-discovery")]
public class SchemaDiscoveryController : ControllerBase
{
    private readonly IPostgreSQLSchemaDiscoveryService _schemaDiscoveryService;
    private readonly AppDbContext _db;
    private readonly ILogger<SchemaDiscoveryController> _logger;

    public SchemaDiscoveryController(
        IPostgreSQLSchemaDiscoveryService schemaDiscoveryService,
        AppDbContext db,
        ILogger<SchemaDiscoveryController> logger)
    {
        _schemaDiscoveryService = schemaDiscoveryService;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Descoberta automática completa do schema PostgreSQL
    /// Baseada no pg-mcp-server para mapear todo o banco automaticamente
    /// </summary>
    [HttpPost("discover")]
    [Authorize]
    public async Task<IActionResult> DiscoverSchema()
    {
        try
        {
            _logger.LogInformation("Iniciando descoberta automática de schema");

            // Buscar usuário autenticado
            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            // Buscar perfil de conexão ativo
            var activeProfileId = HttpContext.Session.GetInt32("ActiveProfileId");
            if (!activeProfileId.HasValue)
            {
                return BadRequest(new { message = "Nenhum perfil de conexão ativo. Selecione um perfil primeiro." });
            }

            var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == activeProfileId && p.UserId == userId);
            if (profile == null)
            {
                return BadRequest(new { message = "Perfil de conexão não encontrado." });
            }

            // Verificar se é PostgreSQL
            if (profile.Kind != DbKind.PostgreSql)
            {
                return BadRequest(new { message = "Descoberta automática disponível apenas para PostgreSQL." });
            }

            var connectionString = BuildConnectionString(profile);
            _logger.LogInformation("Executando descoberta para perfil: {ProfileName}", profile.Name);

            // Executar descoberta automática
            var schema = await _schemaDiscoveryService.DiscoverCompleteSchemaAsync(connectionString);

            var response = new
            {
                success = true,
                message = "Descoberta de schema concluída com sucesso",
                profile = new
                {
                    name = profile.Name,
                    database = profile.Database
                },
                discovery = new
                {
                    discoveredAt = schema.DiscoveredAt,
                    databaseName = schema.DatabaseName,
                    summary = new
                    {
                        totalTables = schema.Tables.Count,
                        totalColumns = schema.Tables.Sum(t => t.ColumnCount),
                        declaredForeignKeys = schema.ForeignKeys.Count,
                        implicitRelations = schema.ImplicitRelations.Count,
                        relevantRelations = schema.RelevantRelations.Count
                    }
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
                        columns = t.Columns.Select(c => new
                        {
                            name = c.ColumnName,
                            dataType = c.DataType,
                            isNullable = c.IsNullable,
                            isPrimaryKey = c.IsPrimaryKey,
                            isForeignKey = c.IsForeignKey
                        })
                    }),
                    foreignKeys = schema.ForeignKeys.Select(fk => new
                    {
                        source = new
                        {
                            table = fk.SourceFullName,
                            column = fk.SourceColumn
                        },
                        target = new
                        {
                            table = fk.TargetFullName,
                            column = fk.TargetColumn
                        },
                        constraintName = fk.ConstraintName
                    }),
                    implicitRelations = schema.ImplicitRelations.Select(ir => new
                    {
                        source = new
                        {
                            table = ir.SourceTable,
                            column = ir.SourceColumn
                        },
                        target = new
                        {
                            table = ir.TargetTable,
                            column = ir.TargetColumn
                        },
                        confidence = ir.ConfidenceScore,
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
                        confidence = rr.ConfidenceLevel,
                        validationOpportunities = rr.ValidationOpportunities
                    })
                }
            };

            _logger.LogInformation("Descoberta concluída: {Tables} tabelas, {FKs} FKs, {Implicit} implícitos",
                schema.Tables.Count, schema.ForeignKeys.Count, schema.ImplicitRelations.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro durante descoberta de schema");
            return StatusCode(500, new
            {
                success = false,
                message = "Erro durante descoberta de schema: " + ex.Message,
                details = ex.InnerException?.Message
            });
        }
    }

    private int? GetUserId()
    {
        var userIdClaim = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                          HttpContext.User.FindFirstValue("sub");
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private static string BuildConnectionString(ConnectionProfile profile)
    {
        return profile.Kind switch
        {
            DbKind.PostgreSql => $"Host={profile.HostOrFile};Port={profile.Port};Database={profile.Database};Username={profile.Username};Password={profile.Password};",
            _ => throw new NotSupportedException($"Descoberta de schema não suportada para {profile.Kind}")
        };
    }
}