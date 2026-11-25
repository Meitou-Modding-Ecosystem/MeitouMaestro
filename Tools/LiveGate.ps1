# Run the exported town and condition provider together in a private v0.22.1 data profile.
param([Parameter(Mandatory)][string]$Game, [Parameter(Mandatory)][string]$Kenshi,
      [Parameter(Mandatory)][string]$Packs, [Parameter(Mandatory)][string]$Trial)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Trial){throw 'Choose a new trial directory.'}
$trialPath=[IO.Path]::GetFullPath($Trial)
$install=Join-Path $trialPath 'install'
New-Item -ItemType Directory -Path $install -Force | Out-Null
New-Item -ItemType Junction -Path (Join-Path $install 'data') -Target (Join-Path (Resolve-Path $Kenshi).Path 'data') | Out-Null
foreach($name in 'resources.cfg','controls.cfg','fcs.def','screens.cfg','settings.cfg','kenshi.cfg','__tutorials.data','currentVersion.txt'){
  $file=Join-Path $Kenshi $name
  if(Test-Path -LiteralPath $file){Copy-Item -LiteralPath $file -Destination $install}
}
$mods=Join-Path $install 'mods'
foreach($name in 'Gear Conditions','Dune Gate'){Expand-Archive -LiteralPath (Join-Path $Packs "$name.zip") -DestinationPath $mods}
$control=(New-Item -ItemType Directory -Path (Join-Path $trialPath 'control')).FullName
$cfg=Join-Path $trialPath 'mods.cfg'
[IO.File]::WriteAllLines($cfg,[string[]]@('Gear Conditions.mod','Dune Gate.mod'))
$log=Join-Path $trialPath 'client.log'
$env:KENSHI_PATH=$install
$env:MEITOU_MODS_CFG=$cfg
$env:MAESTRO_GATE_TRIAL='1'
$env:DOTNET_STARTUP_HOOKS=$null
$env:DOTNET_PROCESSOR_COUNT='4'
$env:TEMP=(New-Item -ItemType Directory -Path (Join-Path $trialPath 'temp')).FullName
$env:TMP=$env:TEMP
$argsText='--headless --new-game "Dune Gate Greeting" --no-load-cache --no-precompile --no-objects --no-foliage --no-gi --no-audio --water-reflection 0 --fps-limit 24 --shadow-quality 0 --texture-quality 1 --size 1100x760 --control-dir "' + $control + '" --mods-cfg "' + $cfg + '"'
$proc=Start-Process -FilePath (Join-Path $Game 'MeitouClient.exe') -ArgumentList $argsText -WorkingDirectory $trialPath -WindowStyle Hidden -PassThru -RedirectStandardOutput $log -RedirectStandardError (Join-Path $trialPath 'errors.log')
try{
  $until=[DateTime]::UtcNow.AddMinutes(6)
  while(!$proc.HasExited -and [DateTime]::UtcNow -lt $until){
    if(Select-String -Path $log -Pattern 'MAESTRO GATE (PASS|FAIL)' -Quiet){break}
    Start-Sleep -Milliseconds 700
  }
  Get-Content $log -Tail 10
  if(!(Select-String -Path $log -Pattern 'MAESTRO GATE PASS' -Quiet)){throw "Inspect $log"}
}finally{
  if(!$proc.HasExited){[IO.File]::WriteAllText((Join-Path $control 'cmd'),"maestro-quit quit`n")
    if(!$proc.WaitForExit(15000)){$proc.Kill();$proc.WaitForExit()}
  }
}
