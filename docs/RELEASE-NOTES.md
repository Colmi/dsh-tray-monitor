# Release Notes

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