#!/bin/bash

echo "🔧 DbConnect Development Workflow"
echo ""
echo "Choose an option:"
echo "1) Build & Deploy to C# (Production workflow)"
echo "2) Start React Dev Server (Development workflow)"
echo ""
read -p "Enter choice [1-2]: " choice

case $choice in
    1)
        echo "🚀 Building and deploying to C#..."
        ./deploy-to-csharp.sh
        ;;
    2)
        echo "🔥 Starting React development server..."
        echo "React will run on: http://localhost:8080"
        echo "C# APIs will be proxied from: https://localhost:7121"
        echo ""
        echo "Make sure your C# server is running!"
        echo "In another terminal: cd ../DbConnect.Web && dotnet run"
        echo ""
        npm run dev
        ;;
    *)
        echo "Invalid choice"
        exit 1
        ;;
esac
