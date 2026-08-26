# ============================================================
#  DeepSeek Harness (DSH) 启动脚本
#  主部署 : D:\Deepseek-harness
#  数据目录: D:\Deepseek-harness-data (DSH_HOME)
#  默认地址: http://127.0.0.1:3080
#  用法   : powershell -ExecutionPolicy Bypass -File .\启动DSH.ps1
# ============================================================
$ErrorActionPreference = 'Stop'
$repo    = 'D:\Deepseek-harness'
$env:DSH_HOME = 'D:\Deepseek-harness-data'
$logDir  = 'D:\Deepseek-harness-data\logs'
$node    = 'C:\Program Files\nodejs\node.exe'

if (-not (Test-Path $repo)) { Write-Host "[错误] 未找到部署目录 $repo" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $node))  { Write-Host "[错误] 未找到 Node.js: $node" -ForegroundColor Red; exit 1 }

if (Get-NetTCPConnection -LocalPort 3080 -State Listen -ErrorAction SilentlyContinue) {
    Write-Host 'DSH 已在运行：http://127.0.0.1:3080' -ForegroundColor Green
    exit 0
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$p = Start-Process -FilePath $node -ArgumentList '--import','tsx/esm','apps/cli/src/bin.ts','web','--no-open' `
    -WorkingDirectory $repo `
    -RedirectStandardOutput "$logDir\web.out.log" `
    -RedirectStandardError  "$logDir\web.err.log" `
    -WindowStyle Hidden -PassThru

Start-Sleep -Seconds 10
if (-not $p.HasExited) {
    Write-Host "DSH 已启动 (PID $($p.Id))：http://127.0.0.1:3080" -ForegroundColor Green
} else {
    Write-Host '[错误] DSH 启动失败，日志如下：' -ForegroundColor Red
    Get-Content "$logDir\web.err.log" -ErrorAction SilentlyContinue | Select-Object -Last 30
    exit 1
}
