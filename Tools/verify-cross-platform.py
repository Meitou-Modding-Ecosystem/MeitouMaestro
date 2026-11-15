"""Check the Linux ZIP, the released reference and the actual Windows ride installation."""
import argparse
import hashlib
import json
import math
import re
import shutil
import subprocess
import tempfile
from pathlib import Path
from zipfile import ZipFile

parser = argparse.ArgumentParser(description=__doc__)
for name in ("export", "linux-hash", "transport-source", "ride", "checks", "evidence"):
    parser.add_argument("--" + name, type=Path, required=True)
parser.add_argument("--dotnet", default="dotnet")
args = parser.parse_args()
digest = hashlib.sha256(args.export.read_bytes()).hexdigest()
if digest != args.linux_hash.read_text().split()[0].lower():
    raise ValueError("Windows copy differs from Linux export")
root = "Garru Saddle/"
expected = {root + n for n in ("Garru Saddle.mod", "transport.json",
            "meshes/mvv_garru_saddle.mesh", "textures/mvv_garru_saddle.dds")}
notes = [f"SHA-256 on Linux and Windows: {digest}"]
with ZipFile(args.export) as pack, ZipFile(args.transport_source) as reference, tempfile.TemporaryDirectory() as tmp:
    if set(pack.namelist()) != expected or len(pack.namelist()) != len(expected):
        raise ValueError("Expected the four folder-wrapped mod files")
    prefix, = [e[:-len(root + "transport.json")] for e in reference.namelist() if e.endswith("/mods/" + root + "transport.json")]
    for entry in sorted(expected):
        data = pack.read(entry)
        original = reference.read(prefix + entry)
        if data != (args.ride / "install/mods" / entry).read_bytes():
            raise ValueError("Live profile differs from Linux ZIP: " + entry)
        if entry.endswith(".json"):
            if json.loads(data) != json.loads(original):
                raise ValueError("Released fit differs")
        elif entry.endswith(".mod"):
            new, old = Path(tmp) / "generated.mod", Path(tmp) / "reference.mod"
            new.write_bytes(data)
            old.write_bytes(original)
            subprocess.run([args.dotnet, str(args.checks), "--compare-saddle", str(new), str(old)], check=True)
        elif data != original:
            raise ValueError("Art differs: " + entry)
        notes.append(f"{hashlib.sha256(data).hexdigest()}  {entry}")
order = (args.ride / "mods.cfg").read_text(encoding="utf-8-sig").splitlines()
if order != ["Meitou Transport.mod", "Garru Saddle.mod", "Stoat Desert Ride.mod"]:
    raise ValueError(f"Unexpected load order: {order}")
log = (args.ride / "client.log").read_bytes().decode("cp1252", "replace").splitlines()
registered, = [r for r in log if "Garru Saddle.mod registered" in r and "1 mount fit(s)" in r]
turn, = [r for r in log if r.startswith("LIVE RIDE turn:")]
passed, = [r for r in log if r.startswith("LIVE RIDE PASS") and "reload, remount" in r]
numbers = re.search(r"poses=(\d+) turnFrames=(\d+).*headingChange=([\d.]+).*maxSaddleError=([\d.]+).*maxSeatError=([\d.]+)", turn)
if not numbers:
    raise ValueError(turn)
poses, moving, heading, saddle, seat = map(float, numbers.groups())
if not (poses > 0 and moving > 0 and 88 <= heading <= 96 and max(saddle, seat) < .08):
    raise ValueError(turn)
images = []
for row in log:
    match = re.match(r"LIVE RIDE capture tick=(\d+) stage=Turn yaw=([-\d.]+)", row)
    if match:
        still = args.ride / f"ride-{int(match[1]):06d}.png"
        if still.is_file():
            images.append((float(match[2]), still))
if not images:
    raise ValueError("No rendered turn captures")
first, last = images[0][0], images[-1][0]
middle = first + math.remainder(last - first, math.tau) / 2
yaw, image = min(images, key=lambda p: abs(math.remainder(p[0] - middle, math.tau)))
notes += ["Windows load order: " + " -> ".join(order), registered, turn, passed,
          f"Mid-turn still: {image.name}, yaw {yaw:.4f} radians"]
args.evidence.mkdir(parents=True, exist_ok=True)
(args.evidence / "windows-ride.txt").write_text("\n".join(notes) + "\n", encoding="utf-8")
shutil.copyfile(image, args.evidence / "windows-turn.png")
print("\n".join(notes))
