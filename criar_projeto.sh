#!/bin/bash

# --- Início do Script ---

echo "🚀 Iniciando a criação da solução DbConnect..."

# 1. Limpeza (opcional, para garantir que não há resíduos de uma execução anterior)
echo "🧹 Limpando diretórios antigos..."
rm -rf DbConnect.sln DbConnect.Core/ DbConnect.Console/ DbConnect.Web/ DbConnect.Tests/

# 2. Criar a estrutura básica da solução e projetos via 'dotnet new'
echo "🏗️  Criando a estrutura com 'dotnet new'..."
dotnet new sln -n DbConnect
dotnet new classlib -n DbConnect.Core
dotnet new console  -n DbConnect.Console
dotnet new web      -n DbConnect.Web
dotnet new xunit    -n DbConnect.Tests

# 3. Adicionar projetos à solução e configurar referências
echo "🔗 Ligando os projetos e a solução..."
dotnet sln add DbConnect.Core/ DbConnect.Console/ DbConnect.Web/ DbConnect.Tests/
dotnet add DbConnect.Console reference DbConnect.Core
dotnet add DbConnect.Web     reference DbConnect.Core
dotnet add DbConnect.Tests   reference DbConnect.Core

# 4. Criar as subpastas necessárias
echo "📁 Criando subdiretórios..."
mkdir -p DbConnect.Core/Models
mkdir -p DbConnect.Core/Abstractions
mkdir -p DbConnect.Core/Services
mkdir -p DbConnect.Web/Data
mkdir -p DbConnect.Web/Auth
mkdir -p DbConnect.Web/Reports
mkdir -p DbConnect.Web/wwwroot
mkdir -p DbConnect.Tests

# --- DbConnect.Core ---
echo "✍️  Escrevendo arquivos para DbConnect.Core..."

# DbConnect.Core.csproj
cat << 'EOF' > DbConnect.Core/DbConnect.Core.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Dapper" Version="2.1.35" />
    <PackageReference Include="System.Data.SqlClient" Version="4.8.6" />
    <PackageReference Include="Npgsql" Version="8.0.3" />
    <PackageReference Include="MySqlConnector" Version="2.3.7" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.4" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
EOF

# Models/DbKind.cs
cat << 'EOF' > DbConnect.Core/Models/DbKind.cs
namespace DbConnect.Core.Models;

public enum DbKind
{
    PostgreSql,
    SqlServer,
    MySql,
    Sqlite
}
EOF

# Models/ConnectionProfile.cs
# Versão atualizada com UserId
cat << 'EOF' > DbConnect.Core/Models/ConnectionProfile.cs
namespace DbConnect.Core.Models;

public record ConnectionProfile(
    string Name,
    DbKind Kind,
    string HostOrFile,
    int? Port,
    string Database,
    string Username,
    string? Password,
    DateTime CreatedAtUtc
)
{
    public int Id { get; init; }
    public int UserId { get; init; }
}
EOF

# Models/User.cs
cat << 'EOF' > DbConnect.Core/Models/User.cs
namespace DbConnect.Core.Models;

public sealed class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ConnectionProfile> Profiles { get; set; } = new List<ConnectionProfile>();
    public ICollection<AnalysisReport> Reports { get; set; } = new List<AnalysisReport>();
}
EOF

# Models/AnalysisReport.cs
cat << 'EOF' > DbConnect.Core/Models/AnalysisReport.cs
namespace DbConnect.Core.Models;

public sealed class AnalysisReport
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Name { get; set; }
    public required string Kind { get; set; }
    public required string InputSignature { get; set; }
    public required string StoragePath { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
EOF

# Abstractions/IProfileStore.cs
cat << 'EOF' > DbConnect.Core/Abstractions/IProfileStore.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using DbConnect.Core.Models;

namespace DbConnect.Core.Abstractions;

public interface IProfileStore
{
    Task<IReadOnlyList<ConnectionProfile>> ListAsync();
    Task<ConnectionProfile?> GetAsync(string name);
    Task UpsertAsync(ConnectionProfile profile);
    Task DeleteAsync(string name);
}
EOF

# Abstractions/IConnectionTester.cs
cat << 'EOF' > DbConnect.Core/Abstractions/IConnectionTester.cs
using System.Threading.Tasks;
using DbConnect.Core.Models;

namespace DbConnect.Core.Abstractions;

public interface IConnectionTester
{
    Task<(bool ok, string message)> TestAsync(ConnectionProfile profile);
    string BuildConnectionString(ConnectionProfile profile);
}
EOF

# Services/FileProfileStore.cs
cat << 'EOF' > DbConnect.Core/Services/FileProfileStore.cs
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using DbConnect.Core.Abstractions;
using DbConnect.Core.Models;
using Newtonsoft.Json;

namespace DbConnect.Core.Services;

public sealed class FileProfileStore : IProfileStore
{
    private readonly string _path;
    private readonly object _lock = new();

    public FileProfileStore(string? path = null)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _path = path ?? Path.Combine(home, ".dbconnect", "profiles.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        if (!File.Exists(_path)) File.WriteAllText(_path, "[]");
    }

    public Task<IReadOnlyList<ConnectionProfile>> ListAsync()
    {
        var list = Read();
        return Task.FromResult<IReadOnlyList<ConnectionProfile>>(list);
    }

    public Task<ConnectionProfile?> GetAsync(string name)
    {
        var p = Read().FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(p);
    }

    public Task UpsertAsync(ConnectionProfile profile)
    {
        lock (_lock)
        {
            var list = Read().ToList();
            var idx = list.FindIndex(x => string.Equals(x.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) list[idx] = profile; else list.Add(profile);
            Write(list);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string name)
    {
        lock (_lock)
        {
            var list = Read().Where(x => !string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            Write(list);
        }
        return Task.CompletedTask;
    }

    private List<ConnectionProfile> Read()
    {
        lock (_lock)
        {
            var json = File.ReadAllText(_path);
            return JsonConvert.DeserializeObject<List<ConnectionProfile>>(json) ?? new();
        }
    }

    private void Write(List<ConnectionProfile> list)
    {
        var json = JsonConvert.SerializeObject(list, Formatting.Indented);
        File.WriteAllText(_path, json);
    }
}
EOF

# Services/ConnectionTester.cs
cat << 'EOF' > DbConnect.Core/Services/ConnectionTester.cs
using System;
using System.Data.Common;
using System.Threading.Tasks;
using DbConnect.Core.Abstractions;
using DbConnect.Core.Models;
using Dapper;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using System.Data.SqlClient;

namespace DbConnect.Core.Services;

public sealed class ConnectionTester : IConnectionTester
{
    public string BuildConnectionString(ConnectionProfile p) => p.Kind switch
    {
        DbKind.PostgreSql => new NpgsqlConnectionStringBuilder {
            Host = p.HostOrFile, Port = p.Port ?? 5432, Database = p.Database,
            Username = p.Username, Password = p.Password ?? ""
        }.ToString(),
        DbKind.SqlServer => new SqlConnectionStringBuilder {
            DataSource = $"{p.HostOrFile},{p.Port ?? 1433}",
            InitialCatalog = p.Database, UserID = p.Username, Password = p.Password ?? "",
            TrustServerCertificate = true
        }.ToString(),
        DbKind.MySql => new MySqlConnectionStringBuilder {
            Server = p.HostOrFile, Port = (uint)(p.Port ?? 3306), Database = p.Database,
            UserID = p.Username, Password = p.Password ?? ""
        }.ToString(),
        DbKind.Sqlite => new SqliteConnectionStringBuilder {
            DataSource = string.IsNullOrWhiteSpace(p.HostOrFile) ? ":memory:" : p.HostOrFile
        }.ToString(),
        _ => throw new NotSupportedException()
    };

    public async Task<(bool ok, string message)> TestAsync(ConnectionProfile profile)
    {
        try
        {
            using var conn = CreateConnection(profile);
            await conn.OpenAsync();
            var _ = await conn.ExecuteScalarAsync<object>("SELECT 1");
            await conn.CloseAsync();
            return (true, $"Conexão OK ({profile.Kind})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private DbConnection CreateConnection(ConnectionProfile p) => p.Kind switch
    {
        DbKind.PostgreSql => new NpgsqlConnection(BuildConnectionString(p)),
        DbKind.SqlServer  => new SqlConnection(BuildConnectionString(p)),
        DbKind.MySql      => new MySqlConnection(BuildConnectionString(p)),
        DbKind.Sqlite     => new SqliteConnection(BuildConnectionString(p)),
        _ => throw new NotSupportedException()
    };
}
EOF


# --- DbConnect.Console ---
echo "✍️  Escrevendo arquivos para DbConnect.Console..."

# DbConnect.Console.csproj
cat << 'EOF' > DbConnect.Console/DbConnect.Console.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\DbConnect.Core\DbConnect.Core.csproj" />
  </ItemGroup>
</Project>
EOF

# Program.cs
cat << 'EOF' > DbConnect.Console/Program.cs
using DbConnect.Core.Models;
using DbConnect.Core.Services;

var store = new FileProfileStore();
var tester = new ConnectionTester();

if (args.Length == 0)
{
    Console.WriteLine("Comandos: add | list | test");
    return;
}

switch (args[0].ToLowerInvariant())
{
    case "add":
        if (args.Length < 7)
        {
            Console.WriteLine("Uso: add <name> <kind> <hostOrFile> <port> <database> <username> [password]");
            return;
        }
        var kind = Enum.Parse<DbKind>(args[2], ignoreCase: true);
        int? port = int.TryParse(args[4], out var p) ? p : null;

        var profile = new ConnectionProfile(
            Name: args[1],
            Kind: kind,
            HostOrFile: args[3],
            Port: port,
            Database: args[5],
            Username: args[6],
            Password: args.Length >= 8 ? args[7] : null,
            CreatedAtUtc: DateTime.UtcNow
        );

        await store.UpsertAsync(profile);
        Console.WriteLine($"Perfil '{profile.Name}' salvo.");
        break;

    case "list":
        var all = await store.ListAsync();
        foreach (var it in all)
            Console.WriteLine($"{it.Name} -> {it.Kind} {it.HostOrFile}:{it.Port} DB={it.Database} USER={it.Username}");
        break;

    case "test":
        if (args.Length < 2) { Console.WriteLine("Uso: test <name>"); return; }
        var prof = await store.GetAsync(args[1]);
        if (prof is null) { Console.WriteLine("Perfil não encontrado."); return; }
        var (ok, msg) = await tester.TestAsync(prof);
        Console.WriteLine(ok ? $"✅ {msg}" : $"❌ {msg}");
        break;

    default:
        Console.WriteLine("Comando desconhecido.");
        break;
}
EOF

# --- DbConnect.Web ---
echo "✍️  Escrevendo arquivos para DbConnect.Web..."

# DbConnect.Web.csproj
cat << 'EOF' > DbConnect.Web/DbConnect.Web.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\DbConnect.Core\DbConnect.Core.csproj" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.4" />
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.4" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.5.1" />
  </ItemGroup>
</Project>
EOF

# Data/AppDbContext.cs
cat << 'EOF' > DbConnect.Web/Data/AppDbContext.cs
using DbConnect.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DbConnect.Web.Data;

public sealed class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ConnectionProfile> Profiles => Set<ConnectionProfile>();
    public DbSet<AnalysisReport> Reports => Set<AnalysisReport>();

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

        base.OnModelCreating(mb);
    }
}
EOF

# Auth/JwtOptions.cs
cat << 'EOF' > DbConnect.Web/Auth/JwtOptions.cs
namespace DbConnect.Web.Auth;

public sealed record JwtOptions(string Issuer, string Audience, string Secret, int ExpMinutes);
EOF

# Auth/AuthDtos.cs
cat << 'EOF' > DbConnect.Web/Auth/AuthDtos.cs
namespace DbConnect.Web.Auth;

public sealed record RegisterDto(string Username, string Password);
public sealed record LoginDto(string Username, string Password);
public sealed record MeDto(int Id, string Username);
EOF

# Reports/ReportDtos.cs
cat << 'EOF' > DbConnect.Web/Reports/ReportDtos.cs
namespace DbConnect.Web.Reports;

public sealed record CreateReportDto(string Name, string Kind, string InputSignature, string ContentBase64);
public sealed record ReportDto(int Id, string Name, string Kind, string InputSignature, DateTime CreatedAtUtc);
EOF

# Program.cs
cat << 'EOF' > DbConnect.Web/Program.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using DbConnect.Core.Abstractions;
using DbConnect.Core.Models;
using DbConnect.Core.Services;
using DbConnect.Web.Auth;
using DbConnect.Web.Data;
using DbConnect.Web.Reports;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1) EF Core (SQLite local)
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var dbFile = Path.Combine(home, ".dbconnect", "app.db");
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbFile}"));

// 2) Core services
builder.Services.AddSingleton<IConnectionTester, ConnectionTester>();

// 3) JWT + Cookie
var jwtOpts = new JwtOptions(
    Issuer: "DbConnect",
    Audience: "DbConnect.Local",
    Secret: "change-this-long-random-secret-at-least-32-bytes",
    ExpMinutes: 60 * 24
);
builder.Services.AddSingleton(jwtOpts);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtOpts.Issuer,
            ValidAudience = jwtOpts.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.Secret)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero
        };
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Cookies.TryGetValue("auth", out var token))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
var app = builder.Build();

// DB init
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Directory.CreateDirectory(Path.GetDirectoryName(dbFile)!);
    db.Database.Migrate(); // Usar Migrate() é mais robusto que EnsureCreated()
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// AUTH
app.MapPost("/api/auth/register", async (AppDbContext db, RegisterDto dto) =>
{
    var exists = await db.Users.AnyAsync(u => u.Username == dto.Username);
    if (exists) return Results.BadRequest(new { ok = false, message = "Usuário já existe." });

    var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
    var user = new User { Username = dto.Username, PasswordHash = hash };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/auth/login", async (AppDbContext db, JwtOptions jwt, HttpResponse res, LoginDto dto) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
    if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        return Results.Unauthorized();

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.UniqueName, user.Username)
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims,
        expires: DateTime.UtcNow.AddMinutes(jwt.ExpMinutes), signingCredentials: creds);
    var jwtString = new JwtSecurityTokenHandler().WriteToken(token);

    res.Cookies.Append("auth", jwtString, new CookieOptions
    {
        HttpOnly = true, Secure = false, SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddMinutes(jwt.ExpMinutes)
    });

    return Results.Ok(new { ok = true });
});

app.MapPost("/api/auth/logout", (HttpResponse res) =>
{
    res.Cookies.Delete("auth");
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/auth/me", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var uid = GetUserId(user);
    if (uid is null) return Results.Unauthorized();

    var me = await db.Users.Where(u => u.Id == uid).Select(u => new MeDto(u.Id, u.Username)).FirstOrDefaultAsync();
    return me is null ? Results.Unauthorized() : Results.Ok(me);
}).RequireAuthorization();

// PROFILES
app.MapGet("/api/u/profiles", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var uid = GetUserId(user);
    if (uid is null) return Results.Unauthorized();

    var items = await db.Profiles
        .Where(p => EF.Property<int>(p, "UserId") == uid)
        .Select(p => new {
            p.Id, p.Name, p.Kind, p.HostOrFile, p.Port, p.Database, p.Username, p.CreatedAtUtc
        })
        .ToListAsync();
    return Results.Ok(items);
}).RequireAuthorization();

app.MapPost("/api/u/profiles", async (AppDbContext db, IConnectionTester tester, ClaimsPrincipal user, ConnectionProfile input) =>
{
    var uid = GetUserId(user);
    if (uid is null) return Results.Unauthorized();

    var (ok, msg) = await tester.TestAsync(input);
    if (!ok) return Results.BadRequest(new { ok, message = msg });

    var entity = input with { CreatedAtUtc = DateTime.UtcNow };
    db.Add(entity);
    db.Entry(entity).Property("UserId").CurrentValue = uid.Value;

    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true, id = entity.Id, message = "Salvo e testado com sucesso." });
}).RequireAuthorization();

// REPORTS
var reportsRoot = Path.Combine(home, ".dbconnect", "reports");
Directory.CreateDirectory(reportsRoot);

app.MapGet("/api/u/reports", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var uid = GetUserId(user);
    if (uid is null) return Results.Unauthorized();
    var list = await db.Reports.Where(r => r.UserId == uid)
        .OrderByDescending(r => r.CreatedAtUtc)
        .Select(r => new ReportDto(r.Id, r.Name, r.Kind, r.InputSignature, r.CreatedAtUtc))
        .ToListAsync();
    return Results.Ok(list);
}).RequireAuthorization();

app.MapPost("/api/u/reports", async (AppDbContext db, ClaimsPrincipal user, CreateReportDto dto) =>
{
    var uid = GetUserId(user);
    if (uid is null) return Results.Unauthorized();

    var existing = await db.Reports.FirstOrDefaultAsync(r =>
        r.UserId == uid && r.Kind == dto.Kind && r.InputSignature == dto.InputSignature);
    if (existing is not null)
        return Results.Ok(new { ok = true, id = existing.Id, cached = true });

    var userDir = Path.Combine(reportsRoot, uid.Value.ToString());
    Directory.CreateDirectory(userDir);
    var id = Guid.NewGuid().ToString("N");
    var path = Path.Combine(userDir, $"{id}.bin");
    var bytes = Convert.FromBase64String(dto.ContentBase64);
    await File.WriteAllBytesAsync(path, bytes);

    var entity = new AnalysisReport
    {
        UserId = uid.Value, Name = dto.Name, Kind = dto.Kind,
        InputSignature = dto.InputSignature, StoragePath = path
    };
    db.Reports.Add(entity);
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true, id = entity.Id, cached = false });
}).RequireAuthorization();

app.MapGet("/api/u/reports/{id:int}/download", async (AppDbContext db, ClaimsPrincipal user, int id) =>
{
    var uid = GetUserId(user);
    if (uid is null) return Results.Unauthorized();
    var r = await db.Reports.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid);
    if (r is null) return Results.NotFound();
    var bytes = await File.ReadAllBytesAsync(r.StoragePath);
    return Results.File(bytes, "application/octet-stream", $"{r.Name}-{r.Id}.bin");
}).RequireAuthorization();

app.MapGet("/health", () => "ok");
app.Run();

static int? GetUserId(ClaimsPrincipal user)
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    return int.TryParse(sub, out var id) ? id : null;
}
EOF

# wwwroot/index.html
cat << 'EOF' > DbConnect.Web/wwwroot/index.html
<!doctype html>
<html lang="pt-br">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>DbConnect (local)</title>
  <style>
    body{font-family:system-ui,Segoe UI,Roboto,Helvetica,Arial,sans-serif;margin:2rem;max-width:900px;background:#f9f9f9;color:#333}
    input,select,textarea{padding:.5rem;margin:.25rem 0;width:100%;box-sizing:border-box;border:1px solid #ccc;border-radius:4px}
    button{padding:.6rem 1rem;margin-top:.5rem;border:0;border-radius:4px;background:#007bff;color:#fff;cursor:pointer}
    button:hover{background:#0056b3}
    .card{border:1px solid #ddd;border-radius:8px;padding:1rem;margin:1rem 0;background:#fff;box-shadow:0 2px 4px rgba(0,0,0,.05)}
    .row{display:grid;grid-template-columns:1fr 1fr;gap:1rem}
    .ok{color:#28a745;font-weight:bold}
    .err{color:#dc3545;font-weight:bold}
    small{color:#666}
    h1,h3{color:#0056b3}
    label{display:block;margin-top:.5rem;font-weight:bold}
  </style>
</head>
<body>
  <h1>DbConnect (local)</h1>

  <div class="card" id="auth-card">
    <h3>Autenticação</h3>
    <div id="me" style="margin-bottom:1rem;font-weight:bold;"></div>
    <div id="login-form">
      <div class="row">
        <div><label>Usuário</label><input id="u_user"></div>
        <div><label>Senha</label><input id="u_pass" type="password"></div>
      </div>
      <button onclick="register()">Registrar</button>
      <button onclick="login()">Entrar</button>
    </div>
    <button onclick="logout()" style="display:none;" id="logout-btn">Sair</button>
  </div>

  <div id="main-content" style="display:none;">
    <div class="card">
      <h3>Novo perfil de conexão</h3>
      <div class="row">
        <div>
          <label>Nome</label><input id="name" />
          <label>Tipo</label>
          <select id="kind">
            <option>PostgreSql</option><option>SqlServer</option><option>MySql</option><option>Sqlite</option>
          </select>
          <label>Host/Arquivo</label><input id="host" />
        </div>
        <div>
          <label>Porta (vazio p/ padrão)</label><input id="port" type="number" />
          <label>Database</label><input id="db" />
          <label>Usuário</label><input id="user" />
          <label>Senha</label><input id="pass" type="password" />
        </div>
      </div>
      <button onclick="saveProfile()">Salvar Perfil (com teste)</button>
      <div id="status"></div>
    </div>

    <div class="card">
      <h3>Meus Perfis</h3>
      <div id="list"></div>
    </div>

    <div class="card">
      <h3>Relatórios</h3>
      <div class="row">
        <div>
          <h4>Novo Relatório</h4>
          <label>Nome</label><input id="rep_name" />
          <label>Tipo (Kind)</label><input id="rep_kind" placeholder="table_profile" />
          <label>Assinatura (InputSignature)</label><input id="rep_sig" placeholder="hash dos parâmetros" />
          <label>Conteúdo (Base64)</label><textarea id="rep_b64" rows="4" style="width:100%"></textarea>
          <button onclick="saveReport()">Salvar relatório</button>
          <div id="rep_status"></div>
        </div>
        <div>
          <h4>Meus relatórios</h4>
          <div id="rep_list"></div>
        </div>
      </div>
    </div>
  </div>

<script>
// Elementos
const u_user = document.getElementById('u_user');
const u_pass = document.getElementById('u_pass');
const meEl = document.getElementById('me');
const loginForm = document.getElementById('login-form');
const logoutBtn = document.getElementById('logout-btn');
const mainContent = document.getElementById('main-content');
const rep_list = document.getElementById('rep_list');

// Auth
async function register(){
  const r = await fetch('/api/auth/register',{method:'POST',headers:{'Content-Type':'application/json'},
    body: JSON.stringify({username: u_user.value, password: u_pass.value})});
  alert(r.ok ? 'Usuário registrado com sucesso!' : 'Falha ao registrar.');
}
async function login(){
  const r = await fetch('/api/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},
    body: JSON.stringify({username: u_user.value, password: u_pass.value})});
  if(r.ok) {
    alert('Login realizado com sucesso!');
    await checkAuth();
  } else {
    alert('Usuário ou senha inválidos.');
  }
}
async function logout(){
  await fetch('/api/auth/logout',{method:'POST'});
  meEl.innerText = '';
  alert('Logout OK');
  await checkAuth();
}
async function checkAuth(){
  const r = await fetch('/api/auth/me');
  if(r.ok) {
    const data = await r.json();
    meEl.innerText = `Conectado como: ${data.username}`;
    meEl.className = 'ok';
    loginForm.style.display = 'none';
    logoutBtn.style.display = 'block';
    mainContent.style.display = 'block';
    loadProfiles();
    loadReports();
  } else {
    meEl.innerText = 'Não autenticado. Por favor, faça o login ou registre-se.';
    meEl.className = 'err';
    loginForm.style.display = 'block';
    logoutBtn.style.display = 'none';
    mainContent.style.display = 'none';
  }
}

// Profiles
async function saveProfile(){
  const body = {
    name:  document.getElementById('name').value.trim(),
    kind:  document.getElementById('kind').value,
    hostOrFile: document.getElementById('host').value.trim(),
    port:  document.getElementById('port').value ? parseInt(document.getElementById('port').value) : null,
    database: document.getElementById('db').value.trim(),
    username: document.getElementById('user').value.trim(),
    password: document.getElementById('pass').value
  };
  const r = await fetch('/api/u/profiles', {method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify(body)});
  const data = await r.json();
  const s = document.getElementById('status');
  s.className = data.ok ? 'ok' : 'err';
  s.innerText = data.message || JSON.stringify(data);
  await loadProfiles();
}
async function loadProfiles(){
  const r = await fetch('/api/u/profiles');
  if(!r.ok){ document.getElementById('list').innerHTML='<p class="err">Faça login para ver seus perfis.</p>'; return; }
  const arr = await r.json();
  const el = document.getElementById('list');
  el.innerHTML = arr.length === 0 ? '<p>Nenhum perfil salvo.</p>' : '';
  arr.forEach(p => {
    const d = document.createElement('div');
    d.className='card';
    d.innerHTML = `<b>${p.name}</b> <small>(${p.kind})</small><br/>
      <code>${p.hostOrFile}${p.port?':'+p.port:''} / DB=${p.database} / USER=${p.username}</code>`;
    el.appendChild(d);
  });
}

// Reports
async function saveReport(){
  const body = {
    name: document.getElementById('rep_name').value,
    kind: document.getElementById('rep_kind').value,
    inputSignature: document.getElementById('rep_sig').value,
    contentBase64: document.getElementById('rep_b64').value
  };
  const r = await fetch('/api/u/reports',{method:'POST',headers:{'Content-Type':'application/json'}, body: JSON.stringify(body)});
  const d = await r.json();
  document.getElementById('rep_status').innerText = r.ok ? `Relatório salvo! (Cache usado: ${d.cached})` : 'Erro ao salvar.';
  await loadReports();
}
async function loadReports(){
  const r = await fetch('/api/u/reports');
  if(!r.ok){ rep_list.innerHTML='<p class="err">Faça login para ver relatórios.</p>'; return; }
  const arr = await r.json();
  rep_list.innerHTML= arr.length === 0 ? '<p>Nenhum relatório salvo.</p>' : '';
  arr.forEach(it=>{
    const div = document.createElement('div');
    div.innerHTML = `<div class="card">
      <b>${it.name}</b> <small>(${it.kind})</small><br/>
      Assinatura: <code>${it.inputSignature}</code><br/>
      Criado: ${new Date(it.createdAtUtc).toLocaleString()}<br/>
      <a href="/api/u/reports/${it.id}/download" target="_blank">Baixar</a>
      </div>`;
    rep_list.appendChild(div);
  });
}

// Initial load
(async() => { await checkAuth(); })();
</script>
</body>
</html>
EOF

# --- DbConnect.Tests ---
echo "✍️  Escrevendo arquivos para DbConnect.Tests..."

# DbConnect.Tests.csproj
cat << 'EOF' > DbConnect.Tests/DbConnect.Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="xunit" Version="2.7.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.7">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="DotNet.Testcontainers" Version="3.8.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\DbConnect.Core\DbConnect.Core.csproj" />
  </ItemGroup>
</Project>
EOF

# PostgresFixture.cs
cat << 'EOF' > DbConnect.Tests/PostgresFixture.cs
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace DbConnect.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public IContainer Container { get; private set; } = default!;
    public string Host => "localhost";
    public int Port { get; private set; }
    public string User => "test_user";
    public string Pass => "test_password";
    public string Db   => "test_db";

    public async Task InitializeAsync()
    {
        var container = new ContainerBuilder()
            .WithImage("postgres:15-alpine")
            .WithEnvironment("POSTGRES_DB", Db)
            .WithEnvironment("POSTGRES_USER", User)
            .WithEnvironment("POSTGRES_PASSWORD", Pass)
            .WithPortBinding(0, true) // Bind to a random available port
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await container.StartAsync();
        Container = container;
        Port = container.GetMappedPublicPort(5432);
    }

    public async Task DisposeAsync() => await Container.DisposeAsync();
}
EOF

# ConnectionTesterTests.cs
cat << 'EOF' > DbConnect.Tests/ConnectionTesterTests.cs
using System;
using System.Threading.Tasks;
using DbConnect.Core.Models;
using DbConnect.Core.Services;
using Xunit;

namespace DbConnect.Tests;

public class ConnectionTesterTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;
    public ConnectionTesterTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Connects_To_Postgres_Container()
    {
        var profile = new ConnectionProfile(
            Name: "pg-test",
            Kind: DbKind.PostgreSql,
            HostOrFile: _fx.Host,
            Port: _fx.Port,
            Database: _fx.Db,
            Username: _fx.User,
            Password: _fx.Pass,
            CreatedAtUtc: DateTime.UtcNow
        );

        var tester = new ConnectionTester();
        var (ok, msg) = await tester.TestAsync(profile);
        Assert.True(ok, msg);
    }
}
EOF

echo "✅ Projeto criado com sucesso!"
echo "Para começar, execute 'dotnet restore' e depois 'dotnet build'."

# --- Fim do Script ---