@echo off
rem Builds InputPlus.dll (x86). The launcher embeds it and installs it into ..\..\..\PLUGINS.
setlocal EnableDelayedExpansion
rem (delayed expansion: the "(x86)" in the path would otherwise close the for's parenthesis)
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "VSDIR="
for /f "usebackq tokens=*" %%i in (`call "!VSWHERE!" -latest -products * -property installationPath`) do set "VSDIR=%%i"
if not defined VSDIR (echo Visual Studio C++ no encontrado & exit /b 1)
call "%VSDIR%\VC\Auxiliary\Build\vcvars32.bat" >nul || exit /b 1
cd /d "%~dp0"
cl /nologo /O2 /W4 /MT /LD InputPlus.c /Fe:InputPlus.dll /link kernel32.lib user32.lib || exit /b 1
del /q InputPlus.obj InputPlus.exp InputPlus.lib 2>nul
echo OK
