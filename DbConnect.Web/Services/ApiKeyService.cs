using System.Security.Cryptography;
using System.Text;
using DbConnect.Web.Data;
using DbConnect.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DbConnect.Web.Services;

public class ApiKeyService
{
    private readonly AppDbContext _db;
    private readonly string _encryptionKey;

    public ApiKeyService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _encryptionKey = config["Encryption:Key"] ?? "default-key-change-in-production-32-chars";
    }

    public async Task<bool> SaveApiKeyAsync(int userId, string provider, string apiKey)
    {
        try
        {
            var encryptedKey = EncryptApiKey(apiKey);
            
            // Remover configuração anterior do mesmo provider
            var existing = await _db.UserApiSettings
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == provider);
            
            if (existing != null)
            {
                existing.ApiKeyEncrypted = encryptedKey;
                existing.IsActive = true;
                existing.LastValidatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                _db.UserApiSettings.Add(new UserApiSettings
                {
                    UserId = userId,
                    Provider = provider,
                    ApiKeyEncrypted = encryptedKey,
                    IsActive = true,
                    LastValidatedAtUtc = DateTime.UtcNow
                });
            }
            
            await _db.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GetApiKeyAsync(int userId, string provider)
    {
        var settings = await _db.UserApiSettings
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == provider && x.IsActive);

        if (settings == null) return null;

        return DecryptApiKey(settings.ApiKeyEncrypted);
    }

    public async Task<string?> GetDecryptedApiKeyAsync(int userId, string provider)
    {
        return await GetApiKeyAsync(userId, provider);
    }

    public async Task<bool> ValidateApiKeyAsync(int userId, string provider, string apiKey)
    {
        try
        {
            // Teste básico - fazer uma chamada simples à API
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var testRequest = new
            {
                model = provider == "openai" ? "gpt-3.5-turbo" : "claude-3-haiku",
                messages = new[] { new { role = "user", content = "Hi" } },
                max_tokens = 5
            };

            var json = System.Text.Json.JsonSerializer.Serialize(testRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var endpoint = provider == "openai" 
                ? "https://api.openai.com/v1/chat/completions"
                : "https://api.anthropic.com/v1/messages";
            
            var response = await client.PostAsync(endpoint, content);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> HasValidApiKeyAsync(int userId, string provider)
    {
        var settings = await _db.UserApiSettings
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Provider == provider && x.IsActive);
            
        return settings != null && settings.LastValidatedAtUtc.HasValue;
    }

    private string EncryptApiKey(string apiKey)
    {
        var key = Encoding.UTF8.GetBytes(_encryptionKey.PadRight(32).Substring(0, 32));
        var iv = new byte[16];
        RandomNumberGenerator.Fill(iv);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var plainTextBytes = Encoding.UTF8.GetBytes(apiKey);
        var encryptedBytes = encryptor.TransformFinalBlock(plainTextBytes, 0, plainTextBytes.Length);

        var result = new byte[iv.Length + encryptedBytes.Length];
        Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, iv.Length, encryptedBytes.Length);

        return Convert.ToBase64String(result);
    }

    private string DecryptApiKey(string encryptedApiKey)
    {
        var key = Encoding.UTF8.GetBytes(_encryptionKey.PadRight(32).Substring(0, 32));
        var fullCipher = Convert.FromBase64String(encryptedApiKey);

        var iv = new byte[16];
        var cipher = new byte[fullCipher.Length - 16];

        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(fullCipher, iv.Length, cipher, 0, cipher.Length);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }
}