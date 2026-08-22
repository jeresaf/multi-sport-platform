$ErrorActionPreference = 'Stop'
$deploymentPath = $PSScriptRoot
$stagingCompose = Join-Path $deploymentPath 'docker-compose.staging.yml'
$walkthroughCompose = Join-Path $deploymentPath 'docker-compose.walkthrough.yml'
$environmentFile = Join-Path $deploymentPath '.env.staging'

Push-Location $deploymentPath
try {
    docker compose --project-name sports-platform-walkthrough `
        --env-file $environmentFile `
        --file $stagingCompose `
        --file $walkthroughCompose stop
    docker compose --project-name staging `
        --env-file $environmentFile `
        --file $stagingCompose up --detach
    Write-Host 'Seeded staging environment resumed.'
}
finally {
    Pop-Location
}
