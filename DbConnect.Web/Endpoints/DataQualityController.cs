using DbConnect.Core.Models;
using DbConnect.Web.Services;
using DbConnect.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DbConnect.Web.Endpoints;

[ApiController]
[Route("api/data-quality")]
public class DataQualityController : ControllerBase
{
    private readonly IPatternAnalysisService _patternAnalysisService;
    private readonly AppDbContext _db;
    private readonly ILogger<DataQualityController> _logger;

    public DataQualityController(
        IPatternAnalysisService patternAnalysisService,
        AppDbContext db,
        ILogger<DataQualityController> logger)
    {
        _patternAnalysisService = patternAnalysisService;
        _db = db;
        _logger = logger;
    }

    [HttpGet("outliers")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> GetOutliers(
        [FromQuery] string tableName,
        [FromQuery] string schemaName,
        [FromQuery] string columnName,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            _logger.LogInformation("Buscando outliers para {Schema}.{Table}.{Column}, página {Page}, tamanho {PageSize}",
                schemaName, tableName, columnName, page, pageSize);

            // Buscar usuário autenticado
            var userId = GetUserId();
            if (userId == null)
            {
                _logger.LogWarning("Usuário não autenticado");
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            // Buscar perfil de conexão ativo
            var activeProfileId = HttpContext.Session.GetInt32("ActiveProfileId");
            if (!activeProfileId.HasValue)
            {
                _logger.LogWarning("Nenhum perfil de conexão ativo");
                return BadRequest(new { message = "Nenhum perfil de conexão ativo. Selecione um perfil primeiro." });
            }

            var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == activeProfileId && p.UserId == userId);
            if (profile == null)
            {
                _logger.LogWarning("Perfil de conexão não encontrado");
                return BadRequest(new { message = "Perfil de conexão não encontrado." });
            }

            var connectionString = BuildConnectionString(profile);
            _logger.LogInformation("Usando perfil de conexão: {ProfileName}", profile.Name);

            var outlierAnalysis = await _patternAnalysisService.AnalyzeColumnOutliers(
                connectionString, schemaName, tableName, columnName, page, pageSize);

            if (outlierAnalysis == null)
            {
                _logger.LogWarning("Nenhum outlier encontrado para a coluna {Column}", columnName);
                return NotFound(new { message = $"Nenhum outlier encontrado para a coluna {columnName}" });
            }

            _logger.LogInformation("Retornando {Count} outliers da página {Page}",
                outlierAnalysis.Items.Count, outlierAnalysis.CurrentPage);

            return Ok(outlierAnalysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar outliers para {Schema}.{Table}.{Column}",
                schemaName, tableName, columnName);

            return StatusCode(500, new {
                message = "Erro ao buscar outliers: " + ex.Message,
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
            DbKind.SqlServer => $"Server={profile.HostOrFile},{profile.Port};Database={profile.Database};User Id={profile.Username};Password={profile.Password};TrustServerCertificate=true;",
            DbKind.PostgreSql => $"Host={profile.HostOrFile};Port={profile.Port};Database={profile.Database};Username={profile.Username};Password={profile.Password};",
            DbKind.MySql => $"Server={profile.HostOrFile};Port={profile.Port};Database={profile.Database};Uid={profile.Username};Pwd={profile.Password};",
            _ => throw new NotSupportedException($"Tipo de conexão {profile.Kind} não suportado para análise de outliers")
        };
    }
}