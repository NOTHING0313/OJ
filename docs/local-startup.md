# 本地启动

统一入口为 `scripts/start-dev.ps1`，从任意目录调用均以脚本所在仓库为工作目录。沿用现有入口，兼容 OJLauncher 调用。

```powershell
# 只读检查命令、Docker 引擎、Compose 配置及端口占用
.\scripts\start-dev.ps1 -CheckOnly

# 日常启动：基础设施、数据库迁移、API、Worker、前端
.\scripts\start-dev.ps1

# 首次使用或明确需要重新恢复依赖时
.\scripts\start-dev.ps1 -RestoreDependencies

# 已有前端运行时，跳过前端
.\scripts\start-dev.ps1 -SkipFrontend
```

- 截图确认：API `http` profile 使用 5101；Vite 使用 5173；PostgreSQL 主机端口 5433，Redis 6379。当前 Worker 基础/开发配置的 Concurrency 均为 2，脚本不覆盖配置。
- `-RestoreDependencies` 才执行 `dotnet tool restore`、`dotnet restore` 和 `npm.cmd ci`。日常使用要求依赖及 EF 工具已安装；缺失时明确失败，不静默安装。
- 默认执行数据库升级；`-SkipMigrations` 可跳过。EF 沿用项目设计时工厂，包括 `ConnectionStrings__DefaultConnection` 环境变量覆盖；运行前确保它指向所需的本地数据库。
- 使用已有 Compose，等待 PostgreSQL/Redis 就绪；API 验证监听端口，Worker 验证消费者启动日志，前端验证 HTTP 200。它们不等同于真实题目判题验收，判题仍要求项目配置的 Docker 沙箱镜像已准备好。
- 服务在隐藏窗口运行，输出保存到 Git 忽略的 `logs/dev-时间戳/`。启动打印各服务监督进程 PID。
- 已占用的 API/前端端口或已运行的 Worker 会阻止重复启动。用 `-SkipApi`、`-SkipWorker`、`-SkipFrontend` 显式保留现有服务；脚本不擅自结束进程。跳过的服务也不会由脚本验证。
- 命令失败或就绪超时即停止后续步骤，保留已启动服务及数据；根据日志检查后再重试。`-TimeoutSeconds` 调整就绪等待时间（默认 90 秒）。
- 脚本不启动 Docker Desktop，不创建/重置数据库卷，不自动打开浏览器，不自动构建沙箱镜像。

验证范围：PowerShell 语法检查与 `-CheckOnly` 只读预检；未为验证脚本实际恢复依赖、迁移数据库或重启全套服务。
