@echo off
echo Compiling src\Program.cs into CustomizableClock.exe...
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /out:CustomizableClock.exe /target:winexe /reference:System.dll,System.Drawing.dll /optimize src\Program.cs
if %ERRORLEVEL% EQU 0 (
    echo Compilation successful!
) else (
    echo Compilation failed!
    exit /b %ERRORLEVEL%
)
