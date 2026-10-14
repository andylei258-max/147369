#!/usr/bin/env python3
"""Synthetic JPEG over real loopback TCP; not a Windows capture or Android test."""
import pathlib
import subprocess
import tempfile
import time

root = pathlib.Path(__file__).resolve().parent.parent
with tempfile.TemporaryDirectory() as temp:
    folder = pathlib.Path(temp)
    subprocess.run(["javac", "-d", temp, str(root / "android/app/src/main/java/org/tabletextend/receiver/WirePacket.java"), str(root / "tests/WireInterop.java")], check=True)
    java = ["java", "-Djava.awt.headless=true", "-cp", temp, "WireInterop"]
    fixture, ready = folder / "fixture.jpg", folder / "port.txt"
    subprocess.run(java + ["fixture", str(fixture)], check=True)
    server = subprocess.Popen(["dotnet", str(root / "tests/TabletExtend.Checks/bin/Release/net8.0/TabletExtend.Checks.dll"), "--interop", str(fixture), str(ready)])
    try:
        deadline = time.monotonic() + 3
        while not ready.exists():
            if server.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError("C# server did not start")
            time.sleep(0.05)
        subprocess.run(java + [ready.read_text()], check=True, timeout=5)
        if server.wait(timeout=8) != 0:
            raise RuntimeError("C# server failed")
    finally:
        if server.poll() is None:
            server.terminate()
            server.wait(timeout=5)
