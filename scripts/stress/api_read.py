#!/usr/bin/env python3
"""Bounded HTTP read load generator for OnlineJudge stress preparation."""

from __future__ import annotations

import argparse
import concurrent.futures
import json
import math
import os
import statistics
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from collections import Counter


PUBLIC_PATHS = (
    "/api/leaderboards/users",
    "/api/leaderboards/challenges",
    "/api/site-settings/appearance",
)
AUTHENTICATED_PATHS = (
    "/api/problems",
    "/api/account/me",
    "/api/help-documents",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-url", required=True)
    parser.add_argument("--target-kind", required=True, choices=("production-read-sanity", "isolated-stress"))
    parser.add_argument("--vus", type=int, default=1)
    parser.add_argument("--duration-seconds", type=int, default=15)
    parser.add_argument("--timeout-seconds", type=float, default=5.0)
    parser.add_argument("--request-interval-ms", type=int, default=250)
    parser.add_argument("--token-env", default="OJ_STRESS_TOKEN")
    parser.add_argument("--safety-marker-file")
    parser.add_argument("--output")
    return parser.parse_args()


def validate_safety(args: argparse.Namespace) -> None:
    parsed = urllib.parse.urlparse(args.base_url)
    if parsed.scheme not in ("http", "https") or not parsed.hostname:
        raise SystemExit("base-url must be an absolute HTTP(S) URL")
    if args.vus < 1 or args.duration_seconds < 1 or args.timeout_seconds <= 0 or args.request_interval_ms < 0:
        raise SystemExit("vus, duration, and timeout must be positive")

    if args.target_kind == "production-read-sanity":
        if args.vus > 2 or args.duration_seconds > 15:
            raise SystemExit("production-read-sanity is hard-limited to 2 VUs and 15 seconds")
        if args.request_interval_ms < 250:
            raise SystemExit("production-read-sanity requires at least 250 ms between requests per VU")
        return

    if not args.safety_marker_file:
        raise SystemExit("isolated-stress requires --safety-marker-file")
    with open(args.safety_marker_file, "r", encoding="utf-8") as marker:
        if marker.read().strip() != "onlinejudge-stress":
            raise SystemExit("invalid isolated stress safety marker")


def percentile(values: list[float], percentile_value: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    rank = (len(ordered) - 1) * percentile_value
    lower = math.floor(rank)
    upper = math.ceil(rank)
    if lower == upper:
        return ordered[lower]
    return ordered[lower] + (ordered[upper] - ordered[lower]) * (rank - lower)


def discover_problem_path(base_url: str, headers: dict[str, str], timeout: float) -> str | None:
    request = urllib.request.Request(f"{base_url.rstrip('/')}/api/problems", headers=headers, method="GET")
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            payload = json.load(response)
    except (OSError, ValueError, urllib.error.HTTPError):
        return None
    if not isinstance(payload, list) or not payload or not isinstance(payload[0], dict):
        return None
    problem_id = payload[0].get("id")
    return f"/api/problems/{problem_id}" if problem_id else None


def main() -> int:
    args = parse_args()
    validate_safety(args)
    base_url = args.base_url.rstrip("/")
    token = os.environ.get(args.token_env, "").strip()
    headers = {"Accept": "application/json", "User-Agent": "onlinejudge-stress-preflight/1"}
    if token:
        headers["Authorization"] = f"Bearer {token}"

    paths = list(PUBLIC_PATHS)
    problem_path = discover_problem_path(base_url, headers, args.timeout_seconds) if token else None
    if problem_path:
        paths.append(problem_path)
    if token:
        paths.extend(AUTHENTICATED_PATHS)

    stop_at = time.monotonic() + args.duration_seconds
    lock = threading.Lock()
    latencies_ms: list[float] = []
    statuses: Counter[int] = Counter()
    failures: Counter[str] = Counter()

    def worker(worker_id: int) -> None:
        index = worker_id
        while time.monotonic() < stop_at:
            path = paths[index % len(paths)]
            index += 1
            started = time.perf_counter()
            status = 0
            failure = ""
            try:
                request = urllib.request.Request(f"{base_url}{path}", headers=headers, method="GET")
                with urllib.request.urlopen(request, timeout=args.timeout_seconds) as response:
                    status = response.status
                    response.read()
            except urllib.error.HTTPError as error:
                status = error.code
                error.read()
            except (OSError, TimeoutError) as error:
                failure = type(error).__name__
            latency = (time.perf_counter() - started) * 1000
            with lock:
                latencies_ms.append(latency)
                if status:
                    statuses[status] += 1
                else:
                    failures[failure or "UnknownError"] += 1
            if args.request_interval_ms:
                time.sleep(args.request_interval_ms / 1000)

    started = time.perf_counter()
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.vus) as executor:
        futures = [executor.submit(worker, worker_id) for worker_id in range(args.vus)]
        for future in futures:
            future.result()
    elapsed = time.perf_counter() - started

    requests = sum(statuses.values()) + sum(failures.values())
    success = sum(count for status, count in statuses.items() if 200 <= status < 400)
    client_errors = sum(count for status, count in statuses.items() if 400 <= status < 500)
    server_errors = sum(count for status, count in statuses.items() if status >= 500)
    result = {
        "targetKind": args.target_kind,
        "vus": args.vus,
        "durationSeconds": round(elapsed, 3),
        "requests": requests,
        "success": success,
        "failure": requests - success,
        "rps": round(requests / elapsed, 3) if elapsed else 0,
        "latencyMs": {
            "p50": round(percentile(latencies_ms, 0.50), 3),
            "p90": round(percentile(latencies_ms, 0.90), 3),
            "p95": round(percentile(latencies_ms, 0.95), 3),
            "p99": round(percentile(latencies_ms, 0.99), 3),
            "max": round(max(latencies_ms), 3) if latencies_ms else 0,
            "mean": round(statistics.fmean(latencies_ms), 3) if latencies_ms else 0,
        },
        "statusCodes": dict(sorted(statuses.items())),
        "status429": statuses[429],
        "status4xx": client_errors,
        "status5xx": server_errors,
        "transportFailures": dict(failures),
    }
    rendered = json.dumps(result, indent=2, sort_keys=True)
    if args.output:
        with open(args.output, "w", encoding="utf-8") as output_file:
            output_file.write(rendered + "\n")
    print(rendered)
    return 0 if requests > 0 and server_errors == 0 and not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
