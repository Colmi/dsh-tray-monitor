# Release Notes

## v1.1.0 (2026-08-27)

### 修复与改进
- 🆕 新增「DSH 开机自启」：登录时自动启动 DSH Web 服务（注册表 Run 键 `DSHWebService`），与「监控开机自启」相互独立
- 🐛 修复开机自启后状态图标默认显示蓝色（运行）的问题：启动时按 DSH 真实状态显示图标
- 🎨 程序（exe）改用**中性图标** `dsh-logo.ico`（无状态圆点），不再用运行状态图标
- 🔔 通知气泡改用中性程序图标，开启/停止分别以蓝色 Info / 黄色 Warning 色调区分

### 功能
- 常驻任务栏通知区，每 3 秒探测 DSH 服务端口
- 状态圆点：🟦 蓝=运行中 / 🟥 红=停止
- 右键菜单：启动 / 停止 / 重启 DSH、打开 Web UI、打开数据目录、打开日志、监控开机自启、DSH 开机自启、退出
- 双击托盘图标直接打开 Web UI；单实例互斥；操作与状态写日志
- 可选 `config.json` 配置（端口 / URL / 日志 / 数据目录 / 启停脚本）

### 安装使用
1. 下载 `dsh-tray-monitor-v1.1.0.zip` 解压到任意目录
2. 按需修改 `启动DSH.ps1` / `停止DSH.ps1`（或 `config.json`）指向你的 DSH 安装
3. 双击 `启动托盘.cmd` 启动；右键托盘图标可开启「监控开机自启」或「DSH 开机自启」

### 要求
- Windows 10/11（自带 .NET Framework 4.x）
- 需要先部署好 DeepSeek Harness（DSH）Web 服务

### 说明
- 本软件由 DeepSeek 辅助编写（Developed with the assistance of DeepSeek）
- DeepSeek logo 版权归 DeepSeek 所有

---

## v1.0.1 (2026-08-27)

- 🐛 修复托盘偶发崩溃：悬浮提示文本强制截断至 63 字符（NotifyIcon.Text 上限）；PID 解析改用 netstat；增加全局异常保护
- 其余功能同 v1.0.0

---

## v1.0.0 (2026-08-26)

DeepSeek Harness（DSH）Web 服务的**常驻任务栏通知区监控工具**（DeepSeek 辅助编写）。

### 功能
- 常驻任务栏通知区，每 3 秒探测 DSH 服务端口
- 状态圆点：🟦 蓝=运行中 / 🟥 红=停止；Windows 通知显示 DSH logo
- 右键菜单：启动 / 停止 / 重启 DSH、打开 Web UI、打开数据目录、打开日志、开机自启、退出
- 双击托盘图标直接打开 Web UI；单实例互斥；操作与状态写日志
- 可选 `config.json` 配置（端口 / URL / 日志 / 数据目录 / 启停脚本）

### 安装使用
1. 下载 `dsh-tray-monitor-v1.0.0.zip` 解压到任意目录
2. 按需修改 `启动DSH.ps1` / `停止DSH.ps1`（或 `config.json`）指向你的 DSH 安装
3. 双击 `启动托盘.cmd` 启动；右键托盘图标可开启「开机自启」

### 要求
- Windows 10/11（自带 .NET Framework 4.x）
- 需要先部署好 DeepSeek Harness（DSH）Web 服务

### 说明
- 本软件由 DeepSeek 辅助编写（Developed with the assistance of DeepSeek）
- DeepSeek logo 版权归 DeepSeek 所有