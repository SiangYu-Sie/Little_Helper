$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host "[1/2] Building solution (Release)..."
dotnet build HostSimTester.slnx -c Release
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "[2/2] Publishing HostSimTester.App single-file release..."
dotnet publish HostSimTester.App/HostSimTester.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exePath = Join-Path $repoRoot "HostSimTester.App/bin/Release/net8.0-windows/win-x64/publish/HostSimTester.App.exe"
Write-Host "Release executable: $exePath"
