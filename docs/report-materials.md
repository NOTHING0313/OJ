# 实验报告素材清单

本文档用于整理最终实验报告或答辩材料的章节素材。

## 1. 项目背景

- 在线判题系统面向算法练习、课程实验和挑战式任务。
- 系统目标是提供题目管理、代码提交、异步判题、提交记录、挑战任务和用户中心的一体化体验。

## 2. 系统架构

- 前端 React + Vite 负责交互页面。
- ASP.NET Core Web API 提供业务接口。
- EF Core 管理 PostgreSQL 持久化。
- Redis 用于判题队列和验证码缓存。
- JudgeWorker 异步消费判题任务。
- Docker Sandbox 隔离运行用户代码。

## 3. 技术栈

- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- Redis
- React / TypeScript / Vite
- Monaco Editor
- Docker

## 4. 模块划分

- Auth / Account：注册、登录、邮箱验证码、头像、注销。
- Problems / TestCases：题目、测试点、导入导出、可见性。
- Submissions / JudgeWorker：提交、队列、判题、结果记录。
- Function Mode：函数式判题代码生成。
- Challenge：棋盘挑战、任务完成、文件上传评分。
- Profile / Leaderboard：个人中心和排行榜。

## 5. 数据库设计摘要

- User：账号、角色、头像、手机号、注销状态。
- Problem：题面、判题模式、FunctionSpec。
- TestCase：输入输出、Function JSON、Sample/Hidden。
- Submission / SubmissionCaseResult：提交及逐测试点结果。
- Challenge / ChallengeTask：棋盘挑战和任务。
- ChallengeTaskCompletion / ChallengeTaskFileSubmission：完成记录与文件题评分。

## 6. 判题流程

1. 用户提交代码。
2. API 创建 Submission 并入队。
3. JudgeWorker 消费任务。
4. Runner 根据语言和模式生成运行请求。
5. Docker Sandbox 编译运行。
6. 写入 Submission 和 CaseResult。
7. 前端轮询或刷新查看结果。

## 7. StandardInputOutput 判题

- 用户提交完整程序。
- 测试点使用标准输入和期望输出。
- C++17 / C11 / C# 复用 Docker 沙箱运行。

## 8. Function Mode 设计

- 用户只写目标函数，不写 `main` / `Main`。
- 后端根据 FunctionSpec 自动生成测试驱动。
- 支持 AC / WA / CE / main 误写友好提示。
- C# 方法名按 PascalCase 生成和调用。

## 9. 复杂结构支持

- `ListNode<int>`：测试数据使用数组表示，系统构造链表并序列化返回链表。
- `TreeNode<int>`：测试数据使用层序数组表示，支持中间 `null`，输出裁剪尾部 `null`。
- C11 暂不支持链表和二叉树，后端在进入 Docker 前友好拒绝。

## 10. Monaco 编辑器体验优化

- 代码提交区使用 Monaco Editor。
- 支持 C++、C、C# 高亮。
- 使用 `vs-dark` 主题、自动布局、自动换行。

## 11. 测试点批量导入 / 导出

- 支持 StandardInputOutput 和 Function Mode 两种 JSON 格式。
- 导入时后端校验题型与 FunctionSpec。
- 导出 JSON 不包含内部字段，可再次导入。

## 12. Sample / Hidden 脱敏

- 题目详情只展示 Sample 测试点。
- Hidden 测试点内容在后端过滤。
- 提交详情对普通用户隐藏 Hidden 的 actual / expected 输出。
- Root 可查看完整详情。

## 13. Challenge 棋盘挑战设计

- Challenge 由棋盘任务组成。
- 任务类型包括算法题和文件上传题。
- 支持完成状态、积分、管理统计和 CSV 导出。

## 14. 文件题 ZIP 提交与评分

- 用户上传 ZIP 文件。
- 管理者下载文件并人工评分。
- 评分结果进入 Challenge 统计与个人中心。

## 15. 榜单系统

- 基于完成记录和分数聚合排行榜。
- 支持 Challenge 维度展示。

## 16. 提交记录中心

- `/submissions/my` 查看个人提交。
- `/submissions/{id}` 查看提交详情。
- `/admin/submissions` Root 查看全站提交。
- 列表接口不返回源码，详情接口按权限返回源码。

## 17. 个人中心

- 展示用户资料、提交统计、AC 题目、语言统计、Challenge 概览、最近提交与文件评分摘要。
- Root 可查看指定用户主页。

## 18. 账号安全设计

- 注册邮箱验证码。
- 邮箱找回密码。
- 头像上传保存。
- 顶部导航头像展示。
- 账号注销采用软删除和匿名化，保留历史提交。

## 19. 权限控制

- Root 管理全站。
- ProblemSetter 管理题目和 Challenge。
- Answerer 进行答题和挑战。
- Hidden 测试点、源码、用户信息均由后端做权限控制。

## 20. 自动化测试与 E2E

- 后端单元测试覆盖 Function code builder、Profile、账号安全、测试点导入导出等核心逻辑。
- Function Mode E2E 覆盖 Two Sum、Reverse List、Invert Tree 和标准 A+B。
- `run-all-checks.ps1` 统一执行构建、测试、前端构建和 E2E。

## 21. AI 使用情况说明

- 使用 AI 辅助需求拆分、接口契约设计、代码草稿生成和测试用例设计。
- 关键权限、数据脱敏、EF 查询、构建验证由开发者审查并集成。
- 不将 AI 输出直接作为最终实现，所有改动需通过编译、测试和 E2E 验证。

## 22. 遇到的问题与解决方案

- Function Mode 多语言签名差异：通过语言专属 builder 处理。
- C# 函数名风格：采用 PascalCase 转换，但不改变 FunctionSpec 存储值。
- Hidden 测试点泄露风险：后端过滤题目详情并脱敏提交详情。
- 验证码安全：Redis TTL、冷却、每日限制、错误次数限制和 hash 存储。
- Monaco 构建体积：接受 Vite chunk warning，换取更好的代码编辑体验。

## 23. 后续改进方向

- 接入真实短信服务商。
- 支持更多 Function Mode 类型，例如字典和泛型结构。
- 增加更细粒度的题目协作者权限。
- 增加提交实时推送。
- 增加公开用户主页和更多可视化统计。
