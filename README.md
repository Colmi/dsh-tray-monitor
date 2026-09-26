# DSH 托盘监控 (DSH Tray Monitor)

> 本软件由 **DeepSeek 辅助编写**（Developed with the assistance of DeepSeek）


![DSH 托盘监控](assets/DSHTM.png)

DeepSeek Harness（DSH）Web 服务的**常驻任务栏通知区监控工具**：监控 DSH 运行状态，提供 启动 / 停止 / 重启、打开 Web UI、配置与一键自部署、监控/DSH 开机自启 等快捷操作。

## 环境要求

| 项目 | 要求 | 官方地址 |
| --- | --- | --- |
| 操作系统 | Windows 10 / 11（64 位） | — |
| .NET Framework | 4.x（Windows 自带，无需安装） | — |
| Node.js | **22.19+ 或 24+**（DSH 运行依赖） | https://nodejs.org/ |
| DeepSeek Harness | 已部署（需含 `apps\cli\src\bin.ts`） | https://github.com/deepseek-ai/deepseek-harness |

> 缺 Node 或 DSH 时，配置窗口会**红字提示并询问是否打开官方下载/仓库页**。DSH 自身的构建依赖（如 pnpm）按其仓库 README 准备，本工具只需 `node.exe` 路径。

## 使用方式

### 方式 A：单文件 exe 从零部署（推荐）

1. 下载 **`dsh-tray-monitor.exe`**（单文件，已内嵌图标资源）
2. 双击运行 → 任务栏出现托盘图标（红点=DSH 未运行）
3. 右键托盘 → **「配置…」**
4. 填写：Node 可执行文件、DSH 部署目录、DSH 数据目录（DSH_HOME）、日志目录、部署目录、端口；勾选需要的开机自启
5. 点 **「部署」** → 在部署目录生成完整一套：`dsh-tray-monitor.exe`、`ico\`、`config.json`、`启动DSH.ps1`、`停止DSH.ps1`、`启动托盘.cmd`
6. 部署到新目录时，按提示选择是否切换并重启监控

### 方式 B：zip 解压使用

1. 下载 zip 解压到任意目录
2. 双击 `启动托盘.cmd` 或 `dsh-tray-monitor.exe`
3. 右键 → 「配置…」 → 部署（同方式 A）

## 特性

- 🖥️ 常驻任务栏通知区，每 3 秒轮询 DSH 服务端口
- 🔵 状态圆点：托盘图标右下角 **蓝=运行中 / 红=已停止**
- 🔔 通知气泡使用中性程序图标；开启=蓝色 Info / 停止=黄色 Warning
- ⚙️ 「配置…」独立窗口 + 一键自部署
- 📋 右键菜单：启动 / 停止 / 重启 DSH、打开 Web UI、打开数据目录、打开日志、配置…、监控开机自启、DSH 开机自启、退出
- 🖱️ 双击托盘图标打开 Web UI；单实例互斥；日志记录
- 📁 状态图标放 `ico\` 子文件夹；**单 exe 已内嵌图标资源**，`ico\` 缺失时自动回退/释放

## 文件说明

| 文件 | 说明 |
| --- | --- |
| `dsh-tray-monitor.exe` | 主程序（单文件可独立运行；内置图标资源） |
| `dsh-tray-monitor.cs` | C# 源码 |
| `启动托盘.cmd` | 双击启动托盘（隐藏窗口） |
| `启动DSH.ps1` / `停止DSH.ps1` | 启停 DSH 脚本（可由「配置…」自动生成） |
| `config.example.json` | 配置示例 |
| `ico\` | 运行/停止状态图标 + 中性图标源（可替换） |
| `black-deepseek-logo.png` | 托盘底图（DeepSeek 官方 logo，版权归 DeepSeek 所有） |

## 重新编译

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ ^
  /win32icon:ico\dsh-logo.ico /out:dsh-tray-monitor.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ^
  /resource:ico\dsh-logo-running.ico,DshTray.Resources.dsh-logo-running.ico ^
  /resource:ico\dsh-logo-stopped.ico,DshTray.Resources.dsh-logo-stopped.ico ^
  /resource:ico\dsh-logo.ico,DshTray.Resources.dsh-logo.ico ^
  dsh-tray-monitor.cs
```

## 配置 (`config.json`)

由「配置…」窗口生成；也可把 `config.example.json` 复制为 `config.json` 手改：

```json
{
  "url": "http://127.0.0.1:3080",
  "port": 3080,
  "logFile": "D:\\Deepseek-harness-data\\logs\\tray.log",
  "dataDir": "D:\\Deepseek-harness-data",
  "startScript": "启动DSH.ps1",
  "stopScript": "停止DSH.ps1",
  "dshRepo": "D:\\Deepseek-harness",
  "dshHome": "D:\\Deepseek-harness-data",
  "nodePath": "C:\\Program Files\\nodejs\\node.exe",
  "deployDir": "D:\\Deepseek-harness-data\\dsh-tray-monitor"
}
```

## 致谢

本软件由 DeepSeek 辅助编写。感谢 DeepSeek 与 DeepSeek Harness 生态的支持。
## 免责声明

- 本项目与 DeepSeek 官方无关联，仅为 DSH 的辅助工具。
- DeepSeek logo 版权归 DeepSeek 所有，仅作为图标使用。
- 启停脚本按本机环境编写，请自行核对路径后再使用。