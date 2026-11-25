param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed ($LASTEXITCODE): $Arguments" }
}
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if ($IsWindows) {
        Invoke-Checked $Dotnet @('build', 'MeitouMaestro.slnx', '-c', 'Release')
    } else {
        foreach ($project in 'Avalonia/Maestro.Avalonia.csproj', 'Providers/Gate/Maestro.Gate.csproj',
                'Examples/Gatekeeper/Gatekeeper.csproj', 'Tools/Checks/Checks.csproj', 'Tools/Seed/Seed.csproj') {
            Invoke-Checked $Dotnet @('build', $project, '-c', 'Release')
        }
    }
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ('maestro-ci-' + [guid]::NewGuid())
    Invoke-Checked $Dotnet @('Tools/Checks/bin/Release/net11.0/Checks.dll', '--fit-proof', $scratch)
} finally { Pop-Location }
