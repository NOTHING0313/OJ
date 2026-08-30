#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import tempfile
import urllib.request
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run authenticated read load without persisting stress credentials.")
    parser.add_argument("--ssh-target", required=True)
    parser.add_argument("--remote-client-env", required=True)
    parser.add_argument("--base-url", default="http://127.0.0.1:15101")
    parser.add_argument("--vus", type=int, required=True)
    parser.add_argument("--duration-seconds", type=int, default=60)
    parser.add_argument("--request-interval-ms", type=int, default=0)
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def read_credentials(ssh_target: str, remote_client_env: str) -> tuple[str, str]:
    completed = subprocess.run(
        ["ssh", "-o", "BatchMode=yes", ssh_target, "sudo", "cat", remote_client_env],
        check=True,
        capture_output=True,
        text=True,
    )
    values: dict[str, str] = {}
    for line in completed.stdout.splitlines():
        key, separator, value = line.partition("=")
        if separator and key in {"STRESS_ROOT_ACCOUNT", "STRESS_ROOT_PASSWORD"}:
            values[key] = value
    account = values.get("STRESS_ROOT_ACCOUNT", "")
    password = values.get("STRESS_ROOT_PASSWORD", "")
    if not account or not password:
        raise RuntimeError("stress client credentials were unavailable")
    return account, password


def login(base_url: str, account: str, password: str) -> str:
    payload = json.dumps({"account": account, "password": password}).encode("utf-8")
    request = urllib.request.Request(
        f"{base_url.rstrip('/')}/api/auth/login",
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=15) as response:
        body = json.load(response)
    token = body.get("accessToken", "")
    if not token:
        raise RuntimeError("stress login did not return an access token")
    return token


def main() -> int:
    args = parse_args()
    account, password = read_credentials(args.ssh_target, args.remote_client_env)
    token = login(args.base_url, account, password)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    child_env = os.environ.copy()
    child_env["OJ_STRESS_TOKEN"] = token
    script = Path(__file__).with_name("api_read.py")
    marker_path = ""
    try:
        with tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False) as marker:
            marker.write("onlinejudge-stress\n")
            marker_path = marker.name
        completed = subprocess.run(
            [
                sys.executable,
                str(script),
                "--base-url", args.base_url,
                "--target-kind", "isolated-stress",
                "--safety-marker-file", marker_path,
                "--token-env", "OJ_STRESS_TOKEN",
                "--vus", str(args.vus),
                "--duration-seconds", str(args.duration_seconds),
                "--request-interval-ms", str(args.request_interval_ms),
                "--output", str(output),
            ],
            env=child_env,
            check=False,
        )
        return completed.returncode
    finally:
        child_env.pop("OJ_STRESS_TOKEN", None)
        token = ""
        password = ""
        if marker_path:
            Path(marker_path).unlink(missing_ok=True)


if __name__ == "__main__":
    raise SystemExit(main())
