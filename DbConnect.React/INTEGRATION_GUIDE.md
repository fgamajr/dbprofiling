# 🔧 React → C# wwwroot Integration Guide

## ✅ O que foi configurado:

### 1. **Vite Configuration** (`vite.config.ts`)
- ✅ Build otimizado para produção 
- ✅ Paths relativos para integração com wwwroot
- ✅ Proxy para desenvolvimento local
- ✅ Chunks otimizados (vendor + UI)

### 2. **Build Scripts**
- ✅ `build-for-csharp.js` - Build automatizado
- ✅ `deploy-to-csharp.sh` - Deploy automático para wwwroot
- ✅ Scripts npm adicionados

### 3. **Controllers Template** 
- ✅ `AuthController.cs` - Autenticação (/api/auth/*)
- ✅ `ProfilesController.cs` - Perfis (/api/u/profiles)
- ✅ `ReportsController.cs` - Relatórios (/api/u/reports)

## 🚀 Como usar:

### **Desenvolvimento Local:**
```bash
# Terminal 1: React (desenvolvimento)
npm run dev

# Terminal 2: C# API (ajustar porta se necessário)
cd DbConnect.Web
dotnet run
```

### **Deploy para Produção:**
```bash
# Opção 1: Build + Deploy automático
npm run build:csharp
./deploy-to-csharp.sh ../DbConnect.Web

# Opção 2: Manual
npm run build
cp -r dist/* ../DbConnect.Web/wwwroot/
```

## 📋 Próximos passos no seu projeto C#:

### 1. **Atualizar Program.cs:**
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy => policy.WithOrigins("http://localhost:8080")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowReactApp");
}

app.UseStaticFiles();          // Serve React build
app.MapControllers();          // API routes
app.MapFallbackToFile("index.html"); // SPA fallback

app.Run();
```

### 2. **Copiar Controllers:**
```bash
# Copie os arquivos Controllers/*.cs para DbConnect.Web/Controllers/
cp Controllers/*.cs ../DbConnect.Web/Controllers/
```

### 3. **Implementar lógica real:**
- Substituir mock data por implementação real
- Conectar com DbConnect.Core services
- Implementar autenticação JWT
- Adicionar validações e error handling

## 🔌 Estrutura final:
```
DbConnect.Web/
├── Controllers/
│   ├── AuthController.cs
│   ├── ProfilesController.cs 
│   └── ReportsController.cs
├── wwwroot/           # ← React build aqui
│   ├── index.html
│   ├── assets/
│   └── ...
└── Program.cs         # ← Configurado para SPA
```

## 🎯 Resultado:
- ✅ React app servido via C# (https://localhost:7001)
- ✅ APIs funcionando nas rotas corretas
- ✅ SPA routing funcionando
- ✅ Development workflow otimizado