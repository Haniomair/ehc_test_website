<#
.SYNOPSIS
  Builds the site for Linux and deploys it over SSH to an Ubuntu server (e.g. an Oracle Cloud Ampere VM).

.DESCRIPTION
  Connection details are read from deploy/oracle.env (not committed; copy oracle.env.example) or from environment
  variables of the same name. Uses the ssh, scp and tar commands built into Windows 10/11 (and Linux/macOS).

  The server's own data is never touched by a normal deploy: umbraco/Data (database), umbraco/Logs,
  umbraco/mediacache, wwwroot/media and appsettings.*.local.json are kept.

.EXAMPLE
  ./deploy/deploy-oracle.ps1 -Setup               # once: prepare the server (runtime, Caddy, service, firewall)
  ./deploy/deploy-oracle.ps1 -Settings            # upload src/EHC.Web/appsettings.Staging.local.json
  ./deploy/deploy-oracle.ps1 -Code -SeedData      # first deploy: code, then the local database and media
  ./deploy/deploy-oracle.ps1                      # build + deploy code
  ./deploy/deploy-oracle.ps1 -Media               # upload new/changed media files (nothing is deleted)
  ./deploy/deploy-oracle.ps1 -Rollback            # put the previous code back
#>
[CmdletBinding()]
param(
    [switch]$Setup,          # prepare the server (safe to repeat; also updates the runtime and Caddy)
    [switch]$SkipFrontend,   # reuse the assets already in src/EHC.Web/wwwroot/assets
    [switch]$Settings,       # upload the server settings file
    [switch]$SeedData,       # overwrite the server's umbraco/Data and wwwroot/media with the local copies
    [string]$DataFrom,       # with -SeedData: folder to upload as umbraco/Data (e.g. a snapshot of a running site)
    [switch]$Media,          # upload new and changed files in wwwroot/media; never deletes files on the server
    [switch]$Code,           # with -Setup, -Settings, -SeedData or -Media: also build and deploy the code
    [switch]$Rollback        # restore the code from the previous deploy
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$web = Join-Path $root 'src\EHC.Web'
$out = Join-Path $web 'bin\Oracle\publish'
$work = Join-Path ([IO.Path]::GetTempPath()) 'ehc-oracle'
New-Item -ItemType Directory -Force $work | Out-Null

# --- connection details ------------------------------------------------------------------------------------
$envFile = Join-Path $PSScriptRoot 'oracle.env'
if (Test-Path $envFile) {
    foreach ($line in Get-Content $envFile) {
        if ($line -match '^\s*([A-Z_]+)\s*=\s*(.*?)\s*$' -and -not [Environment]::GetEnvironmentVariable($Matches[1])) {
            [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2])
        }
    }
}
$server = $env:EHC_ORACLE_HOST
$user = if ($env:EHC_ORACLE_USER) { $env:EHC_ORACLE_USER } else { 'ubuntu' }
$key = $env:EHC_ORACLE_KEY
$domain = $env:EHC_ORACLE_DOMAIN
$runtime = if ($env:EHC_ORACLE_RUNTIME) { $env:EHC_ORACLE_RUNTIME } else { 'linux-arm64' }
if (-not $server) { throw 'Set EHC_ORACLE_HOST in deploy/oracle.env (see oracle.env.example).' }
if ($key -and -not (Test-Path $key)) { throw "SSH key not found: $key" }

$sshArgs = @('-o', 'StrictHostKeyChecking=accept-new', '-o', 'ServerAliveInterval=30')
if ($key) { $sshArgs += @('-i', $key) }
$target = "$user@$server"
$onWindows = $env:OS -eq 'Windows_NT'
$tar = if ($onWindows) { Join-Path $env:SystemRoot 'System32\tar.exe' } else { 'tar' }

function Invoke-Remote([string]$command) {
    ssh @sshArgs $target $command
    if ($LASTEXITCODE) { throw "Server command failed (exit code $LASTEXITCODE): $command" }
}
function Send-File([string[]]$files) {
    scp @sshArgs @files "${target}:/tmp/"
    if ($LASTEXITCODE) { throw 'Upload failed.' }
}
function New-Tarball([string]$from, [string]$name) {
    $file = Join-Path $work $name
    Remove-Item $file -ErrorAction SilentlyContinue
    & $tar -czf $file -C $from .
    if ($LASTEXITCODE) { throw "tar failed for $from" }
    $file
}
# the server scripts must reach Linux with LF line endings whatever the checkout uses
function Get-Script([string]$name) {
    $file = Join-Path $work $name
    $text = [IO.File]::ReadAllText((Join-Path $PSScriptRoot "oracle\$name")) -replace "`r`n", "`n"
    [IO.File]::WriteAllText($file, $text, [Text.UTF8Encoding]::new($false))
    $file
}
$deployScript = Get-Script 'ehc-deploy.sh'

if ($Setup) {
    if (-not $domain) { throw 'Set EHC_ORACLE_DOMAIN in deploy/oracle.env (the site address, e.g. ehctest.site).' }
    Write-Host "Preparing $server for $domain ..."
    Send-File @((Get-Script 'server-setup.sh'))
    Invoke-Remote "sudo bash /tmp/server-setup.sh '$domain'"
}

if ($Rollback) {
    Send-File @($deployScript)
    Invoke-Remote 'sudo bash /tmp/ehc-deploy.sh rollback'
    Write-Host 'Done.'
    return
}

if ($Settings) {
    $file = Join-Path $web 'appsettings.Staging.local.json'
    if (-not (Test-Path $file)) { throw "Create $file first (see deploy/appsettings.Staging.local.example.json)." }
    Write-Host 'Uploading server settings ...'
    $copy = Join-Path $work 'ehc-settings.json'
    Copy-Item $file $copy -Force
    Send-File @($copy, $deployScript)
    Invoke-Remote 'sudo bash /tmp/ehc-deploy.sh settings /tmp/ehc-settings.json'
}

# a data/settings/media upload from a local folder must not also ship that folder's uncommitted code unless asked
$deployCode = $Code -or -not ($Setup -or $Settings -or $SeedData -or $Media)
if (-not $deployCode) { Write-Host 'Code is not deployed (add -Code to build and deploy it too).' }
if ($deployCode) {
    if (-not $SkipFrontend) {
        Push-Location (Join-Path $root 'frontend')
        try {
            npm ci; if ($LASTEXITCODE) { throw 'npm ci failed' }
            npm run publish:umbraco; if ($LASTEXITCODE) { throw 'npm run publish:umbraco failed' }
        } finally { Pop-Location }
    }

    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    dotnet publish $web -c Release -r $runtime --self-contained false -o $out -p:EnvironmentName=Staging
    if ($LASTEXITCODE) { throw 'dotnet publish failed' }
    # never ship local data or settings, even if a build step copied them in
    foreach ($p in 'umbraco\Data', 'umbraco\Logs', 'umbraco\mediacache', 'wwwroot\media') {
        Remove-Item (Join-Path $out $p) -Recurse -Force -ErrorAction SilentlyContinue
    }
    Get-ChildItem $out -Filter 'appsettings.*.local.json' | Remove-Item -Force
    if (-not (Test-Path (Join-Path $out 'wwwroot\tiles\ehc-region.pmtiles'))) {
        Write-Warning 'No map tiles in this build: the tiles on the server are left as they are.'
    }

    Write-Host 'Packing ...'
    $pkg = New-Tarball $out 'ehc-code.tar.gz'
    Write-Host ('Uploading {0:N0} MB to {1} ...' -f ((Get-Item $pkg).Length / 1MB), $server)
    Send-File @($pkg, $deployScript)
    Invoke-Remote 'sudo bash /tmp/ehc-deploy.sh code /tmp/ehc-code.tar.gz'
}

if ($SeedData) {
    $answer = Read-Host 'This REPLACES the database and media on the server with your local copies. Type YES to continue'
    if ($answer -cne 'YES') { throw 'Cancelled.' }
    if ($DataFrom) { $DataFrom = (Resolve-Path $DataFrom).Path }
    else {
        $DataFrom = Join-Path $web 'umbraco\Data'
        Write-Host 'Stop the local site first so the SQLite database is complete on disk.'
    }
    $mediaPath = Join-Path $web 'wwwroot\media'
    if (Test-Path $mediaPath) {
        Write-Host 'Uploading media ...'
        Send-File @((New-Tarball $mediaPath 'ehc-media.tar.gz'), $deployScript)
        Invoke-Remote 'sudo bash /tmp/ehc-deploy.sh media /tmp/ehc-media.tar.gz replace'
    }
    Write-Host 'Uploading database ...'
    Send-File @((New-Tarball $DataFrom 'ehc-data.tar.gz'), $deployScript)
    Invoke-Remote 'sudo bash /tmp/ehc-deploy.sh data /tmp/ehc-data.tar.gz'
}

if ($Media -and -not $SeedData) {
    $mediaPath = Join-Path $web 'wwwroot\media'
    if (-not (Test-Path $mediaPath)) { throw "No local media folder: $mediaPath" }
    Write-Host 'Uploading media (nothing is deleted on the server) ...'
    Send-File @((New-Tarball $mediaPath 'ehc-media.tar.gz'), $deployScript)
    Invoke-Remote 'sudo bash /tmp/ehc-deploy.sh media /tmp/ehc-media.tar.gz'
}

Write-Host 'Done.'
