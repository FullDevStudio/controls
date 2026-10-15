<#
Runs the end-to-end UI suites, one or more platforms per line. The same arguments as ./uitest;
see tests/UITESTING.md.

  ./uitest.ps1 windows                      one platform
  ./uitest.ps1 windows android blazor       several, one after another
  ./uitest.ps1 all                          everything this OS can run, plus Blazor
  ./uitest.ps1 android --filter Slider      one control class (Slider,Ribbon for several, or a raw /*/*/X/* filter)
  ./uitest.ps1 windows --no-build           reuse the last build
  ./uitest.ps1 windows --attach --port 9223 drive an app that is already running

Platforms run in sequence. Each platform's log goes to artifacts/uitest-logs/<platform>.log.
Exits non-zero if any platform failed.
#>
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts/uitest'
$logs = Join-Path $root 'artifacts/uitest-logs'

function Show-Usage([int] $code = 0) {
    Get-Help $PSCommandPath | Out-String | Write-Host
    Write-Host 'Platforms: ios android macos linux windows blazor blazor-webkit blazor-firefox blazor-publish'
    Write-Host 'Groups:    mobile (ios android)  desktop (this OS''s desktop head)  all'
    exit $code
}

function Get-DesktopHead {
    if ($IsMacOS) { 'macos' } elseif ($IsLinux) { 'linux' } else { 'windows' }
}

$platforms = [System.Collections.Generic.List[string]]::new()
$filter = ''; $noBuild = $false; $attach = $false; $port = ''

for ($i = 0; $i -lt $args.Count; $i++) {
    switch ($args[$i]) {
        { $_ -in '-h', '--help' } { Show-Usage }
        # An unquoted `--filter A,B` reaches $args as an object[], not a string; join it back so the
        # filter stays one argument (on 5.1 a raw array also made .StartsWith('/') look true).
        { $_ -in '--filter', '-f' } { $filter = @($args[++$i]) -join ',' }
        '--no-build' { $noBuild = $true }
        '--attach' { $attach = $true }
        '--port' { $port = $args[++$i] }
        'mobile' { $platforms.Add('ios'); $platforms.Add('android') }
        'desktop' { $platforms.Add((Get-DesktopHead)) }
        'all' {
            if ($IsMacOS) { $platforms.Add('ios') }
            $platforms.Add('android'); $platforms.Add((Get-DesktopHead)); $platforms.Add('blazor')
        }
        { $_ -in 'ios', 'android', 'macos', 'linux', 'windows', 'blazor', 'blazor-webkit', 'blazor-firefox', 'blazor-publish' } { $platforms.Add($_) }
        default { Write-Host "Unknown argument: $_"; Show-Usage 1 }
    }
}
if ($platforms.Count -eq 0) { Show-Usage 1 }

# "Slider" -> /*/*/SliderTests/*, "Slider,Ribbon" -> /*/*/(SliderTests|RibbonTests)/*, raw filters pass through.
$treeFilter = ''
if ($filter) {
    if ($filter.StartsWith('/')) { $treeFilter = $filter }
    else {
        $classes = ($filter -split ',' | ForEach-Object { if ($_.EndsWith('Tests')) { $_ } else { "${_}Tests" } }) -join '|'
        if ($classes.Contains('|')) { $classes = "($classes)" }
        $treeFilter = "/*/*/$classes/*"
    }
}

New-Item -ItemType Directory -Force -Path $logs | Out-Null

function Invoke-Build([string] $project) {
    Write-Host "==> Building $project"
    $log = Join-Path $logs "build-$project.log"
    $ErrorActionPreference = 'Continue'
    dotnet build (Join-Path $root "tests/$project") --artifacts-path $artifacts *> $log
    $buildCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($buildCode -ne 0) {
        Write-Host "Build of $project failed - see $log"
        Select-String -Path $log -Pattern ' error ' | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line }
        exit 2
    }
}

if ($platforms | Where-Object { -not $_.StartsWith('blazor') }) { Invoke-Build 'Sample.UITests' }
if ($platforms | Where-Object { $_.StartsWith('blazor') }) { Invoke-Build 'Sample.Blazor.UITests' }

$envNames = 'SHINY_UITEST_PLATFORM', 'SHINY_UITEST_NOBUILD', 'SHINY_UITEST_ATTACH', 'SHINY_UITEST_PORT', 'SHINY_BLAZOR_BROWSER', 'SHINY_BLAZOR_MODE'
$summary = @()
$failedAny = $false

foreach ($platform in $platforms) {
    foreach ($name in $envNames) { Remove-Item "Env:$name" -ErrorAction SilentlyContinue }

    if ($platform.StartsWith('blazor')) {
        $dll = Join-Path $artifacts 'bin/Sample.Blazor.UITests/debug/Sample.Blazor.UITests.dll'
        switch ($platform) {
            'blazor-webkit' { $env:SHINY_BLAZOR_BROWSER = 'webkit' }
            'blazor-firefox' { $env:SHINY_BLAZOR_BROWSER = 'firefox' }
            'blazor-publish' { $env:SHINY_BLAZOR_MODE = 'publish' }
        }
    }
    else {
        $dll = Join-Path $artifacts 'bin/Sample.UITests/debug/Sample.UITests.dll'
        $env:SHINY_UITEST_PLATFORM = $platform
        if ($noBuild) { $env:SHINY_UITEST_NOBUILD = '1' }
        if ($attach) { $env:SHINY_UITEST_ATTACH = '1' }
        if ($port) { $env:SHINY_UITEST_PORT = $port }
    }

    $log = Join-Path $logs "$platform.log"
    Write-Host "==> $platform  (log: $log)"
    $testArgs = @('--progress', 'off')
    if ($treeFilter) { $testArgs += @('--treenode-filter', $treeFilter) }

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    # Windows PowerShell 5.1 turns a native command's stderr into an ErrorRecord, and under 'Stop' the
    # first warning line aborts the script before a test runs - so relax it around the native calls.
    $ErrorActionPreference = 'Continue'
    dotnet $dll @testArgs *> $log
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    $elapsed = '{0}m{1}s' -f [int]$watch.Elapsed.TotalMinutes, $watch.Elapsed.Seconds

    $content = Get-Content $log
    function Read-Count([string] $label) {
        $line = $content | Where-Object { $_ -match "^  ${label}:" } | Select-Object -First 1
        if ($line) { ($line -split ':')[1].Trim() } else { '-' }
    }
    $total = Read-Count 'total'; $passed = Read-Count 'succeeded'; $failed = Read-Count 'failed'; $skipped = Read-Count 'skipped'

    $status = 'PASS'
    if ($code -ne 0 -or ($failed -ne '-' -and $failed -ne '0')) {
        $status = 'FAIL'; $failedAny = $true
        $content | Where-Object { $_ -match '^failed ' } | Select-Object -First 10 | ForEach-Object { Write-Host "      $_" }
    }
    if ($total -eq '-') { $status = 'ERROR (no results - see log)'; $failedAny = $true }

    $summary += '{0,-16} {1,-6} total {2,-5} passed {3,-5} failed {4,-4} skipped {5,-4} {6}' -f $platform, $status, $total, $passed, $failed, $skipped, $elapsed
}

foreach ($name in $envNames) { Remove-Item "Env:$name" -ErrorAction SilentlyContinue }

Write-Host ''
Write-Host '==> Summary'
$summary | ForEach-Object { Write-Host $_ }
Write-Host "Failure screenshots/logs: $artifacts/bin/<suite>/debug/uitest-results/<platform>/failures/"

exit ([int]$failedAny)
