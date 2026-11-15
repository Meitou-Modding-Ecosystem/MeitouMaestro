"""Lay out the Windows x64 editor with an app-relative .NET 11 desktop runtime."""
import argparse
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def prepare_editor(dotnet_root, output, version):
    binary = ROOT / "App" / "bin" / "Release" / "net11.0-windows" / "publish"
    output.mkdir(parents=True, exist_ok=True)
    for file in ("Maestro.exe", "Maestro.dll", "Maestro.deps.json", "Maestro.runtimeconfig.json", "Maestro.Core.dll"):
        shutil.copy2(binary / file, output / file)
    runtime = output / "runtime"
    for component in ("host/fxr", "shared/Microsoft.NETCore.App", "shared/Microsoft.WindowsDesktop.App"):
        part = Path(component) / version
        shutil.copytree(dotnet_root / part, runtime / part, dirs_exist_ok=True)
    for file in ("dotnet.exe", "LICENSE.txt", "ThirdPartyNotices.txt"):
        shutil.copy2(dotnet_root / file, runtime / file)


if __name__ == "__main__":
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("--dotnet-root", required=True, type=Path)
    cli.add_argument("--output", required=True, type=Path)
    cli.add_argument("--runtime-version", default="11.0.0")
    args = cli.parse_args()
    prepare_editor(args.dotnet_root, args.output, args.runtime_version)
