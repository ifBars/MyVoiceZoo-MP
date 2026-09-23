[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameDirectory,
    [Parameter(Mandatory)][string]$GseDll,
    [Parameter(Mandatory)][string]$TestRoot,
    [string]$HostSteamId = '76561198000040001',
    [string]$ClientSteamId = '76561198000040002',
    [string]$Scenario = '',
    [int]$TimeoutSeconds = 120,
    [switch]$SkipBuild,
    [switch]$Rendered,
    [switch]$LobbyChat,
    [switch]$Microphone,
    [switch]$Motion,
    [switch]$Settings,
    [switch]$PlanOnly
)

$ErrorActionPreference = 'Stop'
if (($Microphone -or $Motion) -and $Scenario -ne 'shared-zoo') { throw '-Microphone and -Motion require -Scenario shared-zoo.' }
if ($Settings -and -not $Rendered) { throw '-Settings requires -Rendered for visual inspection.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$game = [IO.Path]::GetFullPath($GameDirectory)
$gse = [IO.Path]::GetFullPath($GseDll)
$root = [IO.Path]::GetFullPath($TestRoot)
$dll = Join-Path $repo 'bin\Release\net6.0\MVZ.MP.dll'
$save = Join-Path $env:USERPROFILE 'AppData\LocalLow\DefaultCompany\MyVoiceZoo'
$exe = Join-Path $game 'MyVoiceZoo.exe'
$nativeRelative = 'MyVoiceZoo_Data\Plugins\x86_64\steam_api64.dll'
$expectedGseHash = 'EF32F9BB1FEF9E9B58F3EA06F88B2EA1E206C861A4D0431D287E537C59A1A391'

function Assert-Outside([string]$candidate, [string]$protected) {
    $path = [IO.Path]::GetFullPath($candidate).TrimEnd('\', '/')
    $boundary = [IO.Path]::GetFullPath($protected).TrimEnd('\', '/')
    if ($path.Equals($boundary, [StringComparison]::OrdinalIgnoreCase) -or
        $path.StartsWith($boundary + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        $boundary.StartsWith($path + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "TestRoot and protected path overlap: $path / $boundary"
    }
}
function Read-Log([string]$path) {
    if (Test-Path -LiteralPath $path -PathType Leaf) { $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite); $reader = [IO.StreamReader]::new($stream); try { return $reader.ReadToEnd() } finally { $reader.Dispose() } }
    return ''
}
function Wait-Log([string]$path, [string]$pattern, [string]$phase, [System.Diagnostics.Process[]]$processes) {
    $end = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $end) {
        foreach ($other in @($hostLog, $clientLog)) {
            if ($other -and (Read-Log $other) -match 'FAIL\|[^\r\n]+') { throw "FAIL|$phase|$($Matches[0])" }
        }
        $value = Read-Log $path
        if ($value -match 'FAIL\|[^\r\n]+') { throw "FAIL|$phase|$($Matches[0])" }
        $match = [regex]::Match($value, $pattern)
        if ($match.Success) { Write-Host "PASS|$phase|$($match.Value)"; return $match }
        foreach ($process in $processes) {
            $process.Refresh()
            if ($process.HasExited) { throw "FAIL|$phase|Process $($process.Id) exited with code $($process.ExitCode)." }
        }
        Start-Sleep -Milliseconds 500
    }
    throw "FAIL|$phase|Timed out after $TimeoutSeconds seconds. See retained logs."
}
function Write-Identity([string]$install, [string]$name, [string]$steamId) {
    $plugin = Join-Path $install 'MyVoiceZoo_Data\Plugins\x86_64'
    $settings = Join-Path $plugin 'steam_settings'
    if (Test-Path -LiteralPath $settings) { throw "Refusing to replace preexisting steam_settings in $install" }
    New-Item -ItemType Directory -Path $settings | Out-Null
    [IO.File]::WriteAllText((Join-Path $settings 'configs.user.ini'), "[user::general]`naccount_name=$name`naccount_steamid=$steamId`nlanguage=english`n")
    [IO.File]::WriteAllText((Join-Path $plugin 'steam_appid.txt'), "4015530`n")
    [IO.File]::WriteAllText((Join-Path $install 'steam_appid.txt'), "4015530`n")
}
function Copy-Install([string]$destination, [string]$name, [string]$steamId) {
    # A fresh unique destination avoids any delete or /MIR operation.
    Copy-Item -LiteralPath $game -Destination $destination -Recurse
    if (-not (Test-Path -LiteralPath (Join-Path $destination 'MyVoiceZoo.exe') -PathType Leaf)) { throw "Copied install is incomplete: $destination" }
    $pluginDll = Join-Path $destination $nativeRelative
    Copy-Item -LiteralPath $pluginDll -Destination "$pluginDll.original"
    Copy-Item -LiteralPath $gse -Destination $pluginDll -Force
    New-Item -ItemType Directory -Path (Join-Path $destination 'Mods') -Force | Out-Null
    Copy-Item -LiteralPath $dll -Destination (Join-Path $destination 'Mods\MVZ.MP.dll') -Force
    if ((Get-FileHash -LiteralPath (Join-Path $destination 'Mods\MVZ.MP.dll') -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash) { throw "Installed mod DLL differs from build: $destination" }
    Write-Identity $destination $name $steamId
    # Remove only the stale log in this fresh disposable copy.
    $copiedLog = Join-Path $destination 'MelonLoader\Latest.log'
    if (Test-Path -LiteralPath $copiedLog) { Remove-Item -LiteralPath $copiedLog -Force }
}

if (-not [IO.Path]::IsPathRooted($TestRoot)) { throw 'TestRoot must be an absolute path.' }
Assert-Outside $root $repo
Assert-Outside $root $game
Assert-Outside $root $save
if ($HostSteamId -eq $ClientSteamId -or $HostSteamId -notmatch '^7656119\d{10}$' -or $ClientSteamId -notmatch '^7656119\d{10}$') { throw 'Two distinct 17-digit Steam IDs are required.' }
if ($Scenario -and $Scenario -notmatch '^[A-Za-z0-9_-]+$') { throw 'Scenario must contain only letters, digits, underscore or hyphen.' }
if ($TimeoutSeconds -lt 10) { throw 'TimeoutSeconds must be at least 10.' }
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "Game executable missing: $exe" }
if (-not (Test-Path -LiteralPath (Join-Path $game $nativeRelative) -PathType Leaf)) { throw 'Game native Steam DLL missing.' }
if (-not (Test-Path -LiteralPath $gse -PathType Leaf)) { throw "GSE DLL missing: $gse" }
$actualHash = (Get-FileHash -LiteralPath $gse -Algorithm SHA256).Hash
if ($actualHash -ne $expectedGseHash) { throw "Unexpected GSE DLL hash: $actualHash" }
$existing = @(Get-CimInstance Win32_Process -Filter "Name = 'MyVoiceZoo.exe'")
if ($existing.Count -gt 0) { throw "MyVoiceZoo is already running (PID $($existing.ProcessId -join ', ')). Close it before testing." }
if (Test-Path -LiteralPath (Join-Path $game 'MyVoiceZoo_Data\Plugins\x86_64\steam_settings')) { throw 'Source game already contains steam_settings; cannot create clean GSE identities.' }
if ($PlanOnly) {
    Write-Output "PLAN|game=$game|gse=$actualHash|testRoot=$root|host=$HostSteamId|client=$ClientSteamId|scenario=$Scenario|microphone=$Microphone|motion=$Motion|settings=$Settings"
    return
}

if (-not $SkipBuild) {
    & dotnet build (Join-Path $repo 'MVZ.MP.csproj') -c Release "-p:GameDir=$game"
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
}
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw "Mod DLL missing: $dll" }

$run = Join-Path $root ("run-" + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
$hostInstall = Join-Path $run 'host'
$client = Join-Path $run 'client'
$evidence = Join-Path $run 'evidence'
$originalSave = Join-Path (Split-Path -Parent $save) ("MyVoiceZoo.mvzmp-backup-" + [guid]::NewGuid().ToString('N'))
$saveWasPresent = Test-Path -LiteralPath $save
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$saveIsGuarded = $false
$result = 'FAIL'
New-Item -ItemType Directory -Path $run, $evidence | Out-Null
try {
    Write-Output "Run: $run"
    Write-Output "User-data backup: $originalSave"
    if ($saveWasPresent) { Move-Item -LiteralPath $save -Destination $originalSave }
    $saveIsGuarded = $true
    Copy-Install $hostInstall 'MVZMP-host' $HostSteamId
    Copy-Install $client 'MVZMP-client' $ClientSteamId
    $hostArgs = @('--mvzmp-host', '-batchmode', '-nographics')
    $clientArgs = @('-batchmode', '-nographics')
    if ($Rendered) { $hostArgs = @('--mvzmp-host', '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720'); $clientArgs = @('-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720') }
    if ($LobbyChat) { $hostArgs += '--mvzmp-lobby-chat'; $clientArgs += '--mvzmp-lobby-chat' }
    if ($Microphone) { $hostArgs += '--mvzmp-smoke-microphone'; $clientArgs += '--mvzmp-smoke-microphone' }
    if ($Motion) { $hostArgs += '--mvzmp-smoke-motion'; $clientArgs += '--mvzmp-smoke-motion' }
    if ($Settings) { $hostArgs += '--mvzmp-smoke-settings'; $clientArgs += '--mvzmp-smoke-settings' }
    if ($Scenario) {
        $hostArgs += "--mvzmp-smoke=$Scenario"
        $clientArgs += "--mvzmp-smoke=$Scenario"
    }
    # Quote the absolute per-process data paths as individual Windows arguments.
    $hostData = Join-Path $run 'host-data'
    $clientData = Join-Path $run 'client-data'
    New-Item -ItemType Directory -Path $hostData, $clientData | Out-Null
    $hostArgs += "`"--mvzmp-data-dir=$hostData`""
    $clientArgs += "`"--mvzmp-data-dir=$clientData`""
    $hostLog = Join-Path $hostInstall 'MelonLoader\Latest.log'
    $clientLog = Join-Path $client 'MelonLoader\Latest.log'
    $hostProcess = Start-Process -FilePath (Join-Path $hostInstall 'MyVoiceZoo.exe') -WorkingDirectory $hostInstall -ArgumentList $hostArgs -WindowStyle Hidden -PassThru
    $processes.Add($hostProcess)
    Wait-Log $hostLog 'Loaded\. F6 host, F7 invite, F8 leave\.' 'host-mod-loaded' @($hostProcess) | Out-Null
    $hostSteam = Wait-Log $hostLog "Steam ready; local Steam ID ($HostSteamId)" 'host-steam' @($hostProcess)
    $created = Wait-Log $hostLog 'Created lobby (\d+)' 'host-created' @($hostProcess)
    $lobbyId = $created.Groups[1].Value
    Wait-Log $hostLog "Joined lobby $lobbyId; owner=$HostSteamId; members=1; role=host" 'host-entered' @($hostProcess) | Out-Null
    if ($Scenario -eq 'shared-zoo') { $clientArgs += "--mvzmp-smoke-lobby=$lobbyId" }
    else { $clientArgs += @('+connect_lobby', $lobbyId) }
    $clientProcess = Start-Process -FilePath (Join-Path $client 'MyVoiceZoo.exe') -WorkingDirectory $client -ArgumentList $clientArgs -WindowStyle Hidden -PassThru
    $processes.Add($clientProcess)
    Wait-Log $clientLog 'Loaded\. F6 host, F7 invite, F8 leave\.' 'client-mod-loaded' @($hostProcess, $clientProcess) | Out-Null
    Wait-Log $clientLog "Steam ready; local Steam ID ($ClientSteamId)" 'client-steam' @($hostProcess, $clientProcess) | Out-Null
    Wait-Log $clientLog "Joined lobby $lobbyId; owner=$HostSteamId; members=([2-9]|[1-9][0-9]+); role=guest" 'client-entered' @($hostProcess, $clientProcess) | Out-Null
    Wait-Log $hostLog "COOP PEER_READY $ClientSteamId" 'host-members' @($hostProcess, $clientProcess) | Out-Null
    Wait-Log $hostLog "COOP PEER_READY $ClientSteamId" 'host-handshake' @($hostProcess, $clientProcess) | Out-Null
    Wait-Log $clientLog 'COOP SYNC_READY revision=' 'client-handshake' @($hostProcess, $clientProcess) | Out-Null

    if ($Scenario) {
        Wait-Log $hostLog "PASS\|$([regex]::Escape($Scenario))\|" 'host-scenario' @($hostProcess, $clientProcess) | Out-Null
        Wait-Log $clientLog "PASS\|$([regex]::Escape($Scenario))\|" 'client-scenario' @($hostProcess, $clientProcess) | Out-Null
    }
    if ($Motion) { Wait-Log $hostLog 'PASS\|native-motion\|' 'native-motion' @($hostProcess, $clientProcess) | Out-Null }
    if ($Microphone) { Wait-Log $clientLog 'PASS\|guest-microphone-capture\|' 'guest-microphone' @($hostProcess, $clientProcess) | Out-Null }
    if ($Settings) {
        Wait-Log $hostLog 'PASS\|settings-hidden\|' 'host-settings' @($hostProcess, $clientProcess) | Out-Null
        Wait-Log $clientLog 'PASS\|settings-hidden\|' 'client-settings' @($hostProcess, $clientProcess) | Out-Null
    }
    $result = 'PASS'
    Write-Output "PASS|gse-coop|lobby=$lobbyId|run=$run"
} catch {
    Write-Error -ErrorAction Continue $_
    throw
} finally {
    foreach ($process in $processes) {
        try {
            $process.Refresh()
            if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction Stop; $process.WaitForExit(10000) | Out-Null }
        } catch { Write-Warning "Could not stop launched PID $($process.Id): $_" }
    }
    foreach ($pair in @(@($hostInstall, 'host'), @($client, 'client'))) {
        $latest = Join-Path $pair[0] 'MelonLoader\Latest.log'
        if (Test-Path -LiteralPath $latest) { Copy-Item -LiteralPath $latest -Destination (Join-Path $evidence "$($pair[1])-Latest.log") }
    }
    if ($saveIsGuarded) {
        if (Test-Path -LiteralPath $save) {
            Move-Item -LiteralPath $save -Destination (Join-Path $evidence 'test-created-user-data')
        }
        if ($saveWasPresent) { Move-Item -LiteralPath $originalSave -Destination $save }
    }
    [pscustomobject]@{
        Result = $result; Run = $run; HostSteamId = $HostSteamId; ClientSteamId = $ClientSteamId
        GseSha256 = $actualHash; Scenario = $Scenario; SaveRestored = $saveIsGuarded
        Microphone = [bool]$Microphone; Motion = [bool]$Motion; Settings = [bool]$Settings
        Evidence = $evidence; TimeUtc = [DateTime]::UtcNow.ToString('o')
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'result.json')
    Write-Output "Evidence retained: $evidence"
}
