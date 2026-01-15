@echo off
REM OAuth2/OIDC Demo Startup Script for Windows

echo ==================================
echo OAuth2/OIDC Demo Startup
echo ==================================
echo.

REM Check if dotnet is installed
where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: .NET SDK is not installed or not in PATH
    echo Please install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo NOTE: Ensure you have added the following to your hosts file:
echo   127.0.0.1 myidprovider.acme.com
echo.
echo Hosts file location: C:\Windows\System32\drivers\etc\hosts
echo.

echo Restoring packages...
dotnet restore "%~dp0OAuthFireBase.sln"

echo.
echo Starting Identity Server on https://myidprovider.acme.com:5001
start "Identity Server" cmd /c "cd /d %~dp0src\IdentityServer && dotnet run --launch-profile https"

REM Wait for IDP to start
timeout /t 5 /nobreak >nul

echo Starting MVC Client on https://localhost:5002
start "MVC Client" cmd /c "cd /d %~dp0src\MvcClient && dotnet run --launch-profile https"

echo.
echo Both services are starting...
echo.
echo Access points:
echo   - Identity Server: https://myidprovider.acme.com:5001
echo   - MVC Client: https://localhost:5002
echo   - Firebase App: https://localhost:5002/Home/Firebase
echo.
echo Test accounts: alice / Pass123! or bob / Pass123!
echo.
pause
