#!/usr/bin/env bash
set -euo pipefail
task_root="$(cd "$(dirname "$0")/.." && pwd)"
task_temp="$(mktemp -d)"
trap 'rm -rf "$task_temp"' EXIT
javac -d "$task_temp" "$task_root/android/app/src/main/java/org/tabletextend/receiver/WirePacket.java" "$task_root/tests/WirePacketChecks.java"
java -cp "$task_temp" WirePacketChecks
