$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishDirectory = Join-Path $projectRoot "publish\win-x64"
$releaseDirectory = Join-Path $projectRoot "release"

dotnet publish (Join-Path $projectRoot "RtlTerminal.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $publishDirectory

$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue

if ($null -eq $iscc) {
    Write-Host ""
    Write-Host "Publish completed: $publishDirectory"
    Write-Host "Install Inno Setup and run this script again to create Setup.exe."
    exit 0
}

& $iscc.Source (Join-Path $projectRoot "installer\RtlTerminal.iss")
Write-Host ""
Write-Host "Installer created in: $releaseDirectory"
