#Requires -Version 7

<#
.SYNOPSIS
    Creates the files under secrets/ that docker-compose.yml mounts as secrets.

.DESCRIPTION
    Existing files are never touched, so running the script again is always safe. Without
    -Generate the files get the development values that appsettings.json also uses, which keeps
    a service started from the IDE able to reach the containers. -Generate writes random values
    instead; use it only before the first start, because PostgreSQL and RabbitMQ take their
    passwords from these files only while their volumes are still empty.

.EXAMPLE
    ./scripts/init-secrets.ps1
    Creates the missing secrets with the development values.

.EXAMPLE
    ./scripts/init-secrets.ps1 -Generate
    Creates the missing secrets with random values.
#>

[CmdletBinding()]
param(
    [switch] $Generate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$SecretsDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'secrets'

$DevelopmentValues = [ordered]@{
    postgres_password   = 'processor_password'
    gateway_db_password = 'gateway_password'
    rabbitmq_password   = 'guest'
    pgadmin_password    = 'admin'
    grafana_password    = 'admin'
}

# The vendored WeakApp only accepts this key, so it is never generated.
$FixedValues = [ordered]@{
    weakapp_api_key = 'supersecret'
}

function New-RandomSecret {
    $bytes = [byte[]]::new(24)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)

    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Write-Secret {
    param([string] $Name, [string] $Value)

    $path = Join-Path $SecretsDirectory $Name

    if (Test-Path $path) {
        Write-Host "    kept     $Name" -ForegroundColor DarkGray
        return
    }

    # No trailing newline: the services read the file verbatim as a configuration value.
    Set-Content -Path $path -Value $Value -NoNewline -Encoding utf8NoBOM
    Write-Host "    created  $Name" -ForegroundColor Green
}

New-Item -ItemType Directory -Force -Path $SecretsDirectory | Out-Null

foreach ($entry in $DevelopmentValues.GetEnumerator()) {
    $value = if ($Generate) { New-RandomSecret } else { $entry.Value }
    Write-Secret -Name $entry.Key -Value $value
}

foreach ($entry in $FixedValues.GetEnumerator()) {
    Write-Secret -Name $entry.Key -Value $entry.Value
}
