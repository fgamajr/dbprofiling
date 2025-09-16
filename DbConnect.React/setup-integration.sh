#!/bin/bash

echo "🚀 Setting up React → C# Integration..."

# Make scripts executable
chmod +x deploy-to-csharp.sh

echo "✅ Integration setup completed!"
echo ""
echo "📋 Next steps:"
echo "1. Build: npm run build (or node build-for-csharp.js)"
echo "2. Deploy: ./deploy-to-csharp.sh ../DbConnect.Web"
echo "3. Copy Controllers: cp Controllers/*.cs ../DbConnect.Web/Controllers/"
echo "4. Update Program.cs (see INTEGRATION_GUIDE.md)"
echo "5. Run: cd ../DbConnect.Web && dotnet run"
echo ""
echo "📖 Full instructions: INTEGRATION_GUIDE.md"