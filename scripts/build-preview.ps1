param([string] $Output = 'artifacts/preview')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    dotnet publish src/RtlTerminal/RtlTerminal.csproj -c Release -p:EnableCliTestLab=true --no-self-contained -o $Output
    if ($LASTEXITCODE -ne 0) { throw 'Preview build failed.' }
    Write-Host "Preview ready: $Output/RtlTerminal.exe"
    Write-Host 'Tools > CLI Test Lab opens local CLI test sessions.'
} finally { Pop-Location }
