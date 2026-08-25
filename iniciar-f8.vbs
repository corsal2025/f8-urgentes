Set WshShell = CreateObject("WScript.Shell")
exePath = "C:\Users\raul.salazar\Desktop\PROYECTOS RAUL\F8\src\F8Urgentes\bin\Debug\net10.0\F8Urgentes.exe"
workDir = "C:\Users\raul.salazar\Desktop\PROYECTOS RAUL\F8\src\F8Urgentes\bin\Debug\net10.0"
WshShell.Run "powershell -WindowStyle Hidden -ExecutionPolicy Bypass -Command ""if (-not (Get-Process -Name F8Urgentes -ErrorAction SilentlyContinue)) { Start-Process -FilePath '""" & exePath & """' -WorkingDirectory '""" & workDir & """' -WindowStyle Hidden; Start-Sleep -Milliseconds 1200 }; Start-Process 'http://localhost:5080'""", 0, False
