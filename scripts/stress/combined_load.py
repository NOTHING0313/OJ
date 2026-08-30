#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
import shlex
import subprocess
import sys
import time
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run the bounded five-minute combined stress mix.")
    parser.add_argument("--ssh-target", required=True)
    parser.add_argument("--remote-client-env", required=True)
    parser.add_argument("--base-url", default="http://127.0.0.1:15101")
    parser.add_argument("--metrics-unit", required=True)
    parser.add_argument("--duration-seconds", type=int, default=300)
    parser.add_argument("--output-dir", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.duration_seconds < 30 or args.duration_seconds > 300:
        raise SystemExit("combined duration must be between 30 and 300 seconds")
    if not re.fullmatch(r"onlinejudge-stress-[a-zA-Z0-9_.@-]+\.service", args.metrics_unit):
        raise SystemExit("metrics-unit must identify an onlinejudge-stress service")
    output = Path(args.output_dir)
    output.mkdir(parents=True, exist_ok=True)
    script_root = Path(__file__).parent
    common = ["--ssh-target", args.ssh_target, "--remote-client-env", args.remote_client_env, "--base-url", args.base_url]
    commands = [
        [sys.executable, str(script_root / "authenticated_read.py"), *common, "--vus", "5", "--duration-seconds", str(args.duration_seconds), "--request-interval-ms", "50", "--output", str(output / "combined-read.json")],
        [sys.executable, str(script_root / "scenario_load.py"), "--scenario", "auth", *common, "--account-index", "61", "--vus", "5", "--duration-seconds", str(args.duration_seconds), "--request-interval-ms", "50", "--output", str(output / "combined-auth.json")],
        [sys.executable, str(script_root / "scenario_load.py"), "--scenario", "chat", *common, "--account-index", "62", "--vus", "1", "--duration-seconds", str(args.duration_seconds), "--request-interval-ms", "1000", "--output", str(output / "combined-chat.json")],
        [sys.executable, str(script_root / "scenario_load.py"), "--scenario", "submission", *common, "--account-index", "63", "--vus", "1", "--duration-seconds", str(args.duration_seconds), "--request-interval-ms", "10000", "--output", str(output / "combined-submission.json")],
        [sys.executable, str(script_root / "scenario_load.py"), "--scenario", "upload", *common, "--account-index", "64", "--vus", "1", "--duration-seconds", str(args.duration_seconds), "--request-interval-ms", "30000", "--payload-bytes", "1048576", "--output", str(output / "combined-upload.json")],
    ]
    processes: list[subprocess.Popen] = []
    streams = []
    try:
        for index, command in enumerate(commands):
            stdout = (output / f"combined-{index}.stdout.log").open("w", encoding="utf-8")
            stderr = (output / f"combined-{index}.stderr.log").open("w", encoding="utf-8")
            streams.extend((stdout, stderr))
            processes.append(subprocess.Popen(command, stdout=stdout, stderr=stderr))
        started = time.monotonic()
        while any(process.poll() is None for process in processes):
            time.sleep(15)
            health = subprocess.run(
                [
                    "ssh", "-o", "BatchMode=yes", args.ssh_target,
                    f"printf 'M='; systemctl is-active {shlex.quote(args.metrics_unit)}; "
                    "printf 'H='; curl -sS -o /dev/null -w '%{http_code}' http://127.0.0.1:5101/api/site-settings/appearance; "
                    "printf ' Q='; sudo docker exec onlinejudge-redis redis-cli LLEN judge:submissions:pending",
                ],
                check=True,
                capture_output=True,
                text=True,
            ).stdout.strip()
            running = sum(process.poll() is None for process in processes)
            print(f"COMBINED_ELAPSED={int(time.monotonic()-started)} RUNNING={running} {health}", flush=True)
            normalized_health = "".join(health.split())
            if "M=activeH=200Q=0" not in normalized_health:
                raise RuntimeError("combined load stop condition triggered")
        exit_codes = [process.wait() for process in processes]
        print(f"COMBINED_EXIT_CODES={exit_codes}")
        return 0 if all(code == 0 for code in exit_codes) else 1
    except Exception:
        for process in processes:
            if process.poll() is None:
                process.terminate()
        for process in processes:
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
        raise
    finally:
        for stream in streams:
            stream.close()


if __name__ == "__main__":
    raise SystemExit(main())
