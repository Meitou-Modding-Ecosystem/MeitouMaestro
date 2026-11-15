#!/usr/bin/env bash
# Run from a Linux desktop or WSLg. The five arguments may contain spaces.
set -euo pipefail
if [[ $# != 5 ]]; then echo "Usage: $0 editor-executable kenshi-directory meitou-directory provider-kit new-trial-directory" >&2; exit 2; fi
editor=$(realpath "$1")
kenshi=$(realpath "$2")
game=$(realpath "$3")
kit=$(realpath "$4")
mkdir "$5"
trial=$(realpath "$5")
profile="$trial/profile"
mkdir -p "$profile/mods" "$trial/projects/Garru Saddle" "$trial/exports" "$trial/evidence"
ln -s "$kenshi/data" "$profile/data"
for name in resources.cfg fcs.def kenshi.cfg settings.cfg currentVersion.txt; do
    if [[ -f "$kenshi/$name" ]]; then cp "$kenshi/$name" "$profile/"; fi
done
export MEITOU_MODS_CFG="$trial/mods.cfg"
printf '%s\n' 'Meitou Transport.mod' > "$MEITOU_MODS_CFG"
python3 - "$trial" "$kit" "$game" <<'PY'
import json, sys, zipfile
from pathlib import Path
trial, kit, game = map(Path, sys.argv[1:])
with zipfile.ZipFile(kit/'Packs/Meitou Transport.zip') as archive:
    archive.extractall(trial/'profile/mods')
recipe = dict(folders=list(map(str, [trial/'profile', game, trial/'projects/Garru Saddle'])),
              action='riding-fit', records=['42201-rebirth.mod','3999-gamedata.base'],
              inputs=dict(pack='garru-saddle', fit='garru-saddle.trail', label='Garru Trail Saddle',
                          bone='Bip01 Spine1', saddle='0.54 9.05 0.4', rotation='0 0 0', seat='0 1.85 0', yaw='0',
                          mesh=str(kit/'Sample art/meshes/mvv_garru_saddle.mesh'), texture=str(kit/'Sample art/textures/mvv_garru_saddle.dds')),
              export=str(trial/'exports/Garru Saddle.zip'), capture=True)
(trial/'fit.json').write_text(json.dumps(recipe), encoding='utf-8')
PY
recipe="$trial/fit.json"
"$editor" --ui-trial "$recipe" >"$trial/editor.log" 2>&1 &
editor_pid=$!
cleanup() { if kill -0 "$editor_pid" 2>/dev/null; then kill "$editor_pid"; fi; }
trap cleanup EXIT
for ((i=0; i<600; ++i)); do
    [[ -f "$recipe.ready" ]] && break
    if [[ -f "$recipe.failure" ]] || ! kill -0 "$editor_pid" 2>/dev/null; then cat "$trial/editor.log" "$recipe.failure" >&2; exit 1; fi
    sleep 1
done
[[ -f "$recipe.ready" ]] || { echo "Timed out opening the fitting window" >&2; exit 1; }
wid=$(xdotool search --onlyvisible --pid "$editor_pid" --name 'Garru Saddle' | tail -1)
[[ -n "$wid" ]]
xdotool windowactivate "$wid"
read -r x y < <(cat "$recipe.ready"; echo)
xdotool mousemove --window "$wid" "$x" "$y" mousedown 1
for ((i=1; i<=8; ++i)); do xdotool mousemove --window "$wid" "$((x - i * 7))" "$((y + i))"; sleep .08; done
xdotool mouseup 1 click 5
xdotool key minus
xdotool getmouselocation > "$trial/evidence/pointer.txt"
sleep 1
import -window "$wid" "$trial/evidence/linux-fit.png"
touch "$recipe.continue"
wait "$editor_pid" || { cat "$recipe.failure" >&2; exit 1; }
trap - EXIT
cat "$recipe.pass"; echo
(cd "$trial/exports" && sha256sum 'Garru Saddle.zip' > SHA256SUMS.txt)
# The second provider goes into this profile after the ride export has been saved.
python3 - "$trial" "$kit" "$game" <<'PY'
import json, sys, zipfile
from pathlib import Path
trial, kit, game = map(Path, sys.argv[1:])
for name, dest in [('Gear Conditions.zip', 'profile/mods'), ('Dune Gate starter.zip', 'projects')]:
    with zipfile.ZipFile(kit/'Packs'/name) as archive:
        archive.extractall(trial/dest)
(trial/'mods.cfg').write_text('Meitou Transport.mod\nGear Conditions.mod\n')
(trial/'goggles.json').write_text(json.dumps(dict(folders=list(map(str, [trial/'profile', game, trial/'projects/Dune Gate'])),
    action='worn-goggles', records=['5-Dune Gate.mod'], inputs={'wearing':'True'})))
PY
"$editor" --ui-trial "$trial/goggles.json" >"$trial/goggles.log" 2>&1 || { cat "$trial/goggles.json.failure" >&2; exit 1; }
cat "$trial/goggles.json.pass"; echo
(cd "$trial/exports" && sha256sum -c SHA256SUMS.txt)
