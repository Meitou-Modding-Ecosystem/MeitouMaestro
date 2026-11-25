param(
    [Parameter(Mandatory)][string]$CurrentReaders,
    [Parameter(Mandatory)][string]$OldReaders,
    [Parameter(Mandatory)][string]$Scratch,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$env:MEITOU_DIR = (Resolve-Path -LiteralPath $CurrentReaders).Path
$OldReaders = (Resolve-Path -LiteralPath $OldReaders).Path
$Scratch = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Scratch)
$repo = Split-Path $PSScriptRoot -Parent
function Invoke-Dotnet {
    & $Dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE): $args" }
}
Push-Location $repo
try {
    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        Invoke-Dotnet build MeitouMaestro.slnx -c Release
    } else {
        # WPF is Windows-only. Build all other solution projects on Linux.
        foreach ($project in 'Avalonia/Maestro.Avalonia.csproj', 'Providers/Gate/Maestro.Gate.csproj',
                'Examples/Gatekeeper/Gatekeeper.csproj', 'Tools/Checks/Checks.csproj', 'Tools/Seed/Seed.csproj') {
            Invoke-Dotnet build $project -c Release
        }
    }
    $checks = Join-Path $repo 'Tools/Checks/bin/Release/net11.0/Checks.dll'
    Invoke-Dotnet $checks --reader-proof $env:MEITOU_DIR $OldReaders (Join-Path $Scratch 'readers')
    Invoke-Dotnet $checks --fit-proof (Join-Path $Scratch 'fits')
} finally {
    Pop-Location
}
