$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'tests\RtlTerminal.RenderTests\RtlTerminal.RenderTests.csproj'
dotnet build $project -c Release
if ($LASTEXITCODE -ne 0) { throw 'Unable to build the local Claude UI demo.' }
$executable = Join-Path $projectRoot 'tests\RtlTerminal.RenderTests\bin\Release\net10.0-windows\RtlTerminal.RenderTests.exe'
Start-Process -FilePath $executable -ArgumentList '--claude-demo' -WorkingDirectory $projectRoot -WindowStyle Hidden
Write-Host 'Claude Code local test window opened. Responses are fixtures, not a live model. Close all demo windows to stop the local server.'
