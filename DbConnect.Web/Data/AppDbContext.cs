using DbConnect.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DbConnect.Web.Data;

public sealed class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ConnectionProfile> Profiles => Set<ConnectionProfile>();
    public DbSet<AnalysisReport> Reports => Set<AnalysisReport>();
    public DbSet<UserApiSettings> UserApiSettings => Set<UserApiSettings>();
    public DbSet<DataQualityAnalysis> DataQualityAnalyses => Set<DataQualityAnalysis>();
    public DbSet<DataQualityResult> DataQualityResults => Set<DataQualityResult>();
    public DbSet<CustomDataQualityRule> CustomDataQualityRules => Set<CustomDataQualityRule>();

    // Novas tabelas de métricas e pré-voo
    public DbSet<TableMetric> TableMetrics => Set<TableMetric>();
    public DbSet<ColumnMetric> ColumnMetrics => Set<ColumnMetric>();
    public DbSet<PreflightResult> PreflightResults => Set<PreflightResult>();
    public DbSet<RuleCandidate> RuleCandidates => Set<RuleCandidate>();
    public DbSet<RuleExecution> RuleExecutions => Set<RuleExecution>();

    // Métricas Essenciais da Tabela
    public DbSet<TableEssentialMetrics> TableEssentialMetrics => Set<TableEssentialMetrics>();
    public DbSet<ColumnEssentialMetrics> ColumnEssentialMetrics => Set<ColumnEssentialMetrics>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>()
          .HasIndex(x => x.Username).IsUnique();

        mb.Entity<ConnectionProfile>()
          .HasKey(x => x.Id);

        mb.Entity<ConnectionProfile>()
          .Property<int>("UserId");

        mb.Entity<ConnectionProfile>()
          .HasOne<User>()
          .WithMany(u => u.Profiles)
          .HasForeignKey("UserId")
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<AnalysisReport>()
          .HasIndex(x => new { x.UserId, x.Kind, x.InputSignature })
          .IsUnique();

        mb.Entity<UserApiSettings>()
          .HasKey(x => x.Id);

        mb.Entity<UserApiSettings>()
          .HasOne(x => x.User)
          .WithMany()
          .HasForeignKey(x => x.UserId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<UserApiSettings>()
          .HasIndex(x => new { x.UserId, x.Provider })
          .IsUnique();

        // Data Quality Analysis
        mb.Entity<DataQualityAnalysis>()
          .HasKey(x => x.Id);

        mb.Entity<DataQualityAnalysis>()
          .HasOne(x => x.User)
          .WithMany()
          .HasForeignKey(x => x.UserId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<DataQualityAnalysis>()
          .HasOne(x => x.Profile)
          .WithMany()
          .HasForeignKey(x => x.ProfileId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<DataQualityAnalysis>()
          .HasMany(x => x.Results)
          .WithOne(r => r.Analysis)
          .HasForeignKey(r => r.AnalysisId)
          .OnDelete(DeleteBehavior.Cascade);

        // Data Quality Result  
        mb.Entity<DataQualityResult>()
          .HasKey(x => x.Id);

        // Custom Data Quality Rules
        mb.Entity<CustomDataQualityRule>()
          .HasKey(x => x.Id);

        mb.Entity<CustomDataQualityRule>()
          .HasOne(x => x.User)
          .WithMany()
          .HasForeignKey(x => x.UserId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<CustomDataQualityRule>()
          .HasOne(x => x.Profile)
          .WithMany()
          .HasForeignKey(x => x.ProfileId)
          .OnDelete(DeleteBehavior.Cascade);

        // UK composta: usuário + banco + tabela + regra + versão (permite múltiplas versões da mesma regra)
        mb.Entity<CustomDataQualityRule>()
          .HasIndex(x => new { x.UserId, x.ProfileId, x.Schema, x.TableName, x.RuleId, x.Version })
          .IsUnique()
          .HasDatabaseName("IX_CustomDataQualityRule_UniqueRule");

        // UK para garantir que apenas uma versão por regra seja a mais recente (IsLatestVersion = true)
        mb.Entity<CustomDataQualityRule>()
          .HasIndex(x => new { x.UserId, x.ProfileId, x.Schema, x.TableName, x.RuleId, x.IsLatestVersion })
          .IsUnique()
          .HasDatabaseName("IX_CustomDataQualityRule_UniqueLatestVersion")
          .HasFilter("IsLatestVersion = 1"); // apenas versões ativas podem ser únicas

        // Table Essential Metrics
        mb.Entity<TableEssentialMetrics>()
          .HasKey(x => x.Id);

        mb.Entity<TableEssentialMetrics>()
          .HasOne(x => x.User)
          .WithMany()
          .HasForeignKey(x => x.UserId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<TableEssentialMetrics>()
          .HasOne(x => x.Profile)
          .WithMany()
          .HasForeignKey(x => x.ProfileId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<TableEssentialMetrics>()
          .HasMany(x => x.ColumnMetrics)
          .WithOne(c => c.TableMetrics)
          .HasForeignKey(c => c.TableMetricsId)
          .OnDelete(DeleteBehavior.Cascade);

        // Índice único para evitar duplicatas (apenas uma análise mais recente por tabela)
        mb.Entity<TableEssentialMetrics>()
          .HasIndex(x => new { x.UserId, x.ProfileId, x.Schema, x.TableName })
          .HasDatabaseName("IX_TableEssentialMetrics_UniqueTable");

        // Column Essential Metrics
        mb.Entity<ColumnEssentialMetrics>()
          .HasKey(x => x.Id);

        base.OnModelCreating(mb);
    }
}
