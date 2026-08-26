# ============================================================
#  DeepSeek Harness (DSH) 停止脚本
#  按 3080 端口查找并停止 DSH 进程
# ============================================================
$conn = Get-NetTCPConnection -LocalPort 3080 -State Listen -ErrorAction SilentlyContinue
if (-not $conn) { Write-Host 'DSH 未在运行'; exit 0 }
$ids = $conn | Select-Object -ExpandProperty OwningProcess -Unique
foreach ($id in $ids) { Stop-Process -Id $id -Force; Write-Host "已停止 DSH 进程 PID $id" }
