# Windows 一键启动 EXE

本项目提供一个轻量 C# 启动器 `OJLauncher.exe`，用于双击启动本地开发环境。启动器不会重写启动逻辑，只会定位项目根目录并调用现有的 `scripts/start-dev.ps1`。

## 生成 EXE

在项目根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-launcher.ps1
```

默认发布到：

```text
artifacts/OJLauncher/OJLauncher.exe
```

默认发布参数：

```powershell
dotnet publish .\tools\OJLauncher\OJLauncher.csproj `
  -c Release `
  -r win-x64 `
  -p:PublishSingleFile=true `
  -p:SelfContained=false `
  -o .\artifacts\OJLauncher
```

`SelfContained=false` 表示目标机器需要安装 .NET Runtime。若需要在没有 .NET Runtime 的机器运行，可以执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-launcher.ps1 -SelfContained
```

这会显著增大发布体积。

## 运行方式

双击：

```text
artifacts/OJLauncher/OJLauncher.exe
```

启动器会：

1. 查找 `scripts/start-dev.ps1`。
2. 使用 `powershell.exe -NoProfile -ExecutionPolicy Bypass` 运行脚本。
3. 等待脚本返回。
4. 默认打开 `http://localhost:5173`。

可选参数：

```powershell
.\artifacts\OJLauncher\OJLauncher.exe --no-browser
.\artifacts\OJLauncher\OJLauncher.exe --lan
```

当前 `scripts/start-dev.ps1` 尚未支持 `-Lan` 参数，因此 `--lan` 会提示并按普通模式启动。

## 桌面快捷方式建议

推荐创建桌面快捷方式，目标指向项目内的：

```text
E:\Github\OJ\artifacts\OJLauncher\OJLauncher.exe
```

不建议只复制 EXE 到桌面。第一版启动器没有配置文件，复制到桌面后可能无法向上找到 `scripts/start-dev.ps1`。
