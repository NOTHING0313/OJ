# OnlineJudge

OnlineJudge 是一个面向课程设计和演示场景的在线判题平台，支持标准输入输出判题、LeetCode 风格 Function Mode、Challenge 棋盘挑战、提交记录中心、个人中心与账号安全功能。

## 技术栈

- 后端：ASP.NET Core Web API、EF Core、PostgreSQL、Redis、Worker Service
- 前端：React、TypeScript、Vite、Monaco Editor
- 判题：Docker Sandbox、异步 JudgeWorker
- 支持语言：C++17、C11、C#

## 核心功能

- 账号体系：注册邮箱验证码、登录、邮箱找回密码、头像上传、账号设置、账号注销
- 题目管理：标准输入输出题、Function Mode 题、测试点批量导入/导出、Sample/Hidden 测试点
- 判题体系：异步提交、Docker 沙箱运行、提交详情、Hidden 测试点后端脱敏
- Function Mode：基础类型、数组、`ListNode<int>`、`TreeNode<int>`
- Challenge：棋盘挑战、算法任务、文件 ZIP 提交、人工评分、管理统计、CSV 导出
- 用户体验：Monaco 代码编辑器、提交记录中心、个人中心、排行榜

## 本地环境要求

- .NET SDK 8+
- Node.js 和 npm
- Docker Desktop
- PostgreSQL / Redis 通过 `docker compose` 启动

## 本地启动

```powershell
docker compose up -d

dotnet ef database update --project .\OnlineJudge.Infrastructure --startup-project .\OnlineJudge.Api

dotnet run --project .\OnlineJudge.Api

dotnet run --project .\OnlineJudge.JudgeWorker

cd frontend
npm.cmd run dev
```

默认访问：

- API: `http://localhost:5101`
- Frontend: `http://localhost:5173`

## 一键启动

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-dev.ps1
```

该脚本会启动 Docker Compose，并分别打开 API、JudgeWorker、前端开发服务器窗口。

## 数据库迁移

```powershell
dotnet ef database update --project .\OnlineJudge.Infrastructure --startup-project .\OnlineJudge.Api
```

本收口阶段不新增数据库字段，也不生成新的 Migration。

## 邮箱 SMTP 配置

系统支持开发环境 `DevEmailSender` 和 SMTP 发信。QQ 邮箱需要开启 SMTP 服务，并使用 SMTP 授权码，不是 QQ 登录密码。不要把授权码写入代码或提交到 Git。

推荐使用 user-secrets：

```powershell
cd OnlineJudge.Api

dotnet user-secrets set "Email:Provider" "Smtp"
dotnet user-secrets set "Email:Smtp:Host" "smtp.qq.com"
dotnet user-secrets set "Email:Smtp:Port" "587"
dotnet user-secrets set "Email:Smtp:EnableSsl" "true"
dotnet user-secrets set "Email:Smtp:UserName" "你的QQ邮箱@qq.com"
dotnet user-secrets set "Email:Smtp:Password" "你的SMTP授权码"
dotnet user-secrets set "Email:Smtp:FromName" "Online Judge"
```

也可以使用环境变量：

```powershell
$env:Email__Provider="Smtp"
$env:Email__Smtp__Host="smtp.qq.com"
$env:Email__Smtp__Port="587"
$env:Email__Smtp__EnableSsl="true"
$env:Email__Smtp__UserName="你的QQ邮箱@qq.com"
$env:Email__Smtp__Password="你的SMTP授权码"
$env:Email__Smtp__FromName="Online Judge"
```

## 演示数据生成

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\demo\seed-demo-data.ps1
```

可选参数：

- `-ApiBaseUrl`，默认 `http://localhost:5101`
- `-RootAccount`，默认 `UnrealStudio`
- `-RootPassword`，默认 `UnrealStudio`
- `-DemoPassword`，默认 `123456`
- `-SkipUsers`
- `-SkipSubmissions`
- `-SkipFileUploadDemo`
- `-InteractiveEmailCode`

脚本通过 HTTP API 创建或复用演示用户、演示题目、测试点、Challenge 和若干演示提交；不会清空数据库，也不会删除既有数据。

## 总体验收

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\e2e\run-all-checks.ps1
```

默认执行：

1. 后端 Release 构建
2. 后端测试
3. 前端依赖安装与构建
4. Function Mode E2E
5. 演示脚本语法 smoke check

可选跳过：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\e2e\run-all-checks.ps1 -SkipFrontend -SkipE2E
```

## 默认开发账号

- Root: `UnrealStudio` / `UnrealStudio`

该账号仅用于本地开发和演示环境，不应作为生产账号使用。

## 功能边界

- C11 Function Mode 支持基础类型和一维数组，但暂不支持 `ListNode<int>` / `TreeNode<int>`。
- 手机号当前作为账号资料和绑定信息保留。
- 找回密码主流程使用邮箱验证码。
- 真实短信服务未接入。
- Hidden 测试点内容由后端过滤和脱敏，前端隐藏不作为安全边界。
- Monaco Editor 会增加前端构建体积，Vite chunk warning 可接受。

## 常见问题

### E2E 失败并提示 API 不可访问

先启动 Docker、API 和 JudgeWorker：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-dev.ps1
```

### 注册演示用户时没有 debugCode

如果当前环境使用真实 SMTP 或 Production 配置，注册验证码不会返回 `debugCode`。可以加上 `-InteractiveEmailCode`，手动输入邮箱收到的验证码：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\demo\seed-demo-data.ps1 -InteractiveEmailCode
```

### QQ 邮箱发信失败

检查 SMTP 服务是否开启，确认使用的是授权码而非登录密码，并确认本地配置没有被提交到仓库。
