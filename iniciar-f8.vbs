Set shell = CreateObject("WScript.Shell")
baseDir = CreateObject("Scripting.FileSystemObject").GetParentFolderName(WScript.ScriptFullName)
shell.CurrentDirectory = baseDir
shell.Run """" & baseDir & "\iniciar-f8.bat""", 0, False
