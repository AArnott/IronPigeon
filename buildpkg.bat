@ECHO OFF

if "%1"=="" (
    ECHO USAGE: %0 version
    EXIT /b 1
)

msbuild "%~dp0IronPigeon.sln" /v:minimal /p:Configuration=Release
IF ERRORLEVEL 1 GOTO END

setlocal
SET OUTDIR=%~dp0IronPigeon\bin\Release
NuGet.exe pack "%~dp0IronPigeon.nuspec" -OutputDirectory "%OUTDIR%" -Version %1 -Symbols
IF ERRORLEVEL 1 GOTO END

@echo Package built: "%OUTDIR%\IronPigeon.%1.nupkg"
