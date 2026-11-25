# Reuse the released Transport verifier with Maestro-authored packs in a new test workspace.
param([Parameter(Mandatory)][string]$Game, [Parameter(Mandatory)][string]$Kenshi,
      [Parameter(Mandatory)][string]$Packs, [Parameter(Mandatory)][string]$TransportSource,
      [Parameter(Mandatory)][string]$TrialRoot,
      [ValidateSet('stoat','stoat-bags')][string[]]$Scenarios = @('stoat','stoat-bags'))
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $TrialRoot) { throw 'Choose an empty, new trial root.' }
$trial = (New-Item -ItemType Directory -Path $TrialRoot).FullName
$unpacked = Join-Path $trial 'source'
Expand-Archive -LiteralPath $TransportSource -DestinationPath $unpacked
$verifierProject = @(Get-ChildItem -LiteralPath $unpacked -Recurse -Filter LiveVoyage.csproj)
if ($verifierProject.Count -ne 1) { throw 'Expected the Transport playtest.6 source archive.' }
$verifier = $verifierProject[0].Directory
$repo = $verifier.Parent.Parent.FullName
$env:MEITOU_DIR = (Resolve-Path $Game).Path
$dist = New-Item -ItemType Directory -Path (Join-Path $repo 'dist') -Force
foreach ($name in 'Meitou Transport','Garru Saddle','Stoat Desert Ride') {
    Copy-Item -LiteralPath (Join-Path $Packs "$name.zip") -Destination (Join-Path $dist.FullName ($name.Replace(' ','-')+'-0.3.0-playtest.6.zip'))
}
foreach ($project in $verifierProject[0].FullName,(Join-Path $repo 'tools/DesertRide/DesertRide.csproj')) {
    & dotnet build $project -c Release
    if ($LASTEXITCODE) { throw "Could not build $project" }
}
foreach ($scenario in $Scenarios) {
    & (Join-Path $verifier.FullName 'run.ps1') -Scenario $scenario -Kenshi $Kenshi -Game $Game -TrialDirectory (Join-Path $trial $scenario) -CacheDirectory $trial -SaddleZip (Join-Path $Packs "Garru Saddle.zip")
    if ($LASTEXITCODE) { throw "$scenario failed" }
}
