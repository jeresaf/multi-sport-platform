[CmdletBinding()]
param(
    [ValidateSet('Check', 'Apply')]
    [string]$Mode = 'Check'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Invoke-RepositoryCommand {
    param(
        [Parameter(Mandatory)]
        [string]$Label,

        [Parameter(Mandatory)]
        [scriptblock]$Command
    )

    Write-Host "`n==> $Label"
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    if ($Mode -eq 'Apply') {
        Invoke-RepositoryCommand '.NET formatting' { dotnet format SportsPlatform.sln --no-restore --verbosity minimal }
        Invoke-RepositoryCommand 'Admin web lint fixes' { npm --prefix apps/admin-web run lint -- --fix }
        Invoke-RepositoryCommand 'Public web lint fixes' { npm --prefix apps/public-web run lint -- --fix }
        Invoke-RepositoryCommand 'Flutter formatting' { dart format apps/mobile/lib apps/mobile/test }
    }
    else {
        Invoke-RepositoryCommand '.NET analyzer formatting gate' { dotnet format analyzers SportsPlatform.sln --verify-no-changes --no-restore --severity error --verbosity minimal }
        Invoke-RepositoryCommand '.NET warning-free build gate' { dotnet build SportsPlatform.sln --configuration Release --no-restore --warnaserror --verbosity minimal }
        Invoke-RepositoryCommand 'Admin web lint' { npm --prefix apps/admin-web run lint }
        Invoke-RepositoryCommand 'Admin web type check' { npm --prefix apps/admin-web run type-check }
        Invoke-RepositoryCommand 'Public web lint' { npm --prefix apps/public-web run lint }
        Invoke-RepositoryCommand 'Public web type check' { npm --prefix apps/public-web run type-check }
        Invoke-RepositoryCommand 'Flutter formatting gate' { dart format --output=none --set-exit-if-changed apps/mobile/lib apps/mobile/test }
        Invoke-RepositoryCommand 'Flutter analysis' { flutter analyze apps/mobile }
    }
}
finally {
    Pop-Location
}

Write-Host "`nRepository formatting mode '$Mode' completed successfully."
