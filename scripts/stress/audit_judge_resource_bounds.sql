\set ON_ERROR_STOP on

-- Read-only pre-rollout inventory for the default JudgeResourcePolicy values.
WITH limits AS (
    SELECT 524288::bigint AS source_bytes,
           200::bigint AS title_characters,
           262144::bigint AS content_bytes,
           100::bigint AS min_time_ms,
           10000::bigint AS max_time_ms,
           16::bigint AS min_memory_mb,
           512::bigint AS max_memory_mb,
           200::bigint AS test_cases,
           1048576::bigint AS test_field_bytes,
           67108864::bigint AS aggregate_test_bytes,
           120000::bigint AS declared_time_budget_ms
), active_case_stats AS (
    SELECT p."Id",
           COUNT(t."Id") AS case_count,
           COALESCE(SUM(
               octet_length(COALESCE(t."Input", ''))
               + octet_length(COALESCE(t."ExpectedOutput", ''))
               + octet_length(COALESCE(t."ArgumentsJson", ''))
               + octet_length(COALESCE(t."ExpectedJson", ''))), 0) AS aggregate_bytes,
           COALESCE(MAX(GREATEST(
               octet_length(COALESCE(t."Input", '')),
               octet_length(COALESCE(t."ExpectedOutput", '')),
               octet_length(COALESCE(t."ArgumentsJson", '')),
               octet_length(COALESCE(t."ExpectedJson", '')))), 0) AS max_field_bytes
    FROM "Problems" p
    LEFT JOIN "TestCases" t ON t."ProblemId" = p."Id" AND NOT t."IsDeleted"
    WHERE NOT p."IsDeleted"
    GROUP BY p."Id"
)
SELECT COUNT(*) AS problem_count,
       MAX(char_length(p."Title")) AS max_title_characters,
       MAX(GREATEST(
           octet_length(COALESCE(p."Description", '')),
           octet_length(COALESCE(p."InputDescription", '')),
           octet_length(COALESCE(p."OutputDescription", '')),
           octet_length(COALESCE(p."FunctionSpecJson", '')),
           octet_length(COALESCE(p."StarterCodeJson", '')))) AS max_content_field_bytes,
       MIN(p."TimeLimitMs") AS min_time_limit_ms,
       MAX(p."TimeLimitMs") AS max_time_limit_ms,
       MIN(p."MemoryLimitMb") AS min_memory_limit_mb,
       MAX(p."MemoryLimitMb") AS max_memory_limit_mb,
       MAX(s.case_count) AS max_active_case_count,
       MAX(s.max_field_bytes) AS max_test_field_bytes,
       MAX(s.aggregate_bytes) AS max_aggregate_test_bytes,
       COUNT(*) FILTER (WHERE
           char_length(p."Title") > l.title_characters
           OR GREATEST(
               octet_length(COALESCE(p."Description", '')),
               octet_length(COALESCE(p."InputDescription", '')),
               octet_length(COALESCE(p."OutputDescription", '')),
               octet_length(COALESCE(p."FunctionSpecJson", '')),
               octet_length(COALESCE(p."StarterCodeJson", ''))) > l.content_bytes
           OR p."TimeLimitMs" NOT BETWEEN l.min_time_ms AND l.max_time_ms
           OR p."MemoryLimitMb" NOT BETWEEN l.min_memory_mb AND l.max_memory_mb
           OR s.case_count > l.test_cases
           OR s.max_field_bytes > l.test_field_bytes
           OR s.aggregate_bytes > l.aggregate_test_bytes
           OR p."TimeLimitMs"::bigint * s.case_count > l.declared_time_budget_ms) AS violating_problem_count
FROM "Problems" p
JOIN active_case_stats s ON s."Id" = p."Id"
CROSS JOIN limits l
WHERE NOT p."IsDeleted"
GROUP BY l.title_characters, l.content_bytes, l.min_time_ms, l.max_time_ms,
         l.min_memory_mb, l.max_memory_mb, l.test_cases, l.test_field_bytes,
         l.aggregate_test_bytes, l.declared_time_budget_ms;

WITH limits AS (SELECT 524288::bigint AS source_bytes)
SELECT COUNT(*) AS submission_count,
       COALESCE(MAX(octet_length("SourceCode")), 0) AS max_source_bytes,
       COUNT(*) FILTER (WHERE octet_length("SourceCode") > limits.source_bytes) AS violating_submission_count
FROM "Submissions"
CROSS JOIN limits
GROUP BY limits.source_bytes;

WITH revision_stats AS (
    SELECT r."Id",
           r."TimeLimitMs",
           r."MemoryLimitMb",
           COUNT(t."Id") AS case_count,
           COALESCE(SUM(
               octet_length(COALESCE(t."Input", ''))
               + octet_length(COALESCE(t."ExpectedOutput", ''))
               + octet_length(COALESCE(t."ArgumentsJson", ''))
               + octet_length(COALESCE(t."ExpectedJson", ''))), 0) AS aggregate_bytes,
           COALESCE(MAX(GREATEST(
               octet_length(COALESCE(t."Input", '')),
               octet_length(COALESCE(t."ExpectedOutput", '')),
               octet_length(COALESCE(t."ArgumentsJson", '')),
               octet_length(COALESCE(t."ExpectedJson", '')))), 0) AS max_field_bytes
    FROM "ProblemJudgeRevisions" r
    LEFT JOIN "ProblemJudgeRevisionTestCases" t ON t."ProblemJudgeRevisionId" = r."Id"
    GROUP BY r."Id", r."TimeLimitMs", r."MemoryLimitMb"
)
SELECT COUNT(*) AS revision_count,
       MAX(case_count) AS max_revision_case_count,
       MAX(max_field_bytes) AS max_revision_field_bytes,
       MAX(aggregate_bytes) AS max_revision_test_bytes,
       COUNT(*) FILTER (WHERE
           "TimeLimitMs" NOT BETWEEN 100 AND 10000
           OR "MemoryLimitMb" NOT BETWEEN 16 AND 512
           OR case_count = 0
           OR case_count > 200
           OR max_field_bytes > 1048576
           OR aggregate_bytes > 67108864
           OR "TimeLimitMs"::bigint * case_count > 120000) AS violating_revision_count
FROM revision_stats;
