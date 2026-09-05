# 挑战作答流程修复

## 范围与设计
- 风险 R1：前端局部行为修复，无后端接口、数据库、权限或计分规则修改。保留工作区此前变更。
- 结果页返回题目时，以提交响应的 challengeTaskId 为任务依据，并保留当前 challengeId；后续提交复用现有 taskId 到 challengeTaskId 的映射。
- 网络错误已由公共请求层转换为 ApiError(status=0)，提交轮询将其纳入既有有界退避重试，403 仍不重试。
- 棋盘可见时每 5 秒串行获取既有详情接口；focus、online、visibilitychange 触发及时刷新，隐藏时暂停。退出页面后停止计时并忽略迟到响应。后台刷新失败保留已有棋盘并显示错误和手动刷新入口。
- 复用 problemDraftKey/readDraft/writeDraft 保存最后进入的棋子，按账号和挑战隔离；普通返回与刷新恢复棋子选择、焦点和滚动定位。缓存不参与权限或计分，浏览器不允许存储或清除缓存时无法恢复位置。
- 完成动画后刷新也走同一页面刷新流程，避免另一条未受卸载保护的请求路径。

## 修改文件
- frontend/src/pages/SubmissionDetailPage.tsx
- frontend/src/pages/ChallengeDetailPage.tsx
- frontend/src/utils/submissionPolling.ts、submissionPolling.test.ts
- frontend/tests/challenge-flow.html、challenge-flow.tsx

## 验证
- 定向 Vitest：submissionPolling.test.ts，5 项通过；断网用例改用真实公共请求层的 ApiError(status=0)，覆盖恢复与结束，保留拒绝访问、限流、重试上限及卸载保护断言。
- 应用 npm run typecheck、修改文件 ESLint、独立验收入口 TypeScript 检查通过。
- 浏览器验收入口：Vite 开发服务的 /tests/challenge-flow.html。使用真实 ChallengeDetailPage、SubmissionDetailPage，MemoryRouter 与模拟 fetch/本地存储隔离。返回目的页为最小路由占位，不执行实际代码提交或判题。
- 首轮 5/5 通过：返回链接的挑战/任务标识；棋子恢复与账号隔离；5 秒更新状态和得分；断网保留内容/联网与焦点恢复；防重叠请求/卸载后迟到响应与计时器停止。
- 补充后台可见性与成功返回回归通过，最终浏览器 6/6 PASS。差异空白检查通过。
- 首轮验收数据缺少 evaluation 必填指标，导致结果组件无法渲染；补齐真实 DTO 字段并等待页面加载后通过，未削弱生产断言。
- 未运行全项目测试、真实多人压测或 Docker 判题；本次不改后端。棋盘刷新新增每个可见页面约每 5 秒一次详情请求，尚未做生产并发负载测量。
