#!/usr/bin/env bash
set -euo pipefail

BRANCH="feat/profiles-crud"
WEB_DIR="DbConnect.Web"
ENDPOINTS_DIR="$WEB_DIR/Endpoints"
ENDPOINTS_FILE="$ENDPOINTS_DIR/ProfilesEndpoints.cs"
PROGRAM_CS="$WEB_DIR/Program.cs"

echo "==> Verificando repositório git..."
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || { echo "Este diretório não é um repo git."; exit 1; }

# garante working tree limpa
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Há mudanças não commitadas. Faça commit/stash antes."
  exit 1
fi

echo "==> Criando branch $BRANCH (se ainda não existir)..."
if git show-ref --quiet refs/heads/"$BRANCH"; then
  git checkout "$BRANCH"
else
  git checkout -b "$BRANCH"
fi

echo "==> Criando pasta $ENDPOINTS_DIR ..."
mkdir -p "$ENDPOINTS_DIR"

echo "==> Escrevendo $ENDPOINTS_FILE ..."
cat > "$ENDPOINTS_FILE" <<'CS'
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DbConnect.Web.Data;
using DbConnect.Core.Models;

namespace DbConnect.Web.Endpoints;

public static class ProfilesEndpoints
{
    public record ProfileCreateDto(
        string Name, DbKind Kind, string HostOrFile, int? Port,
        string Database, string Username, string? Password
    );

    public record ProfileUpdateDto(
        string Name, DbKind Kind, string HostOrFile, int? Port,
        string Database, string Username, string? Password // null => mantém
    );

    public static IEndpointRouteBuilder MapProfilesEndpoints(this IEndpointRouteBuilder app)
    {
        var u = app.MapGroup("/api/u").RequireAuthorization();

        // Criar (salva + testa)
        u.MapPost("/profiles", async (ProfileCreateDto input, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var entity = new ConnectionProfile
            {
                Name = input.Name.Trim(),
                Kind = input.Kind,
                HostOrFile = input.HostOrFile.Trim(),
                Port = input.Port,
                Database = input.Database.Trim(),
                Username = input.Username.Trim(),
                Password = string.IsNullOrEmpty(input.Password) ? null : input.Password,
                CreatedAtUtc = DateTime.UtcNow,
                UserId = uid
            };

            var (ok, err) = await TestConnectionAsync(entity);
            if (!ok) return Results.BadRequest(new { message = err });

            db.Profiles.Add(entity);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Salvo e testado com sucesso.", id = entity.Id });
        });

        // Editar (atualiza + testa)
        u.MapPut("/profiles/{id:int}", async (int id, ProfileUpdateDto input, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var p = await db.Profiles.FindAsync(id);
            if (p is null || p.UserId != uid) return Results.NotFound(new { message = "Perfil não encontrado." });

            p.Name = input.Name.Trim();
            p.Kind = input.Kind;
            p.HostOrFile = input.HostOrFile.Trim();
            p.Port = input.Port;
            p.Database = input.Database.Trim();
            p.Username = input.Username.Trim();
            if (!string.IsNullOrEmpty(input.Password)) p.Password = input.Password;

            var (ok, err) = await TestConnectionAsync(p);
            if (!ok) return Results.BadRequest(new { message = err });

            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Atualizado e testado com sucesso." });
        });

        // Apagar
        u.MapDelete("/profiles/{id:int}", async (int id, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var p = await db.Profiles.FindAsync(id);
            if (p is null || p.UserId != uid) return Results.NotFound(new { message = "Perfil não encontrado." });

            db.Profiles.Remove(p);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Perfil removido." });
        });

        // Testar sem salvar (ou botão "Conectar")
        u.MapPost("/profiles/{id:int}/test", async (int id, AppDbContext db, HttpContext http) =>
        {
            var uid = int.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var p = await db.Profiles.FindAsync(id);
            if (p is null || p.UserId != uid) return Results.NotFound(new { message = "Perfil não encontrado." });

            var (ok, err) = await TestConnectionAsync(p);
            return ok ? Results.Ok(new { message = "Conexão OK." }) : Results.BadRequest(new { message = err });
        });

        return app;
    }

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
}
CS

# garante JsonStringEnumConverter no Program.cs
if ! grep -q "JsonStringEnumConverter" "$PROGRAM_CS"; then
  echo "==> Injetando JsonStringEnumConverter em $PROGRAM_CS ..."
  # Insere após a linha do builder (primeira ocorrência)
  awk '
    BEGIN{done=0}
    /var[ \t]+builder[ \t]*=[ \t]*WebApplication\.CreateBuilder/ && done==0 {
      print $0
      print ""
      print "builder.Services.ConfigureHttpJsonOptions(o =>"
      print "{"
      print "    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());"
      print "});"
      done=1
      next
    }
    {print $0}
  ' "$PROGRAM_CS" > "$PROGRAM_CS.tmp" && mv "$PROGRAM_CS.tmp" "$PROGRAM_CS"
else
  echo "==> JsonStringEnumConverter já presente."
fi

# garante chamada MapProfilesEndpoints();
if ! grep -q "MapProfilesEndpoints" "$PROGRAM_CS"; then
  echo "==> Adicionando chamada a MapProfilesEndpoints() antes de app.Run() ..."
  awk '
    BEGIN{done=0}
    /app\.Run\(\);/ && done==0 {
      print "app.MapProfilesEndpoints();"
      print $0
      done=1
      next
    }
    {print $0}
  ' "$PROGRAM_CS" > "$PROGRAM_CS.tmp" && mv "$PROGRAM_CS.tmp" "$PROGRAM_CS"
else
  echo "==> MapProfilesEndpoints() já está em Program.cs."
fi

echo "==> git add/commit ..."
git add "$ENDPOINTS_FILE" "$PROGRAM_CS"
git commit -m "feat(web): CRUD de perfis (criar/editar/apagar/testar) via Minimal API e JsonStringEnumConverter"

echo "==> Build para validar..."
dotnet build

echo
echo "Pronto! 🌟"
echo "- Branch atual: $BRANCH"
echo "- Endpoints adicionados:"
echo "  POST   /api/u/profiles              (criar + testar)"
echo "  PUT    /api/u/profiles/{id}         (editar + testar)"
echo "  DELETE /api/u/profiles/{id}         (apagar)"
echo "  POST   /api/u/profiles/{id}/test    (testar sem salvar)"
echo
echo "Rode: ./run-dev.sh up  e teste pela UI/React."
