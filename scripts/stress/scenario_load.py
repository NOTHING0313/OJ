#!/usr/bin/env python3
from __future__ import annotations

import argparse
import concurrent.futures
import json
import math
import os
import statistics
import struct
import subprocess
import threading
import time
import urllib.error
import urllib.request
import uuid
import zlib
from collections import Counter
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Controlled isolated OnlineJudge scenario load generator.")
    parser.add_argument("--scenario", required=True, choices=("auth", "login-normal", "login-wrong", "login-many", "login-same", "chat", "upload", "submission", "git"))
    parser.add_argument("--ssh-target", required=True)
    parser.add_argument("--remote-client-env", required=True)
    parser.add_argument("--base-url", default="http://127.0.0.1:15101")
    parser.add_argument("--vus", type=int, required=True)
    parser.add_argument("--duration-seconds", type=int, default=0)
    parser.add_argument("--iterations-per-vu", type=int, default=1)
    parser.add_argument("--request-interval-ms", type=int, default=0)
    parser.add_argument("--payload-bytes", type=int, default=102400)
    parser.add_argument("--account-index", type=int, default=0)
    parser.add_argument("--git-repository-url")
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def percentile(values: list[float], value: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    rank = (len(ordered) - 1) * value
    lower, upper = math.floor(rank), math.ceil(rank)
    if lower == upper:
        return ordered[lower]
    return ordered[lower] + (ordered[upper] - ordered[lower]) * (rank - lower)


def read_credentials(target: str, remote_path: str) -> tuple[str, str]:
    completed = subprocess.run(["ssh", "-o", "BatchMode=yes", target, "sudo", "cat", remote_path], check=True, capture_output=True, text=True)
    values: dict[str, str] = {}
    for line in completed.stdout.splitlines():
        key, separator, value = line.partition("=")
        if separator and key in {"STRESS_ROOT_ACCOUNT", "STRESS_ROOT_PASSWORD"}:
            values[key] = value
    if not values.get("STRESS_ROOT_ACCOUNT") or not values.get("STRESS_ROOT_PASSWORD"):
        raise RuntimeError("stress credentials unavailable")
    return values["STRESS_ROOT_ACCOUNT"], values["STRESS_ROOT_PASSWORD"]


def send(base_url: str, method: str, path: str, payload: bytes | None, headers: dict[str, str], timeout: float = 30) -> tuple[int, bytes]:
    request = urllib.request.Request(f"{base_url.rstrip('/')}{path}", data=payload, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()


def json_request(base_url: str, method: str, path: str, payload: dict | None = None, token: str = "", timeout: float = 30) -> tuple[int, dict]:
    headers = {"Accept": "application/json"}
    data = None
    if payload is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(payload).encode("utf-8")
    if token:
        headers["Authorization"] = f"Bearer {token}"
    status, body = send(base_url, method, path, data, headers, timeout)
    try:
        return status, json.loads(body) if body else {}
    except json.JSONDecodeError:
        return status, {}


def login(base_url: str, account: str, password: str) -> str:
    status, body = json_request(base_url, "POST", "/api/auth/login", {"account": account, "password": password})
    token = body.get("accessToken", "")
    if status != 200 or not token:
        raise RuntimeError(f"stress login failed with HTTP {status}")
    return token


def png_payload(target_size: int) -> bytes:
    if target_size < 128:
        raise ValueError("PNG payload target is too small")

    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    signature = b"\x89PNG\r\n\x1a\n"
    ihdr = chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0))
    idat = chunk(b"IDAT", zlib.compress(b"\x00\x00\x00\x00\xff"))
    iend = chunk(b"IEND", b"")
    fixed = signature + ihdr + idat + iend
    padding_length = target_size - len(fixed) - 12
    if padding_length < 8:
        raise ValueError("PNG payload target cannot hold metadata")
    text = b"Comment\x00" + b"A" * (padding_length - 8)
    return signature + ihdr + idat + chunk(b"tEXt", text) + iend


def multipart_png(target_size: int) -> tuple[bytes, str]:
    boundary = f"----onlinejudge-stress-{uuid.uuid4().hex}"
    image = png_payload(target_size)
    prefix = (
        f"--{boundary}\r\n"
        'Content-Disposition: form-data; name="file"; filename="stress.png"\r\n'
        "Content-Type: image/png\r\n\r\n"
    ).encode("ascii")
    return prefix + image + f"\r\n--{boundary}--\r\n".encode("ascii"), boundary


def main() -> int:
    args = parse_args()
    if args.vus < 1 or args.vus > 80 or args.duration_seconds < 0 or args.iterations_per_vu < 1 or args.request_interval_ms < 0:
        raise SystemExit("invalid bounded load arguments")
    if args.scenario == "git" and not args.git_repository_url:
        raise SystemExit("git scenario requires --git-repository-url")
    account, password = read_credentials(args.ssh_target, args.remote_client_env)
    if args.account_index:
        if args.account_index < 1 or args.account_index > 99:
            raise SystemExit("account-index must be between 1 and 99")
        account = f"stress_user_{args.account_index:03d}"
    token = ""
    team_id = ""
    project_id = ""
    problem_id = ""
    if args.scenario in {"auth", "chat", "upload", "submission", "git"}:
        token = login(args.base_url, account, password)
    if args.scenario in {"chat", "git"}:
        status, team = json_request(args.base_url, "GET", "/api/teams/my", token=token)
        if status != 200 or not team.get("id"):
            raise RuntimeError("stress team discovery failed")
        team_id = team["id"]
    if args.scenario == "git":
        status, projects = json_request(args.base_url, "GET", f"/api/teams/{team_id}/projects", token=token)
        if status != 200:
            raise RuntimeError("stress project discovery failed")
        project = next((item for item in projects if item.get("name") == "STRESS Capacity Repository"), None)
        if project is None:
            status, project = json_request(
                args.base_url,
                "POST",
                f"/api/teams/{team_id}/projects",
                {"name": "STRESS Capacity Repository", "repositoryUrl": args.git_repository_url},
                token,
            )
        if status not in {200, 201} or not project.get("id"):
            raise RuntimeError(f"stress project setup failed with HTTP {status}")
        project_id = project["id"]
    if args.scenario == "submission":
        status, problems = json_request(args.base_url, "GET", "/api/problems", token=token)
        if status != 200 or not problems:
            raise RuntimeError("stress problem discovery failed")
        isolation_problem = next((item for item in problems if item.get("title") == "STRESS Isolation A+B"), problems[0])
        problem_id = isolation_problem["id"]

    lock = threading.Lock()
    latencies: list[float] = []
    statuses: Counter[int] = Counter()
    failures: Counter[str] = Counter()
    stop_at = time.monotonic() + args.duration_seconds if args.duration_seconds else 0

    def run_one(worker_id: int, index: int) -> None:
        started = time.perf_counter()
        status = 0
        try:
            if args.scenario == "auth":
                status, _ = json_request(args.base_url, "GET", "/api/account/me", token=token)
            elif args.scenario.startswith("login-"):
                login_account = account
                login_password = password
                if args.scenario == "login-many":
                    login_account = f"stress_user_{((worker_id + index) % 99) + 1:03d}"
                elif args.scenario == "login-wrong":
                    login_password = password + "-wrong"
                status, _ = json_request(args.base_url, "POST", "/api/auth/login", {"account": login_account, "password": login_password})
            elif args.scenario == "chat":
                status, _ = json_request(args.base_url, "POST", f"/api/teams/{team_id}/chat", {"content": f"stress-{worker_id}-{index}-{uuid.uuid4().hex[:8]}"}, token)
            elif args.scenario == "upload":
                body, boundary = multipart_png(args.payload_bytes)
                status, _ = send(
                    args.base_url,
                    "POST",
                    "/api/uploads/images",
                    body,
                    {"Authorization": f"Bearer {token}", "Content-Type": f"multipart/form-data; boundary={boundary}"},
                )
            elif args.scenario == "submission":
                status, _ = json_request(
                    args.base_url,
                    "POST",
                    "/api/submissions",
                    {"problemId": problem_id, "language": 1, "sourceCode": "#include <iostream>\nint main(){long long a,b;if(std::cin>>a>>b)std::cout<<a+b;}"},
                    token,
                )
            elif args.scenario == "git":
                status, _ = json_request(args.base_url, "POST", f"/api/teams/{team_id}/projects/{project_id}/sync", token=token, timeout=90)
        except (OSError, TimeoutError) as error:
            with lock:
                failures[type(error).__name__] += 1
        finally:
            latency = (time.perf_counter() - started) * 1000
            with lock:
                latencies.append(latency)
                if status:
                    statuses[status] += 1

    def worker(worker_id: int) -> None:
        index = 0
        while (stop_at and time.monotonic() < stop_at) or (not stop_at and index < args.iterations_per_vu):
            run_one(worker_id, index)
            index += 1
            if args.request_interval_ms:
                time.sleep(args.request_interval_ms / 1000)

    started = time.perf_counter()
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.vus) as executor:
        for future in [executor.submit(worker, worker_id) for worker_id in range(args.vus)]:
            future.result()
    elapsed = time.perf_counter() - started
    requests = sum(statuses.values()) + sum(failures.values())
    result = {
        "scenario": args.scenario,
        "vus": args.vus,
        "durationSeconds": round(elapsed, 3),
        "requests": requests,
        "rps": round(requests / elapsed, 3) if elapsed else 0,
        "latencyMs": {
            "mean": round(statistics.fmean(latencies), 3) if latencies else 0,
            "p50": round(percentile(latencies, 0.50), 3),
            "p95": round(percentile(latencies, 0.95), 3),
            "p99": round(percentile(latencies, 0.99), 3),
            "max": round(max(latencies), 3) if latencies else 0,
        },
        "statusCodes": dict(sorted(statuses.items())),
        "status429": statuses[429],
        "status5xx": sum(count for status, count in statuses.items() if status >= 500),
        "transportFailures": dict(failures),
    }
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    rendered = json.dumps(result, indent=2, sort_keys=True)
    output.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    password = ""
    token = ""
    return 0 if result["status5xx"] == 0 and not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
