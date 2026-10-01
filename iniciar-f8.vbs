Set WshShell = CreateObject("WScript.Shell")
exePath = "C:\Users\raul.salazar\Desktop\PROYECTOS RAUL\F8\src\F8Urgentes\bin\Release\net10.0\F8Urgentes.exe"
workDir = "C:\Users\raul.salazar\Desktop\PROYECTOS RAUL\F8\src\F8Urgentes\bin\Release\net10.0"
dbPath = "C:\Users\raul.salazar\Desktop\PROYECTOS RAUL\F8\src\F8Urgentes\bin\Debug\net10.0\data\f8urgentes.db"
psCmd = "$env:F8__SqliteDbPath = '" & dbPath & "'; if (-not (Get-Process -Name F8Urgentes -ErrorAction SilentlyContinue)) { Start-Process -FilePath '" & exePath & "' -WorkingDirectory '" & workDir & "' -WindowStyle Hidden; $retries = 20; while ($retries -gt 0) { Start-Sleep -Milliseconds 300; try { $res = Invoke-WebRequest -Uri 'http://localhost:5080/Login' -UseBasicParsing -TimeoutSec 1; if ($res.StatusCode -eq 200) { break } } catch { }; $retries-- } }; Start-Process 'http://localhost:5080'"
WshShell.Run "powershell -WindowStyle Hidden -ExecutionPolicy Bypass -Command """ & psCmd & """", 0, False
