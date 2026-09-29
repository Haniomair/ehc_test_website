<#
.SYNOPSIS
  Builds the site and deploys it to the staging host (Windows/IIS with Web Deploy, e.g. Plesk).

.DESCRIPTION
  Connection details are read from deploy/staging.env (not committed; copy staging.env.example) or from
  environment variables of the same name. The password is asked for when EHC_DEPLOY_PASSWORD is not set.

  The server's own data is never touched by a normal deploy: umbraco/Data (database), umbraco/Logs,
  umbraco/mediacache, wwwroot/media, logs and appsettings.*.local.json are skipped in both directions.

.EXAMPLE
  ./deploy/deploy-staging.ps1                    # build + deploy code
  ./deploy/deploy-staging.ps1 -Settings          # also upload src/EHC.Web/appsettings.Staging.local.json
  ./deploy/deploy-staging.ps1 -SeedData          # also REPLACE the server database and media with the local ones
  ./deploy/deploy-staging.ps1 -WhatIf            # show what would change, change nothing
#>
[CmdletBinding()]
param(
    [switch]$SkipFrontend,   # reuse the assets already in src/EHC.Web/wwwroot/assets
    [switch]$SelfContained,  # bundle the .NET runtime (when the host doesn't offer .NET 10)
    [switch]$Settings,       # upload the server settings file
    [switch]$SeedData,       # overwrite the server's umbraco/Data and wwwroot/media with the local copies
    [switch]$AllowUntrusted, # accept a self-signed certificate on the Web Deploy endpoint
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$web = Join-Path $root 'src\EHC.Web'
$out = Join-Path $web 'bin\Staging\publish'

# --- connection details ------------------------------------------------------------------------------------
$envFile = Join-Path $PSScriptRoot 'staging.env'
if (Test-Path $envFile) {
    foreach ($line in Get-Content $envFile) {
        if ($line -match '^\s*([A-Z_]+)\s*=\s*(.*?)\s*$' -and -not [Environment]::GetEnvironmentVariable($Matches[1])) {
            [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2])
        }
    }
}
$server = $env:EHC_DEPLOY_SERVER
$site = $env:EHC_DEPLOY_SITE
$user = $env:EHC_DEPLOY_USER
if (-not ($server -and $site -and $user)) {
    throw "Set EHC_DEPLOY_SERVER, EHC_DEPLOY_SITE and EHC_DEPLOY_USER in deploy/staging.env (see staging.env.example)."
}
if ($env:EHC_DEPLOY_ALLOW_UNTRUSTED -eq 'true') { $AllowUntrusted = [switch]$true }
if ($env:EHC_DEPLOY_SELF_CONTAINED -eq 'true') { $SelfContained = [switch]$true }
$password = $env:EHC_DEPLOY_PASSWORD
if (-not $password) {
    if ($env:CI) { throw 'EHC_DEPLOY_PASSWORD is not set.' }
    $secure = Read-Host "Web Deploy password for $user" -AsSecureString
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}

$msdeploy = Join-Path ${env:ProgramFiles} 'IIS\Microsoft Web Deploy V3\msdeploy.exe'
if (-not (Test-Path $msdeploy)) {
    throw "Web Deploy is not installed. Install Web Deploy from Microsoft, then run this again."
}

# --- build -------------------------------------------------------------------------------------------------
if (-not $SkipFrontend) {
    Push-Location (Join-Path $root 'frontend')
    try {
        npm ci; if ($LASTEXITCODE) { throw 'npm ci failed' }
        npm run publish:umbraco; if ($LASTEXITCODE) { throw 'npm run publish:umbraco failed' }
    } finally { Pop-Location }
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
$publishArgs = @('publish', $web, '-c', 'Release', '-o', $out, '-p:EnvironmentName=Staging')
if ($SelfContained) { $publishArgs += @('-r', 'win-x64', '--self-contained') }
dotnet @publishArgs
if ($LASTEXITCODE) { throw 'dotnet publish failed' }

# --- deploy ------------------------------------------------------------------------------------------------
$dest = "computerName=`"https://${server}:8172/msdeploy.axd?site=$site`",userName=`"$user`",password=`"$password`",authType=Basic"
$common = @('-enableRule:AppOffline', '-retryAttempts:3', '-retryInterval:3000')
if ($AllowUntrusted) { $common += '-allowUntrusted' }
if ($WhatIf) { $common += '-whatif' }

function Invoke-MsDeploy([string[]]$arguments) {
    # msdeploy parses its own command line, so pass it verbatim rather than through PowerShell's quoting
    $p = Start-Process -FilePath $msdeploy -ArgumentList ($arguments -join ' ') -NoNewWindow -Wait -PassThru
    if ($p.ExitCode) { throw "msdeploy failed (exit code $($p.ExitCode))" }
}

# map tiles are built separately (npm run tiles); without them, keep the server's copy instead of deleting it
$tilesSkip = @()
if (-not (Test-Path (Join-Path $out 'wwwroot\tiles\ehc-region.pmtiles'))) {
    Write-Warning 'No map tiles in this build: the tiles on the server are left as they are.'
    $tilesSkip = @('-skip:Directory="\\wwwroot\\tiles$"')
}

Write-Host "Deploying code to $site on $server ..."
Invoke-MsDeploy (@(
    '-verb:sync',
    "-source:contentPath=`"$out`"",
    "-dest:contentPath=`"$site`",$dest",
    '-skip:Directory="\\umbraco\\Data$"',
    '-skip:Directory="\\umbraco\\Logs$"',
    '-skip:Directory="\\umbraco\\mediacache$"',
    '-skip:Directory="\\wwwroot\\media$"',
    '-skip:Directory="\\logs$"',
    '-skip:File="\\appsettings\.[^\\]+\.local\.json$"'
) + $tilesSkip + $common)

if ($Settings) {
    $file = Join-Path $web 'appsettings.Staging.local.json'
    if (-not (Test-Path $file)) { throw "Create $file first (see deploy/appsettings.Staging.local.example.json)." }
    Write-Host 'Uploading server settings ...'
    Invoke-MsDeploy (@(
        '-verb:sync',
        "-source:filePath=`"$file`"",
        "-dest:filePath=`"$site/appsettings.Staging.local.json`",$dest"
    ) + $common)
}

if ($SeedData) {
    $answer = Read-Host 'This REPLACES the database and media on the server with your local copies. Type YES to continue'
    if ($answer -cne 'YES') { throw 'Cancelled.' }
    Write-Host 'Stop the local site first so the SQLite database is complete on disk.'
    Invoke-MsDeploy (@(
        '-verb:sync',
        "-source:contentPath=`"$(Join-Path $web 'umbraco\Data')`"",
        "-dest:contentPath=`"$site/umbraco/Data`",$dest",
        '-skip:Directory="\\TEMP$"'
    ) + $common)
    $media = Join-Path $web 'wwwroot\media'
    if (Test-Path $media) {
        Invoke-MsDeploy (@(
            '-verb:sync',
            "-source:contentPath=`"$media`"",
            "-dest:contentPath=`"$site/wwwroot/media`",$dest"
        ) + $common)
    }
}

Write-Host 'Done.'
