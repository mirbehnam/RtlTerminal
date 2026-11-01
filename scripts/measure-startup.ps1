param(
    [Parameter(Mandatory = $true)][string]$Executable,
    [ValidateRange(1, 10)][int]$Runs = 3,
    [ValidateRange(5, 120)][int]$TimeoutSeconds = 30,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\startup-performance')
)
$ErrorActionPreference = 'Stop'
$application = (Resolve-Path -LiteralPath $Executable).Path
$destination = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($destination) | Out-Null
$results = @()
for ($trial = 1; $trial -le $Runs; $trial++) {
    $log = Join-Path $destination ((Get-Date -Format 'yyyyMMdd-HHmmssfff') + "-run-$trial.jsonl")
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $application -ArgumentList @('--performance-probe', '--performance-log', ('"' + $log + '"')) -PassThru
    $ready = $false
    while (!$process.HasExited -and $watch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if (Test-Path -LiteralPath $log) {
            try { $ready = [IO.File]::ReadAllText($log).Contains('"stage":"Terminal.FirstOutput"') } catch [IO.IOException] { }
        }
        if ($ready) { break }
        Start-Sleep -Milliseconds 100
    }
    $observed = $watch.Elapsed.TotalMilliseconds
    if (!$process.WaitForExit(5000)) {
        # Graceful window close only; never kill a shell or an unrelated instance.
        $process.Refresh()
        $null = $process.CloseMainWindow()
        if (!$process.WaitForExit(5000)) { throw "The test window did not close. Close it manually; no process was killed. Log: $log" }
    }
    if (!(Test-Path -LiteralPath $log)) { throw "No timing log was produced: $log" }
    $events = @(Get-Content -LiteralPath $log | ForEach-Object { $_ | ConvertFrom-Json })
    $rendered = $events | Where-Object stage -eq 'MainWindow.ContentRendered' | Select-Object -First 1
    $output = $events | Where-Object stage -eq 'Terminal.FirstOutput' | Select-Object -First 1
    $results += [pscustomobject]@{
        Run = $trial; FirstOutputObserved = ($null -ne $output)
        FirstWindowMs = $rendered.processElapsedMs; FirstShellOutputMs = $output.processElapsedMs
        LauncherObservedMs = [Math]::Round($observed, 1); Log = $log
    }
}
$results | Export-Csv -LiteralPath (Join-Path $destination 'summary.csv') -NoTypeInformation -Encoding UTF8
$results | Format-Table -AutoSize
Write-Host 'First output is not a ready shell prompt. Runs after the first use warm OS/runtime/font caches; this script does not flush them.'
