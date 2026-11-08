#!/usr/bin/env python3
"""Run registration, production, and restart checks in a disposable local server."""
import argparse
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game", default=os.environ.get("VINTAGE_STORY", "/Applications/Vintage Story.app"))
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET", "dotnet"))
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[1]
    game = Path(args.game).resolve()
    subprocess.run([args.dotnet, "build", str(repo / "tests/ResinGrove.Integration"),
                    "-c", "Release", f"-p:VintageStoryPath={game}", "--nologo"], check=True)
    subprocess.run([args.dotnet, "build", str(repo / "src/ResinGrove"), "-c", "Release",
                    "-t:Package", f"-p:VintageStoryPath={game}", "--nologo"], check=True)
    logs = repo / "artifacts/integration-logs"
    logs.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="resingrove-integration-") as temporary:
        data = Path(temporary)
        mods = data / "Mods"
        mods.mkdir()
        version = json.loads((repo / "modinfo.json").read_text())["version"]
        shutil.copy2(repo / f"artifacts/resingrove-{version}.zip", mods)
        checks = mods / "integration"
        checks.mkdir()
        shutil.copy2(repo / "tests/ResinGrove.Integration/modinfo.json", checks)
        shutil.copy2(repo / "tests/ResinGrove.Integration/bin/Release/net10.0/ResinGrove.Integration.dll", checks)
        with socket.socket() as sock:
            sock.bind(("127.0.0.1", 0))
            port = sock.getsockname()[1]
        config = json.dumps({"Ip": "127.0.0.1", "Port": port, "AdvertiseServer": False})
        for phase in (1, 2):
            print(f"Running isolated server check {phase}/2 on localhost:{port}", flush=True)
            process = subprocess.Popen([args.dotnet, str(game / "VintagestoryServer.dll"),
                                        "--dataPath", str(data), "--withconfig=" + config],
                                       cwd=game, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                       stderr=subprocess.STDOUT, text=True)
            try:
                output, _ = process.communicate(timeout=180)
            except subprocess.TimeoutExpired:
                process.terminate()
                try:
                    output, _ = process.communicate(timeout=15)
                except subprocess.TimeoutExpired:
                    process.kill()
                    output, _ = process.communicate()
                (logs / f"phase-{phase}.log").write_text(output)
                raise SystemExit(f"Server check timed out; see {logs}")
            (logs / f"phase-{phase}.log").write_text(output)
            for line in output.splitlines():
                if "RESINGROVE" in line:
                    print(line, flush=True)
            if "RESINGROVE INTEGRATION PASS" not in output or "RESINGROVE INTEGRATION FAIL" in output:
                print("\n".join(output.splitlines()[-35:]))
                raise SystemExit(f"Server check failed; see {logs}")
    print("Both server runs passed. The disposable world has been removed.")


if __name__ == "__main__":
    main()
