# 截图清单

用于最终演示、答辩和实验报告配图。建议统一使用本地演示数据，页面缩放保持一致。

| 序号 | 页面路径 | 登录角色 | 截图目的 | 必须 |
| --- | --- | --- | --- | --- |
| 1 | `/login` | 未登录 | 展示登录入口与整体视觉风格 | 是 |
| 2 | `/register` | 未登录 | 展示注册邮箱验证码流程 | 是 |
| 3 | `/profile/me` | demo_answerer | 展示个人中心、提交统计、Challenge 概览 | 是 |
| 4 | `/account/settings` | demo_answerer | 展示账号设置、头像上传、手机号绑定、注销危险区 | 是 |
| 5 | `/problems` | demo_answerer | 展示题目列表和题型入口 | 是 |
| 6 | `/problems/{problemId}` | demo_answerer | 展示题目详情与 Monaco 编辑器 | 是 |
| 7 | `[Demo] Two Sum 函数式题` 详情 | demo_answerer | 展示基础数组 Function Mode | 是 |
| 8 | `[Demo] Reverse List 链表题` 详情 | demo_answerer | 展示 `ListNode<int>` Function Mode | 是 |
| 9 | `[Demo] Invert Tree 二叉树题` 详情 | demo_answerer | 展示 `TreeNode<int>` Function Mode | 是 |
| 10 | 测试点管理页 | Root 或 demo_setter | 展示批量导入测试点 | 是 |
| 11 | 测试点管理页 | Root 或 demo_setter | 展示导出 JSON 功能 | 是 |
| 12 | `/submissions/{id}` | demo_answerer | 展示 Hidden 测试点脱敏效果 | 是 |
| 13 | `/submissions/my` | demo_answerer | 展示我的提交列表 | 是 |
| 14 | `/submissions/{id}` | demo_answerer | 展示提交详情、源码、caseResults | 是 |
| 15 | `/admin/submissions` | Root | 展示 Root 全站提交管理 | 是 |
| 16 | Challenge 棋盘页 | demo_answerer | 展示棋盘挑战和任务状态 | 是 |
| 17 | Challenge 管理统计页 | Root 或 demo_setter | 展示 Challenge 管理统计 | 是 |
| 18 | 文件题评分页 | Root 或 demo_setter | 展示 ZIP 文件提交与人工评分 | 建议 |
| 19 | 榜单页 | demo_answerer | 展示排行榜 | 是 |
| 20 | PowerShell 终端 | 本机 | 展示 Function Mode E2E `Passed: 10 / Failed: 0` | 是 |

## 截图建议

- 先运行 `scripts/demo/seed-demo-data.ps1`，确保演示题、Challenge 和提交记录存在。
- Hidden 测试点脱敏截图建议使用 demo_answerer 的 WrongAnswer 提交。
- Root 管理页截图前先确认当前登录账号是 `UnrealStudio`。
- 若真实 SMTP 已启用，注册验证码截图可展示“验证码已发送”状态，避免暴露真实验证码。
