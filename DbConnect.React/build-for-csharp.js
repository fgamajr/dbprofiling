#!/usr/bin/env node

const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

console.log('🚀 Building React app for C# wwwroot integration...\n');

try {
  // Clean previous build
  if (fs.existsSync('dist')) {
    fs.rmSync('dist', { recursive: true });
    console.log('✅ Cleaned previous build');
  }

  // Build the React app
  console.log('📦 Building React application...');
  execSync('npm run build', { stdio: 'inherit' });

  // Create instructions file
  const instructions = `
# 📋 Integration Instructions

## 1. Copy files to C# project:
\`\`\`bash
# From your React project root, run:
cp -r dist/* ../DbConnect.Web/wwwroot/
\`\`\`

## 2. Update your DbConnect.Web/Program.cs:
\`\`\`csharp
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:8080", "https://localhost:8080")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("AllowReactApp");
}

app.UseHttpsRedirection();

// Serve static files (React build)
app.UseStaticFiles();

// API routes
app.MapControllers();

// SPA fallback - return index.html for non-API routes
app.MapFallbackToFile("index.html");

app.Run();
\`\`\`

## 3. Create API Controllers (if not exists):
Create controllers for:
- /api/auth/* (AuthController)
- /api/u/profiles (ProfilesController) 
- /api/u/reports (ReportsController)

## 4. Run your C# application:
\`\`\`bash
cd DbConnect.Web
dotnet run
\`\`\`

Your React app will now be served from https://localhost:7001 (or your configured port)
`;

  fs.writeFileSync('INTEGRATION_INSTRUCTIONS.md', instructions);
  console.log('\n✅ Build completed successfully!');
  console.log('📄 Check INTEGRATION_INSTRUCTIONS.md for next steps');
  
} catch (error) {
  console.error('❌ Build failed:', error.message);
  process.exit(1);
}