# 前端信息精简验收（2026-09-05）

## 本次范围

精简 45 个前端页面与组件中的展示文案。删除装饰性英文标签、重复标题说明、宣传性空状态介绍，以及提交详情标题中的长编号。赛季状态改用中文；导入说明保留必要规则并缩短表述。

保留表单标签、验证错误、权限提示、评分与发布规则、危险操作提醒、用户内容和有效统计。未改变 API、持久化、权限及业务流程。此前赛季权限等未提交改动继续保留，不属于本次精简。

## 验证

- 前端生产构建通过（Vite 的 Monaco 大块提示仍存在）。
- 前端测试：6 个文件、20 项通过。
- 前端源码契约相关测试：137 项通过。首次有 2 项因绑定已删除文案失败，已调整这两项断言；按钮、导航和校验断言保留。
- 最终 TypeScript 检查、45 个文件 ESLint 检查通过。
- 差异空白检查通过。
- 浏览器桌面检查：题目列表、帮助、个人中心、账号设置、赛季管理、主题编辑器。个人中心与账号设置初次截图后又删除少量装饰文案；个人中心已在手机端复核。
- 手机 390px 检查：个人中心、题目创建页、选择题提交详情；无页面横向溢出。

## 验证边界

本地没有完整的赛季、挑战和战队业务数据，因此未逐一验收所有有数据状态、弹窗与角色组合。账号设置最后两处文案调整通过静态检查，未重新截图。此轮不创建业务数据、不提交表单、不更改站点主题。

## 修改的前端文件

- `frontend/src/components/ChallengeCompletionMatrix.tsx`
- `frontend/src/components/RankHistoryChart.tsx`
- `frontend/src/components/help/HelpCenterView.tsx`
- `frontend/src/components/leaderboards/LeaderboardHomeView.tsx`
- `frontend/src/components/problems/ChoiceProblemDetail.tsx`
- `frontend/src/components/problems/ProblemDetailView.tsx`
- `frontend/src/components/theme-editor/ThemeEditorPreview.tsx`
- `frontend/src/components/theme-editor/ThemeEditorWorkbench.tsx`
- `frontend/src/pages/AccountCompetitionPage.tsx`
- `frontend/src/pages/AccountSettingsPage.tsx`
- `frontend/src/pages/AdminChallengeEditorPage.tsx`
- `frontend/src/pages/AdminChallengeListPage.tsx`
- `frontend/src/pages/AdminChallengeTaskEditorPage.tsx`
- `frontend/src/pages/AdminLeaderboardSeasonPage.tsx`
- `frontend/src/pages/AdminProblemEditorPage.tsx`
- `frontend/src/pages/AdminProblemListPage.tsx`
- `frontend/src/pages/AdminSecurityAuditPage.tsx`
- `frontend/src/pages/AdminSubmissionsPage.tsx`
- `frontend/src/pages/AdminTeamListPage.tsx`
- `frontend/src/pages/AdminTestCaseEditorPage.tsx`
- `frontend/src/pages/AdminUserListPage.tsx`
- `frontend/src/pages/ChallengeAdminSummaryPage.tsx`
- `frontend/src/pages/ChallengeAdminTaskDetailPage.tsx`
- `frontend/src/pages/ChallengeDetailPage.tsx`
- `frontend/src/pages/ChallengeLeaderboardIndexPage.tsx`
- `frontend/src/pages/ChallengeLeaderboardPage.tsx`
- `frontend/src/pages/ChallengeListPage.tsx`
- `frontend/src/pages/ChallengePeerReviewAuditPage.tsx`
- `frontend/src/pages/ChallengePeerReviewPage.tsx`
- `frontend/src/pages/ChallengeTaskAnswerPage.tsx`
- `frontend/src/pages/ChallengeTaskDetailPage.tsx`
- `frontend/src/pages/ForgotPasswordPage.tsx`
- `frontend/src/pages/GlobalUserLeaderboardPage.tsx`
- `frontend/src/pages/HelpDocumentEditorPage.tsx`
- `frontend/src/pages/HelpDocumentManagePage.tsx`
- `frontend/src/pages/LeaderboardSeasonHistoryDetailPage.tsx`
- `frontend/src/pages/LeaderboardSeasonHistoryPage.tsx`
- `frontend/src/pages/MyProfilePage.tsx`
- `frontend/src/pages/MySubmissionsPage.tsx`
- `frontend/src/pages/ProblemListPage.tsx`
- `frontend/src/pages/SeasonLeaderboardPage.tsx`
- `frontend/src/pages/SeasonProblemLeaderboardPage.tsx`
- `frontend/src/pages/SubmissionDetailPage.tsx`
- `frontend/src/pages/TeamPage.tsx`
- `frontend/src/pages/TeamProjectHistoryPage.tsx`
