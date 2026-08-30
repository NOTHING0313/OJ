#!/usr/bin/env python3
import json
import os
import sys
import urllib.error
import urllib.request


BASE_URL = os.environ.get("STRESS_BASE_URL", "http://127.0.0.1:15101").rstrip("/")
ACCOUNT = os.environ.get("STRESS_ROOT_ACCOUNT", "")
PASSWORD = os.environ.get("STRESS_ROOT_PASSWORD", "")


def request(path: str, payload: dict, token: str | None = None) -> tuple[int, dict]:
    data = json.dumps(payload).encode("utf-8")
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    req = urllib.request.Request(f"{BASE_URL}{path}", data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=15) as response:
            body = response.read()
            return response.status, json.loads(body) if body else {}
    except urllib.error.HTTPError as error:
        body = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"{path} returned HTTP {error.code}: {body[:200]}") from error


def main() -> int:
    if not ACCOUNT or not PASSWORD:
        raise RuntimeError("stress credentials are not configured")
    login_status, login = request("/api/auth/login", {"account": ACCOUNT, "password": PASSWORD})
    token = login.get("accessToken")
    if login_status != 200 or not token:
        raise RuntimeError("stress login did not return an access token")

    problem_status, problem = request(
        "/api/problems",
        {
            "title": "STRESS Isolation A+B",
            "description": "Synthetic isolation probe only.",
            "inputDescription": "Two integers.",
            "outputDescription": "Their sum.",
            "timeLimitMs": 1000,
            "memoryLimitMb": 128,
            "isPublished": True,
            "judgeMode": 1,
            "allowedLanguagesMask": 7,
        },
        token,
    )
    problem_id = problem.get("id")
    if problem_status != 201 or not problem_id:
        raise RuntimeError("stress problem creation failed")

    case_status, test_case = request(
        f"/api/problems/{problem_id}/test-cases",
        {"input": "1 2\n", "expectedOutput": "3\n", "visibility": 1, "score": 100},
        token,
    )
    if case_status != 200 or not test_case.get("id"):
        raise RuntimeError("stress test case creation failed")

    submission_status, submission = request(
        "/api/submissions",
        {
            "problemId": problem_id,
            "language": 1,
            "sourceCode": "#include <iostream>\nint main(){long long a,b;if(std::cin>>a>>b)std::cout<<a+b;}",
        },
        token,
    )
    submission_id = submission.get("id")
    if submission_status != 201 or not submission_id:
        raise RuntimeError("stress submission creation failed")

    print(json.dumps({
        "loginStatus": login_status,
        "problemStatus": problem_status,
        "testCaseStatus": case_status,
        "submissionStatus": submission_status,
        "problemId": problem_id,
        "submissionId": submission_id,
    }, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"ISOLATION_PROBE_FAILED: {error}", file=sys.stderr)
        raise SystemExit(1)
