# Release Notes

## v2.1 (2026-09-27)

### 修复
- 🐛 修复「DSH 未启动时偶发先弹『已开启』、紧接着弹『已停止』」的状态抖动：
  - **连续确认**：判定「已开启」需连续 3 次探测一致（约 9s），「已停止」需连续 2 次；单次瞬时端口占用不再触发通知
  - **占用者校验**：TCP 可连后再校验监听进程是否为 node/DSH；被其它进程占用端口时忽略并记录日志
    `probe: 端口被非 DSH 进程占用，已忽略 [PID xx name]`
- ⚡ 探测超时 600ms → 400ms，减少 UI 线程阻塞；PID 解析逻辑统一复用

### 说明
- 本版探测过程**不发送 HTTP 请求**（避免安全软件行为启发式误报）
- 若被 McAfee 等误报拦截，可参考 `DSH白名单清单`（建议排除部署目录与源目录）

### 环境要求
- Windows 10/11（自带 .NET Framework 4.x）
- Node.js 22.19+ 或 24+（https://nodejs.org/）
- 已部署 DeepSeek Harness（https://github.com/deepseek-ai/deepseek-harness）

### 说明（总）
- 本软件由 DeepSeek 辅助编写（Developed with the assistance of DeepSeek）；DeepSeek logo 版权归 DeepSeek 所有

---
## v2.0 (2026-09-26)

### 新增
- 🆕 右键菜单新增「配置…」，打开独立配置窗口
- ⚙️ 可指定：Node 可执行文件、DSH 部署目录、DSH 数据目录（DSH_HOME）、日志目录、部署目录、Web 端口、监控/DSH 开机自启
- 🚀 「部署」一键自部署：生成 `dsh-tray-monitor.exe`、`ico\`、`config.json`、`启动DSH.ps1`、`停止DSH.ps1`、`启动托盘.cmd`
- 📦 **单文件 exe 分发**：三个状态图标内嵌为资源，仅作分发/兜底；运行时优先读 `ico\`，缺失自动回退，部署时自动释放
- 🧭 环境缺失提示：未检测到 Node.js / DSH 时，红字提示并询问是否打开官方下载/仓库页
- 🪟 配置窗口紧凑化（660×332），去掉多余空白 - 🧭 **首次部署路径指引**：无 `config.json` 时目录字段留空并显示占位提示（例如 D:\Deepseek-harness） - 🔍 新增「**自动检测**」：一键检测 Node、DSH 部署目录、DSH 数据目录，并据此推导日志目录与部署目录 - 📝 首次运行时日志默认写入 exe 所在目录（tray.log）

### 修复
- 🐛 修复切换部署目录后，原目录 exe/文件夹被占用无法删除：新实例工作目录改为新目录（start /D），旧实例改为 Environment.Exit(0) 立即退出

### 环境要求
- Windows 10/11（自带 .NET Framework 4.x）
- Node.js 22.19+ 或 24+（https://nodejs.org/）
- 已部署 DeepSeek Harness（https://github.com/deepseek-ai/deepseek-harness）

### 说明
- 本软件由 DeepSeek 辅助编写（Developed with the assistance of DeepSeek）；DeepSeek logo 版权归 DeepSeek 所有

---

## v1.1.0 (2026-08-27)

### 修复与改进
- 🆕 新增「DSH 开机自启」：登录时自动启动 DSH Web 服务（注册表 Run 键 `DSHWebService`），与「监控开机自启」相互独立
- 🐛 修复开机自启后状态图标默认显示蓝色（运行）的问题：启动时按 DSH 真实状态显示图标
- 🎨 程序（exe）改用**中性图标** `dsh-logo.ico`（无状态圆点），不再用运行状态图标
- 🔔 通知气泡改用中性程序图标，开启/停止分别以蓝色 Info / 黄色 Warning 色调区分

---

## v1.0.1 (2026-08-27)

- 🐛 修复托盘偶发崩溃：悬浮提示文本强制截断至 63 字符（NotifyIcon.Text 上限）；PID 解析改用 netstat；增加全局异常保护

---

## v1.0.0 (2026-08-26)

DeepSeek Harness（DSH）Web 服务的**常驻任务栏通知区监控工具**（DeepSeek 辅助编写）。

### 功能
- 常驻任务栏通知区，每 3 秒探测 DSH 服务端口
- 状态圆点：🟦 蓝=运行中 / 🟥 红=停止；Windows 通知显示 DSH logo
- 右键菜单：启动 / 停止 / 重启 DSH、打开 Web UI、打开数据目录、打开日志、开机自启、退出
- 双击托盘图标直接打开 Web UI；单实例互斥；操作与状态写日志
- 可选 `config.json` 配置（端口 / URL / 日志 / 数据目录 / 启停脚本）

### 要求
- Windows 10/11（自带 .NET Framework 4.x）
- 需要先部署好 DeepSeek Harness（DSH）Web 服务

### 说明
- 本软件由 DeepSeek 辅助编写（Developed with the assistance of DeepSeek）
- DeepSeek logo 版权归 DeepSeek 所有