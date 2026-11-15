param(
    [Parameter(Mandatory)][string]$Meitou,
    [Parameter(Mandatory)][string]$Output,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference='Stop'
$env:MEITOU_DIR=(Resolve-Path -LiteralPath $Meitou).Path
$dest=[IO.Path]::GetFullPath($Output)
$repo=Split-Path $PSScriptRoot
foreach ($rid in 'linux-x64','win-x64') {
    $target=Join-Path $dest $rid
    & $Dotnet publish (Join-Path $repo 'Avalonia/Maestro.Avalonia.csproj') -r $rid --self-contained true -c Release -p:PublishTrimmed=false -p:DebugType=None --artifacts-path (Join-Path $dest '.build') -o $target
    if ($LASTEXITCODE) { throw "Publish failed for $rid" }
    if (Test-Path -LiteralPath (Join-Path $target 'Meitou.Data.dll')) { throw "Game readers belong in the selected installation." }
    Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $target 'LICENSE-Maestro.txt')
}
