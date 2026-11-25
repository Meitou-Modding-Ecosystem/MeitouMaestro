"""Assemble the local playtest from built providers, runtime, WPF exports and captured evidence."""
import argparse
import hashlib
import shutil
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile
from portable import ROOT, prepare_editor

KIT = "Meitou-Maestro-WPF-comparison-win-x64"
SOURCE_DIRS = ("App", "Core", "Examples", "Providers", "Tools", "LICENSES")
ROOT_FILES = ("README.md", "DESCRIPTORS.md", "TESTING.md", "EVIDENCE.md", "LICENSING.md", "LICENSE", "build.ps1",
              ".gitignore", ".gitattributes", ".editorconfig", "MeitouMaestro.slnx", "Directory.Build.props", "global.json")


def pack(folder, archive):
    with ZipFile(archive, "w", ZIP_DEFLATED, compresslevel=6) as z:
        for file in sorted(folder.rglob("*")):
            if file.is_file():
                z.write(file, folder.name + "/" + file.relative_to(folder).as_posix())
    with ZipFile(archive) as z:
        bad = z.testzip()
        if bad:
            raise ValueError(f"Bad ZIP member: {bad}")


def digest(file):
    with file.open("rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest()


def assemble(args):
    dst = args.out / KIT
    dst.mkdir(parents=True, exist_ok=False)
    prepare_editor(args.dotnet_root, dst / "Editor", args.runtime_version)
    packs = dst / "Packs"
    packs.mkdir()
    for name in ["Meitou Transport", "Gear Conditions", "Dune Gate starter"]:
        shutil.copy2(args.packs / (name + ".zip"), packs)
    for name in ["Garru Saddle", "Dune Gate"]:
        shutil.copy2(args.exports / (name + ".zip"), packs)
        with ZipFile(packs / (name + ".zip")) as z:
            if not all(m.startswith(name + "/") for m in z.namelist()) or z.testzip():
                raise ValueError(f"Broken exported pack {name}")
    shutil.copy2(args.transport_dist / "Stoat-Desert-Ride-0.3.0-playtest.6.zip", packs / "Stoat Desert Ride.zip")
    source = dst / "Source"
    source.mkdir()
    shutil.copy2(args.transport_dist / "Meitou-Transport-0.3.0-playtest.6-source.zip", source)
    for name in ROOT_FILES:
        shutil.copy2(ROOT / name, dst / name if name.endswith(".md") or name == "LICENSE" else source / name)
    for name in ("README.md", "DESCRIPTORS.md", "TESTING.md", "EVIDENCE.md", "LICENSING.md", "LICENSE"):
        shutil.copy2(ROOT / name, source)
    for name in SOURCE_DIRS:
        shutil.copytree(ROOT / name, source / name, ignore=shutil.ignore_patterns("bin", "obj", "__pycache__", "*.pyc"))
    for name in ("Providers/Gate/maestro.json","LICENSES/CC0-1.0.txt"):
        file = dst / name
        file.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(ROOT / name, file)
    art = dst / "Sample art"
    with ZipFile(args.transport_dist / "Garru-Saddle-0.3.0-playtest.6.zip") as z:
        for file in ["meshes/mvv_garru_saddle.mesh", "textures/mvv_garru_saddle.dds", "LICENSES/CC0-1.0.txt", "LICENSING.md", "LICENSE"]:
            dest = art / file
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(z.read("Garru Saddle/" + file))
    shutil.copytree(args.evidence, dst / "Evidence")
    # Check the exact packaged editor whose WPF controls created the packs.
    witness = (dst / "Evidence/editor-checks.txt").read_text(encoding="utf-8-sig").lower()
    for file in ["Maestro.exe","Maestro.dll","Maestro.Core.dll"]:
        if witness.count(f"{file.lower()} {digest(dst / 'Editor' / file)}") != 2:
            raise ValueError(f"Editor binary differs from the one used to author both examples: {file}")
    files = [p for p in sorted(dst.rglob("*")) if p.is_file()]
    sums = "".join(f"{digest(f)}  {f.relative_to(dst).as_posix()}\n" for f in files)
    (dst / "SHA256SUMS.txt").write_text(sums, encoding="utf-8", newline="\n")
    archive = args.out / (KIT + ".zip")
    pack(dst, archive)
    with ZipFile(archive) as z:
        for f in files:
            rel = f.relative_to(dst).as_posix()
            with z.open(KIT + "/" + rel) as member:
                if hashlib.file_digest(member, "sha256").hexdigest() != digest(f):
                    raise ValueError(f"Archived bytes changed: {rel}")
    (args.out / (KIT + ".zip.sha256")).write_text(f"{digest(archive)}  {archive.name}\n", encoding="ascii")
    print(f"Verified {len(files)} files in {archive.name} ({archive.stat().st_size:,} bytes)")


if __name__ == "__main__":
    cli = argparse.ArgumentParser(description=__doc__)
    for key in ("out","dotnet-root","packs","exports","evidence","transport-dist"):
        cli.add_argument("--" + key, type=Path, required=True)
    cli.add_argument("--runtime-version", default="11.0.0")
    assemble(cli.parse_args())
