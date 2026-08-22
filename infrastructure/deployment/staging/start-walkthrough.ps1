[CmdletBinding()]
param(
    [switch]$Reset
)

$ErrorActionPreference = 'Stop'
$deploymentPath = $PSScriptRoot
$stagingCompose = Join-Path $deploymentPath 'docker-compose.staging.yml'
$walkthroughCompose = Join-Path $deploymentPath 'docker-compose.walkthrough.yml'
$environmentFile = Join-Path $deploymentPath '.env.staging'

if (-not (Test-Path -LiteralPath $environmentFile)) {
    throw "Missing $environmentFile. Create it from .env.example first."
}

Push-Location $deploymentPath
try {
    # The two environments use the same host ports. Stop the seeded staging
    # containers without removing their volumes so they can be resumed later.
    docker compose --project-name staging `
        --env-file $environmentFile `
        --file $stagingCompose stop

    if ($Reset) {
        Write-Host 'Removing only the isolated walkthrough containers and volumes...'
        docker compose --project-name sports-platform-walkthrough `
            --env-file $environmentFile `
            --file $stagingCompose `
            --file $walkthroughCompose down --volumes --remove-orphans
    }

    docker compose --project-name sports-platform-walkthrough `
        --env-file $environmentFile `
        --file $stagingCompose `
        --file $walkthroughCompose up --detach --build

    Write-Host 'Walkthrough environment started.'
    Write-Host 'Admin web: http://localhost:3000'
    Write-Host 'Login: admin@sportsplatform.com / Admin123!'
    Write-Host 'Use -Reset to recreate the isolated walkthrough database from empty.'
}
finally {
    Pop-Location
}
