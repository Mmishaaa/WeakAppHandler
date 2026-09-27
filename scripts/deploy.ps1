#Requires -Version 7

<#
.SYNOPSIS
    Rolls the whole stack forward to one image tag, or back to the last one that worked.

.DESCRIPTION
    There is no remote host in this project, so "deploying" means the same thing it would
    there: build the images under a tag, bring the stack up on that tag, refuse to call it
    done until every endpoint answers, and fall back to the previous tag when it does not.
    The tag that last passed is remembered in .deploy-state.json, which is what makes
    -Rollback possible.

.EXAMPLE
    ./scripts/deploy.ps1
    Builds the current commit, deploys it, smoke-tests it.

.EXAMPLE
    ./scripts/deploy.ps1 -Rollback
    Puts the previously deployed tag back without rebuilding anything.
#>

[CmdletBinding()]
param(
    [string] $Tag,
    [switch] $SkipBuild,
    [switch] $Rollback,
    [switch] $DryRun,
    [int] $TimeoutSeconds = 240
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$StateFile = Join-Path $RepoRoot '.deploy-state.json'

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

    & docker compose @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "docker compose $($Arguments -join ' ') exited with $LASTEXITCODE"
    }
}

function Read-State {
    if (-not (Test-Path $StateFile)) {
        return [pscustomobject]@{ Current = $null; Previous = $null }
    }

    return Get-Content $StateFile -Raw | ConvertFrom-Json
}

function Write-State {
    param([string] $Current, [string] $Previous)

    if ($DryRun) {
        return
    }

    [pscustomobject]@{
        Current   = $Current
        Previous  = $Previous
        DeployedAt = (Get-Date).ToUniversalTime().ToString('o')
    } | ConvertTo-Json | Set-Content $StateFile -Encoding utf8
}

function Resolve-Tag {
    if ($Tag) {
        return $Tag
    }

    $revision = & git -C $RepoRoot rev-parse --short HEAD 2>$null

    if ($LASTEXITCODE -eq 0 -and $revision) {
        return $revision.Trim()
    }

    return 'local'
}

# The published port is whatever compose actually bound, not whatever .env says it wanted.
function Get-PublishedUrl {
    param([string] $Service, [int] $ContainerPort, [string] $Path)

    $mapping = (& docker compose port $Service $ContainerPort 2>$null | Select-Object -First 1)

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

function Invoke-SmokeTests {
    Write-Step 'Smoke testing'

    if ($DryRun) {
        Write-Host '    would probe every service' -ForegroundColor DarkGray
        return
    }

    Test-Endpoint -Name 'weakapp'    -Service weakapp       -ContainerPort 8080 -Path '/health'
    Test-Endpoint -Name 'ingestor'   -Service ingestor      -ContainerPort 8080 -Path '/metrics'
    Test-Endpoint -Name 'processor'  -Service processor     -ContainerPort 8080 -Path '/metrics'
    Test-Endpoint -Name 'gateway'    -Service gateway       -ContainerPort 8080 -Path '/metrics'
    Test-Endpoint -Name 'notifications' -Service notifications -ContainerPort 8080 -Path '/metrics'
    Test-Endpoint -Name 'frontend'   -Service frontend      -ContainerPort 80   -Path '/'
    Test-Endpoint -Name 'prometheus' -Service prometheus    -ContainerPort 9090 -Path '/-/ready'
    Test-Endpoint -Name 'loki'       -Service loki          -ContainerPort 3100 -Path '/ready'
    Test-Endpoint -Name 'tempo'      -Service tempo         -ContainerPort 3200 -Path '/ready'
    Test-Endpoint -Name 'grafana'    -Service grafana       -ContainerPort 3000 -Path '/api/health'
}

function Invoke-Release {
    param([string] $Release, [bool] $Build)

    $env:IMAGE_TAG = $Release

    if ($Build) {
        Write-Step "Building images tagged $Release"
        Invoke-Compose -Arguments @('build')
    }

    Write-Step "Starting the stack on $Release"
    Invoke-Compose -Arguments @(
        'up', '-d', '--remove-orphans', '--wait', '--wait-timeout', "$TimeoutSeconds")

    Invoke-SmokeTests
}

Push-Location $RepoRoot

try {
    if (-not (Test-Path (Join-Path $RepoRoot '.env'))) {
        Write-Step 'No .env found, copying .env.example'

        if (-not $DryRun) {
            Copy-Item (Join-Path $RepoRoot '.env.example') (Join-Path $RepoRoot '.env')
        }
    }

    $state = Read-State

    if ($Rollback) {
        if (-not $state.Previous) {
            throw 'Nothing to roll back to: .deploy-state.json holds no previous tag.'
        }

        Write-Step "Rolling back to $($state.Previous)"
        Invoke-Release -Release $state.Previous -Build $false
        Write-State -Current $state.Previous -Previous $null

        Write-Host "Rolled back to $($state.Previous)." -ForegroundColor Yellow
        return
    }

    $release = Resolve-Tag

    Write-Step "Deploying $release"

    try {
        Invoke-Release -Release $release -Build (-not $SkipBuild)
    }
    catch {
        Write-Host "Deploy of $release failed: $($_.Exception.Message)" -ForegroundColor Red

        if (-not $state.Current -or $state.Current -eq $release) {
            throw
        }

        Write-Step "Rolling back to $($state.Current)"
        Invoke-Release -Release $state.Current -Build $false
        Write-State -Current $state.Current -Previous $null

        throw "Deploy of $release failed and the stack was rolled back to $($state.Current)."
    }

    Write-State -Current $release -Previous $state.Current

    Write-Host ''
    Write-Host "Deployed $release." -ForegroundColor Green
    Write-Host 'Dashboard http://localhost:5180 | Grafana http://localhost:3000'
}
finally {
    Pop-Location
}
