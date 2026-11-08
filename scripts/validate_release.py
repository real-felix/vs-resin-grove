#!/usr/bin/env python3
"""Check version consistency and release ZIP layout without game dependencies."""
import argparse
import json
import os
from pathlib import Path
import re
import xml.etree.ElementTree as ET
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", default="", help="v<mod version>, or v<major> for a major.0.0 release")
    parser.add_argument("--package", action="store_true", help="Also inspect the built ZIP")
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[1]
    info = json.loads((repo / "modinfo.json").read_text())
    version = info["version"]
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", version):
        raise SystemExit("modinfo.json must contain a semantic version such as 1.0.0")
    project = ET.parse(repo / "src/ResinGrove/ResinGrove.csproj")
    if project.findtext(".//Version") != version:
        raise SystemExit("Versions in modinfo.json and ResinGrove.csproj must match")
    tags = {f"v{version}"}
    if version.endswith(".0.0"):
        tags.add(f"v{version.removesuffix('.0.0')}")
    if args.tag and args.tag not in tags:
        raise SystemExit(f"Tag must be {' or '.join(sorted(tags))}, got {args.tag!r}")
    for asset in (repo / "assets").rglob("*.json"):
        json.loads(asset.read_text())

    if args.package:
        archive = repo / "artifacts" / f"resingrove-{version}.zip"
        with zipfile.ZipFile(archive) as package:
            names = set(package.namelist())
            if not {"modinfo.json", "ResinGrove.dll"} <= names:
                raise SystemExit("ZIP must contain modinfo.json and ResinGrove.dll at its root")
            if json.loads(package.read("modinfo.json")) != info:
                raise SystemExit("Packaged modinfo.json differs from the repository")
            for asset in (repo / "assets").rglob("*"):
                if asset.is_file() and asset.relative_to(repo).as_posix() not in names:
                    raise SystemExit(f"Missing packaged asset: {asset}")
            for optional in ("modicon.png", "LICENSE"):
                if (repo / optional).exists() and optional not in names:
                    raise SystemExit(f"Missing packaged file: {optional}")
            if any(name.lower().endswith(("vintagestoryapi.dll", "vssurvivalmod.dll", "vsessentials.dll"))
                   or "Integration" in name for name in names):
                raise SystemExit("ZIP must not redistribute game DLLs or the integration test mod")

    print(f"Release metadata{' and ZIP' if args.package else ''} valid: {version}")
    if output := os.environ.get("GITHUB_OUTPUT"):
        with open(output, "a") as stream:
            stream.write(f"version={version}\n")


if __name__ == "__main__":
    main()
