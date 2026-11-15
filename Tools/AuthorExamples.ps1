# Drive the same WPF selection, Review, Save and Export controls an author uses.
param([Parameter(Mandatory)][string]$Kenshi, [Parameter(Mandatory)][string]$Meitou,
      [Parameter(Mandatory)][string]$Editor, [Parameter(Mandatory)][string]$Packs,
      [Parameter(Mandatory)][string]$Art, [Parameter(Mandatory)][string]$Trial)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $Trial) { throw 'Choose a new trial folder.' }
$trialDir = (New-Item -ItemType Directory -Path $Trial -Force).FullName
$profile = (New-Item -ItemType Directory -Path (Join-Path $trialDir 'profile')).FullName
New-Item -ItemType Junction -Path (Join-Path $profile 'data') -Target (Join-Path (Resolve-Path $Kenshi).Path 'data') | Out-Null
$mods = (New-Item -ItemType Directory -Path (Join-Path $profile 'mods')).FullName
$projects = (New-Item -ItemType Directory -Path (Join-Path $trialDir 'projects')).FullName
$exports = (New-Item -ItemType Directory -Path (Join-Path $trialDir 'exports')).FullName
$evidence = (New-Item -ItemType Directory -Path (Join-Path $exports 'Evidence')).FullName
foreach ($file in 'resources.cfg','fcs.def','kenshi.cfg','settings.cfg','currentVersion.txt') {
    if (Test-Path (Join-Path $Kenshi $file)) { Copy-Item -LiteralPath (Join-Path $Kenshi $file) -Destination $profile }
}
$editorPath = (Resolve-Path $Editor).Path
$game = (Resolve-Path $Meitou).Path
$artDir = (Resolve-Path $Art).Path
$cfg = Join-Path $trialDir 'mods.cfg'
$previousCfg = $env:MEITOU_MODS_CFG
$record = @()
function Run-Action($mod, $action, $ids, $values, $picture) {
    $project = Join-Path $projects $mod
    $null = New-Item -ItemType Directory -Path $project -Force
    $recipe = Join-Path $trialDir "$action.json"
    $data = [ordered]@{folders=@($profile,$game,$project);action=$action;records=@($ids);inputs=$values
        export=(Join-Path $exports "$mod.zip");screenshot=(Join-Path $evidence "$picture-editor.png")}
    [IO.File]::WriteAllText($recipe,($data | ConvertTo-Json -Depth 6))
    $proc = Start-Process -FilePath $editorPath -WorkingDirectory (Split-Path $editorPath) -WindowStyle Hidden -ArgumentList @('--ui-trial',('"'+$recipe+'"')) -PassThru
    if (!$proc.WaitForExit(120000)) { $proc.Kill(); throw "Editor timed out at $action" }
    if ($proc.ExitCode -or !(Test-Path $data.export)) {
        if (Test-Path "$recipe.failure") { Get-Content -LiteralPath "$recipe.failure" }
        throw "Editor failed at $action"
    }
    Write-Output "$action WPF review/save/export PASS"
    foreach ($file in 'Maestro.exe','Maestro.dll','Maestro.Core.dll') {
        $h = (Get-FileHash -LiteralPath (Join-Path (Split-Path $editorPath) $file)).Hash
        Write-Output "$file $h"
    }
}
try {
    Expand-Archive -LiteralPath (Join-Path $Packs 'Meitou Transport.zip') -DestinationPath $mods
    $env:MEITOU_MODS_CFG=$cfg
    [IO.File]::WriteAllLines($cfg,[string[]]@('Meitou Transport.mod'))
    $values = @{pack='garru-saddle';fit='garru-saddle.trail';label='Garru Trail Saddle';bone='Bip01 Spine1'
        saddle='0.54 9.05 0.4';rotation='0 0 0';seat='0 1.85 0';yaw='0'
        mesh=(Join-Path $artDir 'meshes/mvv_garru_saddle.mesh');texture=(Join-Path $artDir 'textures/mvv_garru_saddle.dds')}
    $record += Run-Action 'Garru Saddle' 'riding-fit' @('42201-rebirth.mod','3999-gamedata.base') $values 'fit'
    Expand-Archive -LiteralPath (Join-Path $Packs 'Gear Conditions.zip') -DestinationPath $mods
    Expand-Archive -LiteralPath (Join-Path $Packs 'Dune Gate starter.zip') -DestinationPath $projects
    [IO.File]::WriteAllLines($cfg,[string[]]@('Meitou Transport.mod','Gear Conditions.mod'))
    $record += Run-Action 'Dune Gate' 'worn-goggles' @('5-Dune Gate.mod') @{wearing='True'} 'dialogue'
    [IO.File]::WriteAllLines((Join-Path $evidence 'editor-checks.txt'),[string[]]$record)
    $record
} finally { $env:MEITOU_MODS_CFG=$previousCfg }
