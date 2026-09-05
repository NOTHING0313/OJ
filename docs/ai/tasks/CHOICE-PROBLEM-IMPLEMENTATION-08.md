# 选择题板块实施结果与自审

## 1. 交付状态

`IMPLEMENTED_AND_POSTGRESQL_GATES_PASSED`

选择题板块的领域模型、持久化迁移、作者工作流、发布快照、同步判分、答案揭示、赛季基础分、个人/管理列表和第一方前端均已实现。2026-09-05 已在隔离的 PostgreSQL 16 容器上完成空库升级、降级、再升级，以及服务级并发编辑和顺序唯一性验收；计划内门禁全部通过。

## 2. 已冻结并实现的契约

- 一个 `ChoiceSet` 含多个有序小题，每题支持单选或多选；集合完全相等才得该题分，满分状态为 Accepted。
- 题面、题干、选项和解析保存 Markdown，统一复用既有安全渲染；围栏代码块可正常展示。
- 草稿允许不完整；发布及已发布内容保存必须满足完整性和 UTF-8 资源边界。
- 已发布内容变化创建一个新的不可变 `ProblemJudgeRevision`；历史提交继续绑定旧修订。
- 答案策略为提交后揭示或 UTC 定时统一揭示；已披露答案不可重新隐藏。
- 选择题同步判分，不创建 Redis 信号、`JudgeJob`、Worker 或 Docker 沙箱任务。
- 选择题满分提交复用赛季基础分与首次完成时间，不进入语言、时间或内存性能奖励。
- Challenge 算法任务仍只允许编程题。

## 3. 架构与数据边界

- `ProblemKind` 与编程专属 `JudgeMode` 分离；编程字段在选择题上为空，并由数据库 check constraint 约束。
- 可变作者数据存入 `ProblemChoiceQuestion/Option`；发布真相存入不可变修订小题/选项表。
- 用户选择与逐题结果规范化存入 `SubmissionChoiceQuestionResult/Selection`，不使用可变 JSON 答案。
- `SubmissionKind` 区分代码和选择题；代码提交仍必须有语言/源码，选择题必须都为空。
- `AuthoringVersion` 从 1 开始；题目聚合、测试用例与判题资源成功变更会递增。

## 4. API 与前端结果

- 作者：`GET/PUT /api/problems/{id}/authoring`，8 MiB 上限，完整题组聚合保存。
- 作答：`POST /api/choice-submissions`，64 KiB 上限，要求当前发布修订 ID。
- 冲突：旧作者版本返回 `409 authoring_version_conflict`；旧题目修订返回 `409 problem_revision_conflict`；揭示回退返回 `409 answers_already_revealed`。
- 编辑器支持题组内小题/选项新增、修改、删除、上下排序、单/多选正确答案、分值、解析、发布状态和揭示策略。
- 公开页支持多小题作答、Markdown 代码块和提交结果；提交详情按策略省略或展示正确答案与解析。
- 题目列表、个人资料、提交列表和赛季页均区分选择题，不伪造语言或资源指标。

## 5. 资源边界

- 每题最多 50 小题，每小题最多 10 选项。
- 题干 16 KiB、选项 4 KiB、解析 16 KiB、整组文本 512 KiB，均按 UTF-8 字节计算。
- 单题分值 1-1,000，整组不超过 10,000。
- 发布时再次执行完整校验；提交限制小题数、选择数、重复 ID、跨修订 ID 和单选数量。

## 6. Verification Matrix

| 门禁 | 结果 | 证据 |
| --- | --- | --- |
| .NET 编译 | 通过 | `dotnet build OnlineJudge.sln --no-restore`：0 警告、0 错误 |
| 选择题与赛季定向测试 | 通过 | 62 项通过、0 失败 |
| 受影响旧模块测试 | 通过 | 72 项通过、0 失败（题目、测试点、资源、Profile、修订、Worker 配置等） |
| 前端生产构建 | 通过 | TypeScript、Vite 与初始 bundle budget 通过 |
| 前端 lint | 通过 | ESLint 0 warning |
| 迁移静态 SQL | 通过 | 草稿表、修订表、提交结果表、题型约束和顺序唯一索引均存在 |
| 全量回归基线 | 通过但早于最后收尾变更 | 1,032 通过、8 个 Redis 环境测试跳过；最终变更另以受影响测试覆盖 |
| PostgreSQL 升级/降级 | 通过 | PostgreSQL 16 空库完整升级；本迁移降级后 6 张选择题表为 0、旧版本恢复，随后重新升级成功 |
| PostgreSQL 列语义与约束 | 通过 | `Problems.JudgeMode` 升级后为 nullable 且无数据库默认值；降级后恢复 non-null/default 1；6 张选择题表和约束落库 |
| PostgreSQL 并发/排序 | 通过 | 同一 `ExpectedAuthoringVersion` 的两个独立 DbContext 并发保存仅一个成功、一个返回版本冲突；小题和选项逆序保存后均保持从 0 连续且唯一 |
| 选择题完整定向回归 | 通过 | `ChoiceProblemTests` 8/8，包括真实 PostgreSQL 集成门禁 |

## 7. Stage Result Ledger

| Stage | 状态 | 结果 |
| --- | --- | --- |
| A 契约冻结 | 完成 | 两份计划与 DecisionLog 已设为 Accepted/冻结 |
| B 领域与迁移 | 完成 | 模型、配置、迁移与不可变守卫完成；静态 SQL和真实 PostgreSQL 升降级均通过 |
| C 作者/答题 API | 完成 | 聚合 CRUD、修订、冲突、判分、揭示与列表投影完成 |
| D 赛季与既有功能 | 完成 | 基础分接入；性能候选/benchmark 排除；Challenge 保持编程题边界 |
| E 第一方前端 | 完成 | 编辑、作答、结果、列表、个人与赛季展示完成 |
| F PostgreSQL 验收 | 完成 | PostgreSQL 16 空库迁移闭环、列默认值/可空性、真实并发版本锁和重排唯一序均通过 |

## 8. Diff Intent 与剩余边界

- 本次选择题差异仅服务于冻结契约；没有重写 Worker、Redis 或沙箱执行架构。
- 工作区原先已有 Worker 并行度、资源测量和压力测试改动，本次保留且未将其归为选择题实现。
- `PROBLEM-AUTHORING-PARITY-07` 的完整编程题聚合编辑、测试点显式 `Order`、能力投影、资产原子替换和删除期望版本仍是后续阶段，不伪装成本次已完成。
- PostgreSQL 验收首次暴露 `Problems.JudgeMode` 的数据库默认值会把选择题 `NULL` 改写为 `1`；已从 EF 配置及当前迁移模型中移除该默认值，并补正 Down 迁移以恢复旧默认值。
- 集成门禁使用本轮专用 PostgreSQL 16 容器和匿名卷；验证完成后删除，不保留测试数据。

## 9. 自审结论

`REVIEW_PASSED_ALL_PLANNED_GATES`

架构复用、历史修订一致性、答案保密投影、同步事务、赛季边界、资源上限、第一方 UI 与 PostgreSQL 持久化行为均符合冻结契约。计划内没有剩余阻塞项；尚未执行的是远程生产部署，这不属于本地交付范围。
