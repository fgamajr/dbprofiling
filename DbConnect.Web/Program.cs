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
using System.Text.Json.Serialization;
using DbConnect.Web.Endpoints; // <— para MapProfilesEndpoints()
using DbConnect.Web.AI;
using DbConnect.Web.Services;



var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()); // permite "PostgreSql" etc.
});


// 1) EF Core (SQLite local)
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var dbFile = Path.Combine(home, ".dbconnect", "app.db");
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbFile}"));

// 2) Core services
builder.Services.AddSingleton<IConnectionTester, ConnectionTester>();
builder.Services.AddHttpClient<DataQualityAI>();
builder.Services.AddTransient<DataQualityAI>();
builder.Services.AddTransient<ApiKeyService>();
builder.Services.AddHttpClient<DataQualityService>();
builder.Services.AddTransient<DataQualityTemplateService>();
builder.Services.AddTransient<IntelligentSampler>();

// Novos serviços V2
builder.Services.AddTransient<StandardMetricsService>();
builder.Services.AddTransient<PreflightService>();
builder.Services.AddTransient<SimpleTableMetricsService>();
builder.Services.AddTransient<IPatternAnalysisService, PatternAnalysisService>();

// Adicionar suporte a controllers
builder.Services.AddControllers();

// 4) CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:8080", "http://localhost:5000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Importante para cookies JWT
    });
});


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

// Adicionar suporte a sessões (precisa de um cache distribuído)
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache(); // Para desenvolvimento
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

var app = builder.Build();

// Logo após var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseCors(policy => policy
        .WithOrigins("http://localhost:8080") // React dev server
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
}




// DB init
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Directory.CreateDirectory(Path.GetDirectoryName(dbFile)!);
    // Aplicar migrações pendentes
    try
    {
        db.Database.Migrate();
        Console.WriteLine("✅ Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Migration failed: {ex.Message}");
        // Se a migração falhar, tenta EnsureCreated como backup
        db.Database.EnsureCreated();
    }
}


app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSession(); // Adicionar middleware de sessão
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
app.MapProfilesEndpoints();
app.MapDataQualityV2Endpoints(); // Novos endpoints V2
app.MapTableEssentialMetricsEndpoints(); // Métricas Essenciais
app.MapControllers(); // Registrar controllers incluindo DataQualityController

// SPA Fallback deve ser DEPOIS das rotas da API
app.MapFallbackToFile("index.html");
app.Run();

static int? GetUserId(ClaimsPrincipal user)
{
    var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    return int.TryParse(sub, out var id) ? id : null;
}
