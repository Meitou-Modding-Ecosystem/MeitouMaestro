"""Verify that the editor exports are the mod files used in three isolated live trials."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from zipfile import ZipFile


def archive_files(archive):
    with ZipFile(archive) as z:
        return {n: z.read(n) for n in z.namelist() if not n.endswith("/")}


def check(condition, message, notes):
    if not condition:
        raise ValueError(message)
    notes.append("PASS " + message)


def log(trial):
    return (trial / "client.log").read_bytes().decode("cp1252", errors="replace").splitlines()


def verify(args):
    out = args.output
    out.mkdir(parents=True, exist_ok=True)
    notes = []
    saddle = archive_files(args.packs / "Garru Saddle.zip")
    reference = archive_files(args.reference)
    key = "Garru Saddle/transport.json"
    check(json.loads(saddle[key]) == json.loads(reference[key]), "whole schema-3 definition equals the released Garru Saddle", notes)
    for path, data in saddle.items():
        if path.endswith((".mesh", ".dds")):
            check(data == reference[path], f"original CC0 asset bytes match: {path}", notes)
    def installed(archive, name, trial):
        for path, data in archive_files(archive).items():
            if data != (trial / "install/mods" / path).read_bytes():
                raise ValueError(f"{name} used different bytes: {path}")
        notes.append(f"PASS {archive.name} payload equals the installed {name} files")
    for label, trial in [("Garru",args.garru),("Pack Beast",args.beast)]:
        installed(args.packs / "Garru Saddle.zip", label, trial)
        installed(args.packs / "Meitou Transport.zip", label, trial)
        rows = log(trial)
        check(any("Garru Saddle.mod registered" in line and "1 mount fit(s)" in line for line in rows),
              f"{label} live client registered the editor pack", notes)
        report = [r for r in rows if "Garru Saddle.mod registered" in r or r.startswith(
            ("LIVE RIDE start ", "LIVE RIDE turn:", "LIVE RIDE mount stopped", "LIVE RIDE PASS"))]
        if not any("LIVE RIDE PASS" in r for r in report):
            raise ValueError(f"{label} live ride did not finish")
        (out / (label.lower().replace(" ", "-") + "-ride.txt")).write_text("\n".join(report) + "\n", encoding="utf-8")
    for mod in ["Gear Conditions", "Dune Gate"]:
        installed(args.packs / (mod + ".zip"), "dialogue", args.gate)
    talk = [r for r in log(args.gate) if r.startswith("MAESTRO GATE") or "loaded from Gear Conditions" in r or "loaded from Dune Gate" in r]
    equipped = next(r for r in talk if r.startswith("MAESTRO GATE equipped"))
    carried = next(r for r in talk if r.startswith("MAESTRO GATE carried"))
    extract = lambda r: re.search(r"keeper (\d+\.\d+)", r).group(1)
    check(extract(equipped) == extract(carried) and any(r.startswith("MAESTRO GATE PASS") for r in talk),
          "same in-game keeper gave the equipped and carried responses", notes)
    (out / "gate-conversation.txt").write_text("\n".join(talk) + "\n", encoding="utf-8")
    for name in ("Garru Saddle","Dune Gate","Gear Conditions","Meitou Transport"):
        for path,data in sorted(archive_files(args.packs / (name + ".zip")).items()):
            if path.endswith((".mod", ".mesh", ".dds", ".dll", ".json")):
                notes.append(f"{hashlib.sha256(data).hexdigest()}  {path}")
    (out / "installed-payloads.txt").write_text("\n".join(notes) + "\n", encoding="utf-8")
    print("\n".join(n for n in notes if n.startswith("PASS")))


if __name__ == "__main__":
    cli = argparse.ArgumentParser(description=__doc__)
    for key in ("packs","reference","garru","beast","gate","output"):
        cli.add_argument("--" + key, required=True, type=Path)
    verify(cli.parse_args())
