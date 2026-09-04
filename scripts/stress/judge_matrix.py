#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import subprocess
import time
import urllib.error
import urllib.request
from pathlib import Path


TERMINAL_STATUSES = {3, 4, 5, 6, 7, 8, 9}
STATUS_NAMES = {1: "Pending", 2: "Judging", 3: "Accepted", 4: "WrongAnswer", 5: "TimeLimitExceeded", 6: "MemoryLimitExceeded", 7: "RuntimeError", 8: "CompileError", 9: "SystemError"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run one isolated judge outcome case per supported language.")
    parser.add_argument("--ssh-target", required=True)
    parser.add_argument("--remote-client-env", required=True)
    parser.add_argument("--base-url", default="http://127.0.0.1:15101")
    parser.add_argument("--account-index", type=int, default=40)
    parser.add_argument("--only", action="append", default=[])
    parser.add_argument("--output", required=True)
    return parser.parse_args()


def credentials(target: str, remote_path: str) -> tuple[str, str]:
    completed = subprocess.run(["ssh", "-o", "BatchMode=yes", target, "sudo", "cat", remote_path], check=True, capture_output=True, text=True)
    values = dict(line.split("=", 1) for line in completed.stdout.splitlines() if "=" in line)
    return values["STRESS_ROOT_ACCOUNT"], values["STRESS_ROOT_PASSWORD"]


def request(base: str, method: str, path: str, payload: dict | None = None, token: str = "") -> tuple[int, dict]:
    headers = {"Accept": "application/json"}
    data = None
    if payload is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(payload).encode("utf-8")
    if token:
        headers["Authorization"] = f"Bearer {token}"
    req = urllib.request.Request(f"{base.rstrip('/')}{path}", data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=20) as response:
            body = response.read()
            return response.status, json.loads(body) if body else {}
    except urllib.error.HTTPError as error:
        body = error.read()
        try:
            return error.code, json.loads(body) if body else {}
        except json.JSONDecodeError:
            return error.code, {}


def main() -> int:
    args = parse_args()
    _, password = credentials(args.ssh_target, args.remote_client_env)
    account = f"stress_user_{args.account_index:03d}"
    status, login = request(args.base_url, "POST", "/api/auth/login", {"account": account, "password": password})
    token = login.get("accessToken", "")
    if status != 200 or not token:
        raise RuntimeError(f"judge matrix login failed with HTTP {status}")
    status, problems = request(args.base_url, "GET", "/api/problems", token=token)
    problem = next((item for item in problems if item.get("title") == "STRESS Isolation A+B"), None)
    if status != 200 or not problem:
        raise RuntimeError("judge matrix problem unavailable")

    cases = [
        ("cpp17-ac", 1, 3, "#include <iostream>\nint main(){long long a,b;if(std::cin>>a>>b)std::cout<<a+b;}"),
        ("cpp17-ce", 1, 8, "int main( {"),
        ("cpp17-wa", 1, 4, "#include <iostream>\nint main(){std::cout<<0;}"),
        ("cpp17-tle", 1, 5, "int main(){for(;;){}}"),
        ("cpp17-mle", 1, 6, "#include <vector>\nint main(){std::vector<char> x(256*1024*1024);for(size_t i=0;i<x.size();i+=4096)x[i]=1;}"),
        ("c11-ac", 2, 3, "#include <stdio.h>\nint main(){long long a,b;if(scanf(\"%lld%lld\",&a,&b)==2)printf(\"%lld\",a+b);}"),
        ("c11-ce", 2, 8, "int main( {"),
        ("c11-wa", 2, 4, "#include <stdio.h>\nint main(){printf(\"0\");}"),
        ("c11-tle", 2, 5, "int main(){for(;;){}}"),
        ("c11-mle", 2, 6, "#include <stdlib.h>\nint main(){volatile char*x=malloc(256*1024*1024);if(!x)return 2;for(long i=0;i<256L*1024*1024;i+=4096)x[i]=1;return x[0];}"),
        ("csharp-ac", 3, 3, "using System; class Program{static void Main(){var p=Console.ReadLine()!.Split();Console.Write(long.Parse(p[0])+long.Parse(p[1]));}}"),
        ("csharp-ce", 3, 8, "class Program { static void Main( {"),
        ("csharp-wa", 3, 4, "using System; class Program{static void Main(){Console.Write(0);}}"),
        ("csharp-tle", 3, 5, "class Program{static void Main(){while(true){}}}"),
        ("csharp-mle", 3, 6, "using System; using System.Runtime.InteropServices; class Program{static void Main(){var p=Marshal.AllocHGlobal(256*1024*1024);for(int i=0;i<256*1024*1024;i+=4096)Marshal.WriteByte(p,i,1);}}"),
        ("cpp17-file-spam", 1, 5, "#include <fstream>\n#include <string>\nint main(){std::string data(65536,'x');for(int i=0;i<100;i++){std::ofstream f(\"stress-spam-\"+std::to_string(i));f<<data;}for(;;){}}"),
    ]
    if args.only:
        requested = set(args.only)
        cases = [item for item in cases if item[0] in requested]
        if {item[0] for item in cases} != requested:
            raise RuntimeError("unknown judge matrix case requested")
    results: list[dict] = []
    all_expected = True
    for name, language, expected, source in cases:
        started = time.perf_counter()
        while True:
            create_status, submission = request(
                args.base_url,
                "POST",
                "/api/submissions",
                {"problemId": problem["id"], "language": language, "sourceCode": source},
                token,
            )
            if create_status != 429:
                break
            time.sleep(1)
        if create_status != 201 or not submission.get("id"):
            raise RuntimeError(f"{name} submission failed with HTTP {create_status}")
        submission_id = submission["id"]
        final_status = 0
        deadline = time.monotonic() + 90
        while time.monotonic() < deadline:
            get_status, current = request(args.base_url, "GET", f"/api/submissions/{submission_id}", token=token)
            if get_status == 200 and current.get("status") in TERMINAL_STATUSES:
                final_status = current["status"]
                break
            time.sleep(0.5)
        matched = final_status == expected
        all_expected = all_expected and matched
        results.append({
            "case": name,
            "language": language,
            "expected": STATUS_NAMES[expected],
            "actual": STATUS_NAMES.get(final_status, "Timeout"),
            "matched": matched,
            "latencyMs": round((time.perf_counter() - started) * 1000, 3),
            "resourceEvaluation": current.get("evaluation") if final_status else None,
            "measuredCaseCount": len(current.get("caseResults", [])) if final_status else 0,
        })
    rendered = json.dumps({"allExpected": all_expected, "cases": results}, indent=2, sort_keys=True)
    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    password = ""
    token = ""
    return 0 if all_expected else 1


if __name__ == "__main__":
    raise SystemExit(main())
