# Official managed-reader fixtures only; no Kenshi installation or credentials needed.
param([Parameter(Mandatory)][string]$Output)
$ErrorActionPreference = 'Stop'
$fixtures = @(
    @{ Version = '0.22.1'; Hash = 'e9d1c41330d0859902b35db450209f86a5cf5093c846545153c3eb131161375f'; Variable = 'MEITOU_DIR' },
    @{ Version = '0.20.1'; Hash = 'ea090b02f48bddcc11998c7f2ac7a75fe71a42d15bb691953d32132dbce9a820'; Variable = 'MAESTRO_OLD_READERS' }
)
New-Item -ItemType Directory -Force -Path $Output | Out-Null
foreach ($fixture in $fixtures) {
    $version = $fixture.Version
    $archive = Join-Path $Output "Meitou-$version-win-x64.zip"
    if (-not (Test-Path -LiteralPath $archive)) {
        Invoke-WebRequest -Uri "https://github.com/VlcekM/MeitouClient/releases/download/v$version/Meitou-$version-win-x64.zip" -OutFile $archive
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $fixture.Hash) {
        throw "SHA-256 mismatch for Meitou $version"
    }
    $destination = Join-Path $Output $version
    Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
    $readers = Join-Path $destination "Meitou-$version-win-x64"
    foreach ($assembly in 'Meitou.Core.dll', 'Meitou.Data.dll') {
        if (-not (Test-Path -LiteralPath (Join-Path $readers $assembly))) { throw "Missing $assembly in Meitou $version" }
    }
    # Both jobs use the same managed DLL bytes; Linux never runs the Windows apphost.
    $readers = (Resolve-Path -LiteralPath $readers).Path
    Set-Item -Path "Env:$($fixture.Variable)" -Value $readers
    if ($env:GITHUB_ENV) { "$($fixture.Variable)=$readers" | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append }
    Write-Host "Verified Meitou $version readers: $readers"
}
