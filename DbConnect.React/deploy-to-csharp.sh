#!/bin/bash

# Deploy React build to C# wwwroot
CSHARP_PROJECT_PATH="../DbConnect.Web"
WWWROOT_PATH="$CSHARP_PROJECT_PATH/wwwroot"

echo "🚀 Building React app..."
npm run build

if [ $? -ne 0 ]; then
    echo "❌ React build failed"
    exit 1
fi

echo "🧹 Cleaning wwwroot..."
rm -rf "$WWWROOT_PATH"/*

echo "📁 Copying build files to wwwroot..."
cp -r dist/* "$WWWROOT_PATH/"

if [ $? -eq 0 ]; then
    echo "✅ Deploy completed!"
    echo "📍 Files deployed to: $WWWROOT_PATH"
    echo ""
    echo "Next: cd ../DbConnect.Web && dotnet run"
else
    echo "❌ Deploy failed"
    exit 1
fi

