@echo off
cd /d "%~dp0"
start "F8 Urgentes" http://localhost:5209
dotnet run --project src\F8Urgentes\F8Urgentes.csproj --launch-profile http
