#Requires -Version 7

<#
.SYNOPSIS
    Rolls the whole stack forward to a set of image tags, or back to the last set that worked.

.DESCRIPTION
    There is no remote host in this project, so "deploying" means the same thing it would
    there: put the images of a release in place, bring the stack up on them, refuse to call it
    done until every endpoint answers, and fall back to the previous release when it does not.

    A release is one image tag per service. Without -Registry the images are built from the
    working tree under a single tag, the short commit sha unless -Tag says otherwise. With
    -Registry they are pulled from it as CI published them, and nothing is built: -Tag applies
    to every service, a service parameter such as -GatewayTag overrides it for that one
    service, and a service left with neither runs latest. The tags can differ because CI only
    rebuilds the services a push touched. Log in to the registry before a pull
    (docker login ghcr.io); the script does not handle credentials.

    The release that last passed is remembered in .deploy-state.json, which is what makes
    -Rollback possible.

.EXAMPLE
    ./scripts/deploy.ps1
    Builds the current commit, deploys it, smoke-tests it.

.EXAMPLE
    ./scripts/deploy.ps1 -Registry ghcr.io/mmishaaa/weakapphandler -GatewayTag 2026-09-25-13.12-update-metrics-format
    Pulls that gateway image and latest for every other service, deploys them, smoke-tests them.

.EXAMPLE
    ./scripts/deploy.ps1 -Rollback
    Puts the previously deployed release back without building anything.
#>

[CmdletBinding()]
param(
    [string] $Tag,
    [string] $IngestorTag,
    [string] $ProcessorTag,
    [string] $NotificationsTag,
    [string] $GatewayTag,
    [string] $FrontendTag,
    [string] $Registry,
    [switch] $SkipBuild,
    [switch] $Rollback,
    [switch] $DryRun,
    [int] $TimeoutSeconds = 240
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$StateFile = Join-Path $RepoRoot '.deploy-state.json'

# The deploy runs the stack as described in docker-compose.yml alone: naming the file skips the
# Development override compose would otherwise load, and the observability profile is part of
# what gets smoke-tested.
$ComposeArguments = @('--file', (Join-Path $RepoRoot 'docker-compose.yml'), '--profile', 'observability')

# Our images, by compose service, with the variable docker-compose.yml reads each tag from.
$ServiceTagVariables = [ordered]@{
    ingestor      = 'INGESTOR_TAG'
    processor     = 'PROCESSOR_TAG'
    notifications = 'NOTIFICATIONS_TAG'
    gateway       = 'GATEWAY_TAG'
    frontend      = 'FRONTEND_TAG'
}

$RequestedServiceTags = @{
    ingestor      = $IngestorTag
    processor     = $ProcessorTag
    notifications = $NotificationsTag
    gateway       = $GatewayTag
    frontend      = $FrontendTag
}

$LocalImagePrefix = 'weakapphandler'

# Every variable the script sets for compose, so the calling session gets its own values back.
$ReleaseVariables = @('IMAGE_REGISTRY', 'IMAGE_TAG') + @($ServiceTagVariables.Values)

function Write-Step {
    param([string] $Message)

    Write-Host "==> $Message" -ForegroundColor Cyan
}

# Callers pass one array: PowerShell would otherwise try to bind '-d' and '--wait' as
# parameter names of this function rather than pass them through to docker.
function Invoke-Compose {
    param([string[]] $Arguments)

    if ($DryRun) {
        Write-Host "    would run: docker compose $($Arguments -join ' ')" -ForegroundColor DarkGray
        return
    }

    & docker compose @ComposeArguments @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') exited with $LASTEXITCODE"
    }
}

function Test-IsBlank {
    param([string] $Value)

    return [string]::IsNullOrWhiteSpace($Value)
}

# Docker's own rule for a tag, checked here so a typo fails before anything is pulled.
function Assert-Tag {
    param([string] $Value, [string] $Service)

    if ($Value -notmatch '^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$') {
        throw "'$Value' is not a valid image tag for $Service."
    }
}

function New-Release {
    param([string] $ImageRegistry, [string] $DefaultTag, [hashtable] $Overrides)

    $tags = [ordered]@{}

    foreach ($service in $ServiceTagVariables.Keys) {
        $override = $Overrides[$service]
        $tags[$service] = (Test-IsBlank $override) ? $DefaultTag : $override.Trim()
        Assert-Tag -Value $tags[$service] -Service $service
    }

    return [pscustomobject]@{
        Registry = (Test-IsBlank $ImageRegistry) ? $null : $ImageRegistry
        Tags     = $tags
    }
}

# One line per release, also used to tell two releases apart.
function Format-Release {
    param($Release)

    $source = $Release.Registry ?? 'local build'
    $tags = @($ServiceTagVariables.Keys | ForEach-Object { "$_=$($Release.Tags[$_])" })

    return "$source; $($tags -join ' ')"
}

# State written before a release carried a tag per service holds one string per release.
function ConvertFrom-StateEntry {
    param($Entry)

    if ($null -eq $Entry) {
        return $null
    }

    if ($Entry -is [string]) {
        return New-Release -ImageRegistry $null -DefaultTag $Entry -Overrides @{}
    }

    $tags = @{}

    foreach ($property in $Entry.Tags.PSObject.Properties) {
        $tags[$property.Name] = [string] $property.Value
    }

    return New-Release -ImageRegistry $Entry.Registry -DefaultTag 'latest' -Overrides $tags
}

function Read-State {
    if (-not (Test-Path $StateFile)) {
        return [pscustomobject]@{ Current = $null; Previous = $null }
    }

    $raw = Get-Content $StateFile -Raw | ConvertFrom-Json

    return [pscustomobject]@{
        Current  = ConvertFrom-StateEntry $raw.Current
        Previous = ConvertFrom-StateEntry $raw.Previous
    }
}

function Write-State {
    param($Current, $Previous)

    if ($DryRun) {
        return
    }

    [pscustomobject]@{
        Current    = $Current
        Previous   = $Previous
        DeployedAt = (Get-Date).ToUniversalTime().ToString('o')
    } | ConvertTo-Json -Depth 5 | Set-Content $StateFile -Encoding utf8
}

function Get-CommitTag {
    $revision = & git -C $RepoRoot rev-parse --short HEAD 2>$null

    if ($LASTEXITCODE -eq 0 -and $revision) {
        return $revision.Trim()
    }

    return 'local'
}

function Resolve-Release {
    $imageRegistry = (Test-IsBlank $Registry) ? $null : $Registry.Trim().TrimEnd('/')
    $overridden = @($RequestedServiceTags.Values | Where-Object { -not (Test-IsBlank $_) })

    if ($null -eq $imageRegistry -and $overridden.Count -gt 0 -and -not $SkipBuild) {
        throw 'A local build tags every image alike; per-service tags need -Registry or -SkipBuild.'
    }

    $defaultTag = if (-not (Test-IsBlank $Tag)) {
        $Tag.Trim()
    }
    elseif ($null -ne $imageRegistry) {
        'latest'
    }
    else {
        Get-CommitTag
    }

    return New-Release -ImageRegistry $imageRegistry -DefaultTag $defaultTag -Overrides $RequestedServiceTags
}

# Compose reads the image names from these variables. Each one is set explicitly, so a value
# left in .env or in the calling session cannot leak into the release.
function Set-ReleaseEnvironment {
    param($Release)

    Set-Item Env:IMAGE_REGISTRY ($Release.Registry ?? $LocalImagePrefix)
    Remove-Item Env:IMAGE_TAG -ErrorAction Ignore

    foreach ($service in $ServiceTagVariables.Keys) {
        Set-Item "Env:$($ServiceTagVariables[$service])" $Release.Tags[$service]
    }
}

# The published port is whatever compose actually bound, not whatever .env says it wanted.
function Get-PublishedUrl {
    param([string] $Service, [int] $ContainerPort, [string] $Path)

    $mapping = (& docker compose @ComposeArguments port $Service $ContainerPort 2>$null | Select-Object -First 1)

    if ($LASTEXITCODE -ne 0 -or -not $mapping) {
        throw "$Service does not publish port $ContainerPort"
    }

    $port = $mapping.Trim().Split(':')[-1]

    return "http://localhost:$port$Path"
}

function Test-Endpoint {
    param([string] $Name, [string] $Service, [int] $ContainerPort, [string] $Path)

    $url = Get-PublishedUrl -Service $Service -ContainerPort $ContainerPort -Path $Path
    $deadline = (Get-Date).AddSeconds(60)

    while ($true) {
        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5
            Write-Host ("    {0,-14} {1} {2}" -f $Name, $response.StatusCode, $url) -ForegroundColor Green
            return
        }
        catch {
            if ((Get-Date) -gt $deadline) {
                throw "$Name is not answering on $url : $($_.Exception.Message)"
            }

            Start-Sleep -Seconds 3
        }
    }
}

# Keys in .env that .env.example does not know are either typos or leftovers from an older
# template, and compose silently ignores both.
function Test-EnvFile {
    $envFile = Join-Path $RepoRoot '.env'
    $exampleFile = Join-Path $RepoRoot '.env.example'

    if (-not (Test-Path $envFile) -or -not (Test-Path $exampleFile)) {
        return
    }

    $pattern = '^\s*#?\s*([A-Z][A-Z0-9_]*)\s*='
    $known = Get-Content $exampleFile | ForEach-Object { if ($_ -match $pattern) { $Matches[1] } }
    $unknown = Get-Content $envFile |
        Where-Object { $_ -notmatch '^\s*#' } |
        ForEach-Object { if ($_ -match $pattern) { $Matches[1] } } |
        Where-Object { $_ -notin $known }

    foreach ($key in $unknown) {
        Write-Warning ".env sets $key, which .env.example does not document; compose may ignore it."
    }
}

function Initialize-Secrets {
    Write-Step 'Checking secrets'

    if ($DryRun) {
        Write-Host '    would create the missing files under secrets/' -ForegroundColor DarkGray
        return
    }

    & (Join-Path $PSScriptRoot 'init-secrets.ps1')
}

# Catches syntax errors, unknown keys and missing secret files before anything is built.
function Test-ComposeFile {
    Write-Step 'Validating docker-compose.yml'

    if ($DryRun) {
        Write-Host '    would run: docker compose config --quiet' -ForegroundColor DarkGray
        return
    }

    & docker compose @ComposeArguments config --quiet

    if ($LASTEXITCODE -ne 0) {
        throw "docker-compose.yml is not valid (docker compose config exited with $LASTEXITCODE)"
    }
}

function Invoke-SmokeTests {
    Write-Step 'Smoke testing'

    if ($DryRun) {
        Write-Host '    would probe every service' -ForegroundColor DarkGray
        return
    }

    Test-Endpoint -Name 'weakapp'    -Service weakapp       -ContainerPort 8080 -Path '/health'
    Test-Endpoint -Name 'ingestor'   -Service ingestor      -ContainerPort 8080 -Path '/health'
    Test-Endpoint -Name 'processor'  -Service processor     -ContainerPort 8080 -Path '/health'
    Test-Endpoint -Name 'gateway'    -Service gateway       -ContainerPort 8080 -Path '/health'
    Test-Endpoint -Name 'notifications' -Service notifications -ContainerPort 8080 -Path '/health'
    Test-Endpoint -Name 'frontend'   -Service frontend      -ContainerPort 80   -Path '/'
    Test-Endpoint -Name 'prometheus' -Service prometheus    -ContainerPort 9090 -Path '/-/ready'
    Test-Endpoint -Name 'loki'       -Service loki          -ContainerPort 3100 -Path '/ready'
    Test-Endpoint -Name 'tempo'      -Service tempo         -ContainerPort 3200 -Path '/ready'
    Test-Endpoint -Name 'grafana'    -Service grafana       -ContainerPort 3000 -Path '/api/health'
}

# A registry release is pulled and never built: the point is to run exactly what CI produced.
# WeakApp is not published anywhere, so compose builds it on `up` if the image is missing.
function Invoke-Release {
    param($Release, [bool] $Build)

    Set-ReleaseEnvironment $Release

    Write-Host "    images from $env:IMAGE_REGISTRY" -ForegroundColor DarkGray

    foreach ($service in $ServiceTagVariables.Keys) {
        Write-Host ("    {0,-14} {1}" -f $service, $Release.Tags[$service]) -ForegroundColor DarkGray
    }

    if ($null -ne $Release.Registry) {
        Write-Step "Pulling images from $($Release.Registry)"
        Invoke-Compose -Arguments (@('pull') + @($ServiceTagVariables.Keys))
    }
    elseif ($Build) {
        Write-Step "Building images tagged $($Release.Tags['ingestor'])"
        Invoke-Compose -Arguments @('build')
    }

    Write-Step 'Starting the stack'
    Invoke-Compose -Arguments @(
        'up', '-d', '--remove-orphans', '--wait', '--wait-timeout', "$TimeoutSeconds")

    Invoke-SmokeTests
}

$savedEnvironment = @{}

foreach ($name in $ReleaseVariables) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

Push-Location $RepoRoot

try {
    if (-not (Test-Path (Join-Path $RepoRoot '.env'))) {
        Write-Step 'No .env found, copying .env.example'

        if (-not $DryRun) {
            Copy-Item (Join-Path $RepoRoot '.env.example') (Join-Path $RepoRoot '.env')
        }
    }

    Test-EnvFile
    Initialize-Secrets
    Test-ComposeFile

    $state = Read-State

    if ($Rollback) {
        if ($null -eq $state.Previous) {
            throw 'Nothing to roll back to: .deploy-state.json holds no previous release.'
        }

        Write-Step "Rolling back to $(Format-Release $state.Previous)"
        Invoke-Release -Release $state.Previous -Build $false
        Write-State -Current $state.Previous -Previous $null

        Write-Host "Rolled back to $(Format-Release $state.Previous)." -ForegroundColor Yellow
        return
    }

    $release = Resolve-Release

    Write-Step "Deploying $(Format-Release $release)"

    try {
        Invoke-Release -Release $release -Build (-not $SkipBuild)
    }
    catch {
        Write-Host "Deploy failed: $($_.Exception.Message)" -ForegroundColor Red

        if ($null -eq $state.Current -or (Format-Release $state.Current) -eq (Format-Release $release)) {
            throw
        }

        Write-Step "Rolling back to $(Format-Release $state.Current)"
        Invoke-Release -Release $state.Current -Build $false
        Write-State -Current $state.Current -Previous $null

        throw "Deploy failed and the stack was rolled back to $(Format-Release $state.Current)."
    }

    Write-State -Current $release -Previous $state.Current

    Write-Host ''
    Write-Host "Deployed $(Format-Release $release)." -ForegroundColor Green
    Write-Host 'Dashboard http://localhost:5180 | Grafana http://localhost:3000'
}
finally {
    Pop-Location

    foreach ($name in $ReleaseVariables) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name])
    }
}
