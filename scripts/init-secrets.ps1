#Requires -Version 7

<#
.SYNOPSIS
    Creates the files under secrets/ that docker-compose.yml mounts as secrets.

.DESCRIPTION
    Existing files are never touched, so running the script again is always safe.

    Each missing file takes its value from the first source that has one:
      1. the environment variable WEAKAPPHANDLER_SECRET_<NAME>, for example
         WEAKAPPHANDLER_SECRET_POSTGRES_PASSWORD. CI and servers pass real values this way,
         typically from GitHub Secrets;
      2. a random value, when -Generate is given;
      3. the development value that appsettings.json also uses, which keeps a service started
         from the IDE able to reach the containers.

    Real or random values belong only before the first start: PostgreSQL and RabbitMQ take
    their passwords from these files only while their volumes are still empty.

.EXAMPLE
    ./scripts/init-secrets.ps1
    Creates the missing secrets from the environment, or with the development values.

.EXAMPLE
    ./scripts/init-secrets.ps1 -Generate
    Creates the missing secrets from the environment, or with random values.
#>

[CmdletBinding()]
param(
    [switch] $Generate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$SecretsDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'secrets'
$EnvironmentPrefix = 'WEAKAPPHANDLER_SECRET_'

$DevelopmentValues = [ordered]@{
    postgres_password   = 'processor_password'
    gateway_db_password = 'gateway_password'
    rabbitmq_password   = 'guest'
    pgadmin_password    = 'admin'
    grafana_password    = 'admin'
}

# The vendored WeakApp only accepts this key, so it is never generated. The environment can
# still override it, for the day a real API with a real key takes WeakApp's place.
$FixedValues = [ordered]@{
    weakapp_api_key = 'supersecret'
}

function New-RandomSecret {
    $bytes = [byte[]]::new(24)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)

    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

# An empty variable counts as unset: GitHub expands a secret that was never created to an
# empty string, and that must fall back to the default instead of writing an empty password.
function Get-EnvironmentSecret {
    param([string] $Name)

    $value = [Environment]::GetEnvironmentVariable($EnvironmentPrefix + $Name.ToUpperInvariant())

    return [string]::IsNullOrEmpty($value) ? $null : $value
}

function Write-Secret {
    param([string] $Name, [string] $Default, [string] $DefaultSource)

    $path = Join-Path $SecretsDirectory $Name
    $fromEnvironment = Get-EnvironmentSecret -Name $Name

    if (Test-Path $path) {
        # Overwriting would not change the password PostgreSQL or RabbitMQ already stored in
        # their volumes, only break the services, so a mismatch is reported and left alone.
        if ($null -ne $fromEnvironment -and $fromEnvironment -cne (Get-Content -Path $path -Raw)) {
            Write-Warning "$EnvironmentPrefix$($Name.ToUpperInvariant()) differs from secrets/$Name; the file is kept."
        }

        Write-Host "    kept       $Name" -ForegroundColor DarkGray
        return
    }

    $value, $source = ($null -ne $fromEnvironment) ? @($fromEnvironment, 'environment') : @($Default, $DefaultSource)

    # No trailing newline: the services read the file verbatim as a configuration value.
    Set-Content -Path $path -Value $value -NoNewline -Encoding utf8NoBOM
    Write-Host "    created    $Name ($source)" -ForegroundColor Green
}

New-Item -ItemType Directory -Force -Path $SecretsDirectory | Out-Null

foreach ($entry in $DevelopmentValues.GetEnumerator()) {
    if ($Generate) {
        Write-Secret -Name $entry.Key -Default (New-RandomSecret) -DefaultSource 'generated'
    }
    else {
        Write-Secret -Name $entry.Key -Default $entry.Value -DefaultSource 'development value'
    }
}

foreach ($entry in $FixedValues.GetEnumerator()) {
    Write-Secret -Name $entry.Key -Default $entry.Value -DefaultSource 'fixed value'
}
