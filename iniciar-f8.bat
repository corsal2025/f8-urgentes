@echo off
cd /d "%~dp0src\F8Urgentes"
start "" cmd /k dotnet run --urls "http://localhost:5080"
timeout /t 5 /nobreak >nul
start "" http://localhost:5080
