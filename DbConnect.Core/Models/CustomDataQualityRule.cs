using System.ComponentModel.DataAnnotations;

namespace DbConnect.Core.Models;

public class CustomDataQualityRule
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    [Required]
    public int ProfileId { get; set; }
    
    [Required, MaxLength(100)]
    public string Schema { get; set; } = "";
    
    [Required, MaxLength(100)]
    public string TableName { get; set; } = "";
    
    [Required, MaxLength(50)]
    public string RuleId { get; set; } = "";
    
    [Required]
    public int Version { get; set; } = 1; // versioning para permitir histórico
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = "";
    
    [Required, MaxLength(500)]
    public string Description { get; set; } = "";
    
    [Required, MaxLength(50)]
    public string Dimension { get; set; } = ""; // completeness, uniqueness, validity, etc.
    
    [MaxLength(100)]
    public string Column { get; set; } = ""; // pode ser vazio para regras de tabela
    
    [Required]
    public string SqlCondition { get; set; } = ""; // WHERE condition SQL
    
    [Required, MaxLength(20)]
    public string Severity { get; set; } = ""; // error, warning, info
    
    [Required]
    public double ExpectedPassRate { get; set; } = 95.0; // % esperado de registros válidos
    
    [Required]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAtUtc { get; set; }
    
    [Required]
    public bool IsActive { get; set; } = true; // permite desativar regras sem deletar
    
    [Required]
    public bool IsLatestVersion { get; set; } = true; // flag para versão mais recente
    
    [MaxLength(20)]
    public string Source { get; set; } = "custom"; // "ai", "custom", "template"
    
    public string? Notes { get; set; } // anotações do usuário
    
    public string? ChangeReason { get; set; } // motivo da alteração/versão
    
    // Navigation properties
    public User User { get; set; } = null!;
    public ConnectionProfile Profile { get; set; } = null!;
}