"""Wrap the released Transport framework, the new Gear Conditions provider and the starter town."""
import argparse
import shutil
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def write_pack(archive, name, mapping):
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as dest:
        for member, file in sorted(mapping.items()):
            dest.write(file, f"{name}/{member}")


def make_packs(transport, seeds, out):
    out.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(transport) as archive:
        archive.getinfo("Meitou Transport/maestro.json")
        archive.getinfo("Meitou Transport/Meitou.Transport.dll")
    shutil.copyfile(transport, out / "Meitou Transport.zip")
    gate = ROOT / "Providers/Gate"
    write_pack(out / "Gear Conditions.zip", "Gear Conditions", {
        "Gear Conditions.mod": seeds / "Gear Conditions/Gear Conditions.mod",
        "Maestro.Gate.dll": gate / "bin/Release/net11.0/Maestro.Gate.dll",
        "meitou.json": gate / "meitou.json", "maestro.json": gate / "maestro.json"})
    town = ROOT / "Examples/Gatekeeper"
    write_pack(out / "Dune Gate starter.zip", "Dune Gate", {
        "Dune Gate.mod": seeds / "Dune Gate/Dune Gate.mod",
        "Gatekeeper.dll": town / "bin/Release/net11.0/Gatekeeper.dll", "meitou.json": town / "meitou.json"})


if __name__ == "__main__":
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("--transport", type=Path, required=True, help="released playtest.6 framework ZIP")
    cli.add_argument("--seeds", type=Path, required=True, help="output of Tools/Seed")
    cli.add_argument("--out", type=Path, required=True)
    args = cli.parse_args()
    make_packs(args.transport, args.seeds, args.out)
