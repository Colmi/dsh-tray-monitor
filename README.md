# DSH 托盘监控 (DSH Tray Monitor)

> 本软件由 **DeepSeek 辅助编写**（Developed with the assistance of DeepSeek）


![DSH 托盘监控](assets/DSHTM.png)

DeepSeek Harness（DSH）Web 服务的**常驻任务栏通知区监控工具**：监控 DSH 运行状态，提供 启动 / 停止 / 重启、打开 Web UI、开机自启 等快捷操作。

## 特性

- 🖥️ 常驻 Windows 任务栏通知区（NotifyIcon），每 3 秒轮询 DSH 服务端口
- 🔵 状态一目了然：托盘图标右下角圆点 **蓝=运行中 / 红=已停止**；Windows 通知也显示 DSH logo（exe 内置图标）
- 📋 右键菜单：启动 / 停止 / 重启 DSH、打开 Web UI、打开数据目录、打开日志、**开机自启**、退出
- 🖱️ 双击托盘图标直接打开 Web UI
- 🛡️ 单实例互斥；操作与状态写入日志
- ⚙️ 可选 `config.json` 配置（端口、URL、日志、数据目录、启停脚本），开箱即用有合理默认值

## 文件说明

| 文件 | 说明 |
| --- | --- |
| `dsh-tray-monitor.exe` | 编译好的主程序（.NET Framework 4.x，Windows 10/11） |
| `dsh-tray-monitor.cs` | C# 源码（可用 csc 重新编译） |
| `启动托盘.cmd` | 双击启动托盘（隐藏窗口） |
| `启动DSH.ps1` / `停止DSH.ps1` | 启停 DSH 的示例脚本（按你的环境调整路径） |
| `black-deepseek-logo.png` | 托盘底图（DeepSeek 官方 logo，版权归 DeepSeek 所有） |
| `dsh-logo-running.ico` / `dsh-logo-stopped.ico` | 运行 / 停止状态图标 |
| `config.example.json` | 配置示例 |

## 使用方法

1. 把整个目录放到任意位置（如 `D:\tools\dsh-tray-monitor`）
2. 按需修改 `启动DSH.ps1` / `停止DSH.ps1`（或 `config.json`）指向你的 DSH 安装
3. 双击 `启动托盘.cmd` 启动；或右键托盘图标开启「开机自启」

## 重新编译

需要 .NET Framework 4.x 自带 csc：

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ ^
  /win32icon:dsh-logo-running.ico /out:dsh-tray-monitor.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ^
  dsh-tray-monitor.cs
```

## 配置 (`config.json`)

所有字段可选，缺省用内置默认值；将 `config.example.json` 复制为 `config.json` 后修改：

```json
{
  "url": "http://127.0.0.1:3080",
  "port": 3080,
  "logFile": "D:\\Deepseek-harness-data\\logs\\tray.log",
  "dataDir": "D:\\Deepseek-harness-data",
  "startScript": "启动DSH.ps1",
  "stopScript": "停止DSH.ps1"
}
```

- `startScript` / `stopScript` 可为相对路径（相对 exe 目录）或绝对路径
- 修改 `config.json` 后重启托盘生效


## 致谢

本软件由 DeepSeek 辅助编写。感谢 DeepSeek 与 DeepSeek Harness 生态的支持。
## 免责声明

- 本项目与 DeepSeek 官方无关联，仅为 DSH 的辅助工具。
- DeepSeek logo 版权归 DeepSeek 所有，仅作为图标使用。
- 启停脚本按本机环境编写，请自行核对路径后再使用。