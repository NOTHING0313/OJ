#!/usr/bin/env python3
import json
import os
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

API_BASE = os.environ.get("OJ_SMOKE_API_BASE", "http://127.0.0.1:5101").rstrip("/")
TIMEOUT_SECONDS = int(os.environ.get("OJ_SMOKE_TIMEOUT_SECONDS", "120"))
RUN_ID = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
ACCOUNT = os.environ.get("RootAccount__UserName", "")
PASSWORD = os.environ.get("RootAccount__Password", "")
STATUS_NAMES = {1:"Pending",2:"Judging",3:"Accepted",4:"WrongAnswer",5:"TimeLimitExceeded",6:"MemoryLimitExceeded",7:"RuntimeError",8:"CompileError",9:"SystemError"}
TERMINAL = {"Accepted","WrongAnswer","TimeLimitExceeded","MemoryLimitExceeded","RuntimeError","CompileError","SystemError"}
token = None
user_id = None
standard_problem_id = None
challenge_id = None
challenge_task_id = None
function_problem_id = None
results = []

def log(msg=""):
    print(msg, flush=True)

def step(msg):
    log(); log(f">>> {msg}")

def passed(msg):
    log(f"[PASS] {msg}")

def fail(msg):
    raise RuntimeError(msg)

def api(method, path, body=None, auth=True):
    url = API_BASE + path
    data = None
    hs = {"Accept": "application/json"}
    if auth and token:
        hs["Authorization"] = f"Bearer {token}"
    if body is not None:
        data = json.dumps(body, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
        hs["Content-Type"] = "application/json; charset=utf-8"
    req = urllib.request.Request(url, data=data, headers=hs, method=method)
    try:
        with urllib.request.urlopen(req, timeout=20) as resp:
            raw = resp.read()
            if not raw:
                return None
            text = raw.decode("utf-8")
            if "json" in resp.headers.get("Content-Type", "").lower() or text[:1] in ("{", "["):
                return json.loads(text)
            return text
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"API {method} {path} failed: HTTP {exc.code}: {detail}") from exc
    except Exception as exc:
        raise RuntimeError(f"API {method} {path} failed: {exc}") from exc

def find_named(value, names):
    names = {n.lower() for n in names}
    if isinstance(value, dict):
        for k, v in value.items():
            if str(k).lower() in names:
                return v
        for v in value.values():
            found = find_named(v, names)
            if found is not None:
                return found
    elif isinstance(value, list):
        for item in value:
            found = find_named(item, names)
            if found is not None:
                return found
    return None

def find_guid(value, names=("id", "submissionId", "problemId")):
    v = find_named(value, names)
    if v is None:
        return None
    s = str(v).strip()
    return s if len(s) >= 32 else None

def status_name(raw):
    if isinstance(raw, int):
        return STATUS_NAMES.get(raw, f"Unknown({raw})")
    text = str(raw)
    try:
        return STATUS_NAMES.get(int(text), f"Unknown({text})")
    except ValueError:
        return text

def wait_submission(submission_id):
    deadline = time.time() + TIMEOUT_SECONDS
    last = None
    while time.time() < deadline:
        last = api("GET", f"/api/submissions/{submission_id}")
        status = status_name(find_named(last, ("status",)))
        if status in TERMINAL:
            return status, last
        time.sleep(0.8)
    fail(f"Submission {submission_id} timed out")

def submit(problem_id, language, source_code, challenge_task=None):
    created = api("POST", "/api/submissions", {
        "problemId": problem_id,
        "userId": user_id,
        "challengeTaskId": challenge_task,
        "language": language,
        "sourceCode": source_code,
    })
    sid = find_guid(created, ("id", "submissionId"))
    if not sid:
        fail("Submission creation did not return id")
    return sid

def submit_expect(name, problem_id, language, source_code, expected, challenge_task=None, expected_case_count=None, expected_accepted_count=None):
    step(name)
    sid = submit(problem_id, language, source_code, challenge_task)
    status, body = wait_submission(sid)
    if status != expected:
        fail(f"{name}: expected {expected}, actual {status}; response={json.dumps(body, ensure_ascii=False)}")
    cases = body.get("caseResults") or []
    if expected_case_count is not None and len(cases) != expected_case_count:
        fail(f"{name}: expected {expected_case_count} case results, actual {len(cases)}")
    if expected_accepted_count is not None:
        accepted = sum(1 for c in cases if status_name(c.get("status")) == "Accepted")
        if accepted != expected_accepted_count:
            fail(f"{name}: expected accepted cases={expected_accepted_count}, actual={accepted}")
    passed(f"{name} -> {status}")
    results.append({"test":name,"submissionId":sid,"status":status,"caseCount":len(cases)})
    return body

def assert_challenge_score(expected_score, completed, completed_count):
    detail = api("GET", f"/api/challenges/{challenge_id}")
    task = next((x for x in (detail.get("tasks") or []) if str(x.get("id")) == str(challenge_task_id)), None)
    if task is None:
        fail("Challenge detail missing temporary task")
    if int(task.get("earnedScore", -1)) != expected_score:
        fail(f"earnedScore expected {expected_score}, actual {task.get('earnedScore')}")
    if bool(task.get("isCompleted")) != completed:
        fail(f"isCompleted expected {completed}, actual {task.get('isCompleted')}")
    if int(detail.get("completedTaskCount", -1)) != completed_count:
        fail(f"completedTaskCount expected {completed_count}, actual {detail.get('completedTaskCount')}")
    board = api("GET", f"/api/challenges/{challenge_id}/leaderboard")
    entry = next((x for x in (board.get("entries") or []) if str(x.get("userId")) == str(user_id)), None)
    if entry is None:
        fail("Leaderboard missing current root user")
    if int(entry.get("totalScore", -1)) != expected_score:
        fail(f"Leaderboard totalScore expected {expected_score}, actual {entry.get('totalScore')}")
    if int(entry.get("completedTaskCount", -1)) != completed_count:
        fail(f"Leaderboard completedTaskCount expected {completed_count}, actual {entry.get('completedTaskCount')}")
    passed(f"Challenge Score={expected_score}/300; Completed={completed}; CompletedTaskCount={completed_count}")

CPP_ACCEPTED = r'''#include <iostream>
int main(){ long long x; if(!(std::cin>>x)) return 0; std::cout<<x*10; return 0; }
'''
C_ACCEPTED = r'''#include <stdio.h>
int main(void){ long long x; if(scanf("%lld",&x)!=1) return 0; printf("%lld",x*10); return 0; }
'''
CSHARP_ACCEPTED = r'''using System;
public static class Program { public static void Main(){ var s=Console.ReadLine(); if(long.TryParse(s,out var x)) Console.Write(x*10); } }
'''
CPP_PARTIAL_50 = r'''#include <iostream>
int main(){ int x; if(!(std::cin>>x)) return 0; if(x==1) std::cout<<10; else if(x==4) std::cout<<40; else std::cout<<-1; return 0; }
'''
CPP_LOWER_30 = r'''#include <iostream>
int main(){ int x; if(!(std::cin>>x)) return 0; if(x==1) std::cout<<10; else if(x==2) std::cout<<20; else std::cout<<-1; return 0; }
'''

FUNCTION_SPEC = json.dumps({
    "types":[
        {"name":"Point3","fields":[{"name":"x","type":"double"},{"name":"y","type":"double"},{"name":"z","type":"double"}]},
        {"name":"Triangle","fields":[{"name":"a","type":"Point3"},{"name":"b","type":"Point3"},{"name":"c","type":"Point3"}]},
        {"name":"Segment3","fields":[{"name":"a","type":"Point3"},{"name":"b","type":"Point3"}]}
    ],
    "functionName":"geometryScore","returnType":"double",
    "parameters":[{"name":"triangles","type":"Triangle[]"},{"name":"segments","type":"Segment3[]"}],
    "supportedLanguages":["cpp17","csharp","c11"]
}, separators=(",",":"))
CPP_FUNCTION = r'''struct Point3{double x;double y;double z;};
struct Triangle{Point3 a;Point3 b;Point3 c;};
struct Segment3{Point3 a;Point3 b;};
class Solution{public:double geometryScore(vector<Triangle>& triangles,vector<Segment3>& segments){double score=triangles.size()*100.0+segments.size()*10.0;if(!triangles.empty())score+=triangles[0].a.x+triangles[0].b.y+triangles[0].c.z;if(!segments.empty())score+=segments[0].a.x+segments[0].b.z;return score;}};
'''
C_FUNCTION = r'''typedef struct Point3{double x;double y;double z;}Point3;
typedef struct Triangle{Point3 a;Point3 b;Point3 c;}Triangle;
typedef struct Segment3{Point3 a;Point3 b;}Segment3;
double geometryScore(Triangle* triangles,int trianglesSize,Segment3* segments,int segmentsSize){double score=trianglesSize*100.0+segmentsSize*10.0;if(trianglesSize>0)score+=triangles[0].a.x+triangles[0].b.y+triangles[0].c.z;if(segmentsSize>0)score+=segments[0].a.x+segments[0].b.z;return score;}
'''
CSHARP_FUNCTION = r'''public class Point3{public double x;public double y;public double z;}
public class Triangle{public Point3 a=new();public Point3 b=new();public Point3 c=new();}
public class Segment3{public Point3 a=new();public Point3 b=new();}
public class Solution{public double GeometryScore(Triangle[] triangles,Segment3[] segments){double score=triangles.Length*100.0+segments.Length*10.0;if(triangles.Length>0)score+=triangles[0].a.x+triangles[0].b.y+triangles[0].c.z;if(segments.Length>0)score+=segments[0].a.x+segments[0].b.z;return score;}}
'''
STARTER_CODE = json.dumps({"cpp17":CPP_FUNCTION,"csharp":CSHARP_FUNCTION,"c11":C_FUNCTION}, separators=(",",":"))
ARGS1 = json.dumps({"triangles":[{"a":{"x":1.0,"y":2.0,"z":3.0},"b":{"x":4.0,"y":5.0,"z":6.0},"c":{"x":7.0,"y":8.0,"z":9.0}}],"segments":[{"a":{"x":10.0,"y":11.0,"z":12.0},"b":{"x":13.0,"y":14.0,"z":15.0}}]}, separators=(",",":"))
ARGS2 = json.dumps({"triangles":[],"segments":[]}, separators=(",",":"))

def cleanup():
    if challenge_id and token:
        try:
            step("Cleanup Temporary Challenge"); api("DELETE", f"/api/challenges/{challenge_id}"); passed("Temporary Challenge Cleanup")
        except Exception as exc:
            log(f"[WARN] Challenge cleanup failed: {exc}")
    for pid, label in ((function_problem_id,"Function Problem"),(standard_problem_id,"Standard Problem")):
        if pid and token:
            try:
                step(f"Cleanup Temporary {label}"); api("DELETE", f"/api/problems/{pid}"); passed(f"Temporary {label} Cleanup")
            except Exception as exc:
                log(f"[WARN] {label} cleanup failed: {exc}")

def main():
    global token,user_id,standard_problem_id,challenge_id,challenge_task_id,function_problem_id
    log("========================================"); log("OnlineJudge Production Smoke"); log("========================================")
    log(f"RunId   : {RUN_ID}"); log(f"ApiBase : {API_BASE}"); log("Secrets : loaded from process environment; not printed")
    if not ACCOUNT or not PASSWORD:
        fail("RootAccount__UserName / RootAccount__Password missing from process environment")
    step("Production API Health")
    if not isinstance(api("GET","/api/site-settings/appearance",auth=False),dict):
        fail("Appearance API did not return JSON object")
    passed("Production API")
    step("Root Login")
    login=api("POST","/api/auth/login",{"account":ACCOUNT,"password":PASSWORD},auth=False)
    tv=find_named(login,("accessToken","token","jwtToken","jwt"))
    if not tv: fail("Login succeeded but JWT not found")
    token=str(tv)
    me=api("GET","/api/auth/me")
    user_id=find_guid(me,("id","userId"))
    if not user_id: fail("Unable to resolve root user id")
    passed("Root Login")
    step("Create Temporary Standard Judge Problem")
    problem=api("POST","/api/problems",{"title":f"[PROD-SMOKE] Standard Judge {RUN_ID}","description":"Temporary production judge smoke problem.","inputDescription":"One integer x.","outputDescription":"Print x * 10.","timeLimitMs":3000,"memoryLimitMb":512,"isPublished":True,"judgeMode":1,"functionSpecJson":None,"starterCodeJson":None})
    standard_problem_id=find_guid(problem,("id","problemId"))
    if not standard_problem_id: fail("Standard problem creation did not return id")
    for inp,out,score,visibility in [("1\n","10",10,1),("2\n","20",20,2),("3\n","30",30,2),("4\n","40",40,2)]:
        api("POST",f"/api/problems/{standard_problem_id}/test-cases",{"input":inp,"expectedOutput":out,"argumentsJson":None,"expectedJson":None,"visibility":visibility,"score":score})
    passed(f"Temporary Standard Problem {standard_problem_id}; Score Total=100")
    submit_expect("C++17 Standard Judge",standard_problem_id,1,CPP_ACCEPTED,"Accepted",expected_case_count=4,expected_accepted_count=4)
    submit_expect("C11 Standard Judge",standard_problem_id,2,C_ACCEPTED,"Accepted",expected_case_count=4,expected_accepted_count=4)
    submit_expect("C# Standard Judge",standard_problem_id,3,CSHARP_ACCEPTED,"Accepted",expected_case_count=4,expected_accepted_count=4)
    step("Create Temporary Challenge")
    now=datetime.now(timezone.utc)
    ch=api("POST","/api/challenges",{"title":f"[PROD-SMOKE] Challenge Score {RUN_ID}","description":"Temporary production partial-score smoke.","startAt":(now-timedelta(minutes=5)).isoformat(),"endAt":(now+timedelta(hours=2)).isoformat(),"isPublished":True})
    challenge_id=find_guid(ch,("id","challengeId"))
    if not challenge_id: fail("Challenge creation did not return id")
    task=api("POST",f"/api/challenges/{challenge_id}/tasks",{"title":"Partial Score Task","description":"100 testcase points map to 300 challenge points.","taskType":1,"difficulty":1,"boardX":0,"boardY":0,"algorithmProblemId":standard_problem_id,"score":300,"isPublished":True})
    challenge_task_id=find_guid(task,("id","taskId"))
    if not challenge_task_id: fail("Challenge task creation did not return id")
    api("POST",f"/api/challenges/{challenge_id}/join")
    passed(f"Temporary Challenge {challenge_id}; Task={challenge_task_id}")
    submit_expect("Challenge Partial 50/100",standard_problem_id,1,CPP_PARTIAL_50,"WrongAnswer",challenge_task_id,4,2); assert_challenge_score(150,False,0)
    submit_expect("Challenge Lower 30/100",standard_problem_id,1,CPP_LOWER_30,"WrongAnswer",challenge_task_id,4,2); assert_challenge_score(150,False,0)
    submit_expect("Challenge Full 100/100",standard_problem_id,1,CPP_ACCEPTED,"Accepted",challenge_task_id,4,4); assert_challenge_score(300,True,1)
    step("Create Temporary Custom Struct Function Problem")
    fp=api("POST","/api/problems",{"title":f"[PROD-SMOKE] Custom Struct Geometry {RUN_ID}","description":"Temporary production custom struct function smoke.","inputDescription":"","outputDescription":"","timeLimitMs":2000,"memoryLimitMb":128,"isPublished":True,"judgeMode":2,"functionSpecJson":FUNCTION_SPEC,"starterCodeJson":STARTER_CODE})
    function_problem_id=find_guid(fp,("id","problemId"))
    if not function_problem_id: fail("Function problem creation did not return id")
    api("POST",f"/api/problems/{function_problem_id}/test-cases",{"input":"","expectedOutput":"","argumentsJson":ARGS1,"expectedJson":"150","visibility":1,"score":50})
    api("POST",f"/api/problems/{function_problem_id}/test-cases",{"input":"","expectedOutput":"","argumentsJson":ARGS2,"expectedJson":"0","visibility":2,"score":50})
    passed(f"Temporary Function Problem {function_problem_id}; Cases=2")
    submit_expect("C++17 Function Struct",function_problem_id,1,CPP_FUNCTION,"Accepted",expected_case_count=2,expected_accepted_count=2)
    submit_expect("C11 Function Struct",function_problem_id,2,C_FUNCTION,"Accepted",expected_case_count=2,expected_accepted_count=2)
    submit_expect("C# Function Struct",function_problem_id,3,CSHARP_FUNCTION,"Accepted",expected_case_count=2,expected_accepted_count=2)
    log(); log("========================================"); log("PRODUCTION SMOKE RESULT : PASS"); log("========================================")
    log("Standard Judge   : C++17/C11/C# PASS"); log("Challenge Score  : 150 -> protect 150 -> 300 PASS"); log("Function Struct  : C++17/C11/C# PASS"); log(f"Submissions      : {len(results)}"); log("Cleanup          : runs in finally"); log("========================================")
    return 0

if __name__=="__main__":
    rc=1
    try:
        rc=main()
    except Exception as exc:
        log(); log(f"[FAIL] {exc}"); rc=1
    finally:
        cleanup()
    sys.exit(rc)
