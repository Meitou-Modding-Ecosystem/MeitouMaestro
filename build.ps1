param([Parameter(Mandatory)][string]$Meitou, [string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$env:MEITOU_DIR = (Resolve-Path -LiteralPath $Meitou).Path
Push-Location $PSScriptRoot
try {
    foreach ($project in 'App/Maestro.csproj', 'Providers/Gate/Maestro.Gate.csproj',
             'Examples/Gatekeeper/Gatekeeper.csproj', 'Tools/Seed/Seed.csproj', 'Tools/Checks/Checks.csproj') {
        & $Dotnet build $project -c Release --nologo
        if ($LASTEXITCODE) { throw "Build failed for $project" }
    }
    & $Dotnet publish App/Maestro.csproj -c Release --no-restore --no-self-contained
    if ($LASTEXITCODE) { throw 'Editor publish failed.' }
} finally { Pop-Location }
