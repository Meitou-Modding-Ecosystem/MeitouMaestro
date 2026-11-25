"""Package self-contained publish folders, preserving the Linux apphost's execute bit."""
import argparse
import hashlib
import json
import re
import shutil
import tarfile
import xml.etree.ElementTree as ET
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

ROOT = Path(__file__).resolve().parents[1]
MIT = """Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
"""

def notices(source, cache, manifest):
    mit = []
    target = source / "Licenses"
    target.mkdir(exist_ok=True)
    for key, info in manifest["libraries"].items():
        if info["type"] == "project":
            continue
        name, version = key.lower().removeprefix("runtimepack.").split("/")
        package = cache / name / version
        if not package.is_dir():
            raise ValueError(f"Missing restored package notice source: {key}")
        files = [p for p in package.iterdir() if p.is_file() and p.name.lower().startswith(("license", "third-party-notices"))]
        for file in files:
            shutil.copyfile(file, target / (name + "-" + file.name))
        if not files:
            spec = ET.parse(next(package.glob("*.nuspec")))
            license_id = spec.findtext(".//{*}license")
            if license_id != "MIT":
                raise ValueError(f"Review the notice for {key}: {license_id}")
            holder = spec.findtext(".//{*}copyright") or spec.findtext(".//{*}authors")
            mit.append(f"{key} — {holder}")
    (target / "MIT-components.txt").write_text("\n".join(sorted(mit)) + "\n\n" + MIT, encoding="utf-8")

def package(source, cache, destination, rid, source_ref):
    manifest = json.loads((source / "MeitouMaestro.deps.json").read_bytes())
    application, = [key for key in manifest["libraries"] if key.startswith("MeitouMaestro/")]
    name = "Meitou-Maestro-" + application.split("/")[1] + "-" + rid
    notices(source, cache, manifest)
    shutil.copyfile(ROOT / "AVALONIA.md", source / "START-HERE.md")
    shutil.copyfile(ROOT / "LICENSE", source / "LICENSE-Maestro.txt")
    shutil.copyfile(ROOT / "LICENSES/CC0-1.0.txt", source / "Licenses/CC0-1.0.txt")
    licensing = (ROOT / "LICENSING.md").read_text(encoding="utf-8")
    licensing = licensing.replace("](LICENSE)", "](LICENSE-Maestro.txt)").replace("LICENSES/CC0-1.0.txt", "Licenses/CC0-1.0.txt")
    (source / "LICENSING.md").write_text(licensing, encoding="utf-8")
    (source / "SOURCE.md").write_text(
        "# Matching source\n\n"
        f"Maestro {application.split('/')[1]}, source commit `{source_ref}`.\n\n"
        f"[Browse the source](https://github.com/Meitou-Modding-Ecosystem/MeitouMaestro/tree/{source_ref}) · "
        f"[Download source ZIP](https://github.com/Meitou-Modding-Ecosystem/MeitouMaestro/archive/{source_ref}.zip)\n\n"
        "Build and packaging instructions are in TESTING.md in that source. "
        "The editor archive includes its runtime and dependency notices; Meitou and Kenshi files are supplied by your own installations.\n",
        encoding="utf-8")
    (source / "docs").mkdir(exist_ok=True)
    shutil.copyfile(ROOT / "docs/garru-fit.png", source / "docs/garru-fit.png")
    files = sorted(p for p in source.rglob("*") if p.is_file())
    if any((p.name.lower().startswith("meitou.") and p.suffix.lower() == ".dll") or p.name.lower() in {"meitouclient.dll", "meitouclient.exe"} for p in files):
        raise ValueError("Unexpected client files in the editor")
    if rid == "linux-x64":
        archive = destination / (name + ".tar.gz")
        with tarfile.open(archive, "w:gz", compresslevel=6) as output:
            for p in files:
                info = output.gettarinfo(str(p), name + "/" + p.relative_to(source).as_posix())
                info.mode = 0o755 if p.name in ("MeitouMaestro", "createdump") else 0o644
                info.uid = info.gid = 0
                info.uname = info.gname = ""
                with p.open("rb") as content:
                    output.addfile(info, content)
    else:
        archive = destination / (name + ".zip")
        with ZipFile(archive, "w", compression=ZIP_DEFLATED) as output:
            for p in files:
                output.write(p, name + "/" + p.relative_to(source).as_posix())
    return hashlib.sha256(archive.read_bytes()).hexdigest() + "  " + archive.name

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for option in ("publish", "nuget", "out"):
        parser.add_argument("--" + option, type=Path, required=True)
    parser.add_argument("--source-ref", required=True, help="Full Git commit of the clean source used to build the editor")
    args = parser.parse_args()
    if not re.fullmatch(r"[0-9a-fA-F]{40}", args.source_ref):
        parser.error("--source-ref must be a full 40-character Git commit")
    args.out.mkdir(parents=True, exist_ok=True)
    lines = [package(args.publish / rid, args.nuget, args.out, rid, args.source_ref) for rid in ("linux-x64", "win-x64")]
    (args.out / "editor-SHA256SUMS.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("\n".join(lines))
