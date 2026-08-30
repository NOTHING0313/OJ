\set ON_ERROR_STOP on
BEGIN;

DO $$
BEGIN
  IF (SELECT count(*) FROM "Users") <> 1
     OR (SELECT count(*) FROM "Problems") <> 1
     OR (SELECT count(*) FROM "TestCases") <> 1
     OR (SELECT count(*) FROM "Submissions") <> 1
     OR (SELECT count(*) FROM "Teams") <> 0 THEN
    RAISE EXCEPTION 'stress seed requires a fresh isolation-probe database';
  END IF;
END $$;

INSERT INTO "Users" (
  "Id", "UserName", "Email", "PasswordHash", "CreatedAt", "IsBlacklisted", "Role", "UpdatedAt",
  "PhoneNumberConfirmed", "IsDeleted", "IsLeaderboardAnonymous")
SELECT gen_random_uuid(),
       'stress_user_' || lpad(g::text, 3, '0'),
       'stress_user_' || lpad(g::text, 3, '0') || '@example.invalid',
       root_user."PasswordHash",
       now() - make_interval(mins => g),
       false,
       1,
       now(),
       false,
       false,
       false
FROM generate_series(1, 99) AS g
CROSS JOIN LATERAL (SELECT "PasswordHash" FROM "Users" WHERE "Role" = 3 LIMIT 1) AS root_user;

CREATE TEMP TABLE stress_users AS
SELECT "Id", row_number() OVER (ORDER BY "UserName")::integer AS rn
FROM "Users";

INSERT INTO "Problems" (
  "Id", "Title", "Description", "InputDescription", "OutputDescription", "TimeLimitMs", "MemoryLimitMb",
  "IsPublished", "CreatedAt", "UpdatedAt", "CreatedByUserId", "IsDeleted", "JudgeMode", "AllowedLanguagesMask")
SELECT gen_random_uuid(),
       'STRESS Problem ' || lpad(g::text, 3, '0'),
       'Synthetic capacity data only.',
       'Two integers.',
       'Their sum.',
       1000,
       128,
       true,
       now() - make_interval(mins => g),
       now(),
       (SELECT "Id" FROM "Users" WHERE "Role" = 3 LIMIT 1),
       false,
       1,
       7
FROM generate_series(1, 49) AS g;

CREATE TEMP TABLE stress_problems AS
SELECT "Id", row_number() OVER (ORDER BY "Title")::integer AS rn
FROM "Problems";

INSERT INTO "TestCases" (
  "Id", "ProblemId", "Input", "ExpectedOutput", "Visibility", "Score", "CreatedAt", "IsDeleted", "UpdatedAt")
SELECT gen_random_uuid(),
       problem."Id",
       (g::text || ' ' || (g + 1)::text || E'\n'),
       ((2 * g + 1)::text || E'\n'),
       CASE WHEN g % 10 = 1 THEN 1 ELSE 2 END,
       10,
       now(),
       false,
       now()
FROM generate_series(1, 499) AS g
JOIN stress_problems AS problem ON problem.rn = ((g - 1) % 50) + 1;

INSERT INTO "Submissions" (
  "Id", "ProblemId", "UserId", "Language", "SourceCode", "Status", "TimeUsedMs", "MemoryUsedKb", "CreatedAt", "FinishedAt")
SELECT gen_random_uuid(),
       problem."Id",
       app_user."Id",
       ((g - 1) % 3) + 1,
       '/* synthetic historical submission */',
       CASE WHEN g % 5 = 0 THEN 4 ELSE 3 END,
       5 + (g % 100),
       2048 + (g % 8192),
       now() - make_interval(mins => g),
       now() - make_interval(mins => g) + interval '1 second'
FROM generate_series(1, 999) AS g
JOIN stress_problems AS problem ON problem.rn = ((g - 1) % 50) + 1
JOIN stress_users AS app_user ON app_user.rn = ((g - 1) % 100) + 1;

INSERT INTO "Teams" (
  "Id", "Name", "NormalizedName", "Description", "OwnerUserId", "IsDeleted", "CreatedAt", "UpdatedAt")
SELECT gen_random_uuid(),
       'STRESS Team ' || lpad(g::text, 2, '0'),
       upper('STRESS Team ' || lpad(g::text, 2, '0')),
       'Synthetic capacity team.',
       app_user."Id",
       false,
       now(),
       now()
FROM generate_series(1, 20) AS g
JOIN stress_users AS app_user ON app_user.rn = ((g - 1) * 5) + 1;

CREATE TEMP TABLE stress_teams AS
SELECT "Id", "OwnerUserId", row_number() OVER (ORDER BY "Name")::integer AS rn
FROM "Teams";

INSERT INTO "TeamMembers" ("Id", "TeamId", "UserId", "Role", "IsActive", "JoinedAt")
SELECT gen_random_uuid(),
       team."Id",
       app_user."Id",
       CASE WHEN app_user."Id" = team."OwnerUserId" THEN 2 ELSE 1 END,
       true,
       now()
FROM stress_users AS app_user
JOIN stress_teams AS team ON team.rn = ((app_user.rn - 1) / 5) + 1;

INSERT INTO "Challenges" (
  "Id", "Title", "Description", "StartAt", "EndAt", "CreatedByUserId", "IsPublished", "CreatedAt", "UpdatedAt",
  "ParticipationMode", "PeerReviewEnabled")
SELECT gen_random_uuid(),
       'STRESS Challenge ' || lpad(g::text, 2, '0'),
       'Synthetic capacity challenge.',
       now() - interval '1 day',
       now() + interval '30 days',
       (SELECT "Id" FROM "Users" WHERE "Role" = 3 LIMIT 1),
       true,
       now(),
       now(),
       1,
       false
FROM generate_series(1, 10) AS g;

INSERT INTO "LeaderboardSeasons" (
  "Id", "Name", "StartAt", "FreezeAt", "PublicUntil", "Status", "IsCurrent", "CreatedByUserId", "CreatedAt", "UpdatedAt", "ActivatedAt")
VALUES (
  gen_random_uuid(), 'STRESS Active Season', now() - interval '1 day', now() + interval '30 days', now() + interval '60 days',
  2, true, (SELECT "Id" FROM "Users" WHERE "Role" = 3 LIMIT 1), now(), now(), now() - interval '1 day');

INSERT INTO "LeaderboardSeasonBoards" ("Id", "SeasonId", "BoardType", "ChallengeId", "CreatedAt")
VALUES (
  gen_random_uuid(), (SELECT "Id" FROM "LeaderboardSeasons" LIMIT 1), 1, NULL, now()),
  (gen_random_uuid(), (SELECT "Id" FROM "LeaderboardSeasons" LIMIT 1), 2, (SELECT "Id" FROM "Challenges" ORDER BY "Title" LIMIT 1), now());

INSERT INTO "LeaderboardSeasonProblems" ("Id", "SeasonId", "ProblemId", "BaseScore", "CreatedAt")
SELECT gen_random_uuid(), (SELECT "Id" FROM "LeaderboardSeasons" LIMIT 1), problem."Id", 100, now()
FROM stress_problems AS problem
WHERE problem.rn <= 10;

INSERT INTO "LeaderboardUserProblemScores" (
  "Id", "SeasonId", "SeasonProblemId", "ProblemId", "UserId", "BestBaseScore", "IsFullScore", "FirstFullScoreAt",
  "BestRuntimeMs", "BestMemoryKb", "LastScoreImprovedAt", "CreatedAt", "UpdatedAt", "BestPerformanceFinishedAt", "BestPerformanceLanguage")
SELECT gen_random_uuid(),
       season_problem."SeasonId",
       season_problem."Id",
       season_problem."ProblemId",
       app_user."Id",
       CASE WHEN (app_user.rn + problem_order.rn) % 4 = 0 THEN 100 ELSE 50 END,
       (app_user.rn + problem_order.rn) % 4 = 0,
       CASE WHEN (app_user.rn + problem_order.rn) % 4 = 0 THEN now() - interval '1 hour' ELSE NULL END,
       10 + app_user.rn,
       2048 + problem_order.rn * 100,
       now(),
       now(),
       now(),
       now(),
       ((app_user.rn - 1) % 3) + 1
FROM stress_users AS app_user
CROSS JOIN LATERAL (
  SELECT season_problem.*, row_number() OVER (ORDER BY season_problem."Id")::integer AS rn
  FROM "LeaderboardSeasonProblems" AS season_problem
) AS problem_order
JOIN "LeaderboardSeasonProblems" AS season_problem ON season_problem."Id" = problem_order."Id";

COMMIT;

SELECT 'Users=' || count(*) FROM "Users"
UNION ALL SELECT 'Problems=' || count(*) FROM "Problems"
UNION ALL SELECT 'TestCases=' || count(*) FROM "TestCases"
UNION ALL SELECT 'Submissions=' || count(*) FROM "Submissions"
UNION ALL SELECT 'Teams=' || count(*) FROM "Teams"
UNION ALL SELECT 'TeamMembers=' || count(*) FROM "TeamMembers"
UNION ALL SELECT 'Challenges=' || count(*) FROM "Challenges"
UNION ALL SELECT 'LeaderboardUserProblemScores=' || count(*) FROM "LeaderboardUserProblemScores";
