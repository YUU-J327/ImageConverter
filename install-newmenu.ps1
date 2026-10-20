# Windows 11 新コンテキストメニューに登録 (署名付きパッケージ。開発者モードは不要)
# 事前に package.ps1 でパッケージを作成し、trust-cert.ps1 を管理者で一度実行しておくこと。
$root = $PSScriptRoot
Get-AppxPackage -Name 'ImageConverter.ContextMenu' | Remove-AppxPackage -ErrorAction SilentlyContinue
Add-AppxPackage -Path (Join-Path $root 'ImageConverter.msix') -ExternalLocation (Join-Path $root 'bin')
Write-Host '登録しました。エクスプローラーを再起動します。'
Stop-Process -Name explorer -Force
