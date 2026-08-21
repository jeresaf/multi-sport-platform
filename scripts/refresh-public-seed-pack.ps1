[CmdletBinding()]
param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '..\..\docs\public_multi_sport_seed_csv_pack'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\infrastructure\deployment\staging\seed-pack')
)

$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $SourceDirectory).Path
$output = (Resolve-Path -LiteralPath $OutputDirectory).Path
$requiredFiles = 1..19 | ForEach-Object { '{0:D2}_*.csv' -f $_ }

foreach ($pattern in $requiredFiles) {
    if ((Get-ChildItem -LiteralPath $source -Filter $pattern -File).Count -ne 1) {
        throw "The seed source must contain exactly one file matching $pattern."
    }
}

foreach ($required in @('manifest.json', 'README.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $required) -PathType Leaf)) {
        throw "The seed source is missing $required."
    }
}

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('sports-platform-seed-' + [Guid]::NewGuid().ToString('N'))
$archivePath = Join-Path $temporaryRoot 'public-csv-seed.tar.gz'
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null

try {
    & tar -czf $archivePath -C $source .
    if ($LASTEXITCODE -ne 0) {
        throw "tar failed with exit code $LASTEXITCODE."
    }

    $encoded = [Convert]::ToBase64String([IO.File]::ReadAllBytes($archivePath))
    $partLength = [Math]::Ceiling($encoded.Length / 3.0)
    for ($part = 0; $part -lt 3; $part++) {
        $start = $part * $partLength
        $length = [Math]::Min($partLength, $encoded.Length - $start)
        $content = if ($length -gt 0) { $encoded.Substring($start, $length) } else { '' }
        $partPath = Join-Path $output ('public-csv-seed.part-{0:D2}.b64' -f ($part + 1))
        [IO.File]::WriteAllText($partPath, $content, [Text.Encoding]::ASCII)
    }

    Write-Host "Packed $source into three Docker seed parts ($($encoded.Length) Base64 characters)."
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
