@echo off
chcp 65001 >nul
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo Для запуска из исходников нужен .NET 10 SDK. Команда dotnet не найдена.
  pause
  exit /b 1
)
echo Сборка RadarPlacement...
dotnet build "src\RadarPlacement.Console\RadarPlacement.Console.csproj" --configuration Release --disable-build-servers -m:1 --nologo --verbosity quiet
if errorlevel 1 (
  echo Сборка не удалась. Проверьте сообщения выше и наличие .NET 10 SDK.
  pause
  exit /b 1
)
dotnet "src\RadarPlacement.Console\bin\Release\net10.0\RadarPlacement.Console.dll" %*
exit /b %errorlevel%
