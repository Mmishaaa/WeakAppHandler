# Writes the current GraphQL schema next to the solution so the frontend can generate types from
# it. No database is needed: building the schema never opens a connection.

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'GraphQlGateway.API'
$output = Join-Path $root 'schema.graphql'

dotnet run --project $project --no-launch-profile -- schema export --output $output

Write-Host "Schema written to $output"
