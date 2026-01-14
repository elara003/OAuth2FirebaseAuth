#!/bin/bash

# OAuth2/OIDC Demo Startup Script
# This script starts both the Identity Server and MVC Client

echo "=================================="
echo "OAuth2/OIDC Demo Startup"
echo "=================================="
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK is not installed or not in PATH"
    echo "Please install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

# Check hosts file
if ! grep -q "recruiting.ultipro.com" /etc/hosts 2>/dev/null; then
    echo "WARNING: recruiting.ultipro.com not found in /etc/hosts"
    echo "Please add the following line to your hosts file:"
    echo "  127.0.0.1 recruiting.ultipro.com"
    echo ""
fi

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo "Restoring packages..."
dotnet restore "$SCRIPT_DIR/OAuthFireBase.sln"

echo ""
echo "Starting Identity Server on https://recruiting.ultipro.com:5001"
echo "Starting MVC Client on https://localhost:5002"
echo ""
echo "Press Ctrl+C to stop all services"
echo ""

# Start Identity Server in background
cd "$SCRIPT_DIR/src/IdentityServer"
dotnet run --launch-profile https &
IDP_PID=$!

# Give IDP time to start
sleep 3

# Start MVC Client in background
cd "$SCRIPT_DIR/src/MvcClient"
dotnet run --launch-profile https &
MVC_PID=$!

# Function to cleanup on exit
cleanup() {
    echo ""
    echo "Shutting down..."
    kill $IDP_PID 2>/dev/null
    kill $MVC_PID 2>/dev/null
    exit 0
}

trap cleanup SIGINT SIGTERM

# Wait for both processes
wait
