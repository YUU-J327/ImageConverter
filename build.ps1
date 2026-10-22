$root = $PSScriptRoot
$bin = Join-Path $root 'bin'
$assets = Join-Path $root 'pkg\Assets'
New-Item -ItemType Directory -Force $bin, $assets | Out-Null

# WebP 用の libwebp.dll を取得 (取得済みなら何もしない)
& "$root\get-libwebp.ps1"

# ロゴ画像を生成
Add-Type -AssemblyName System.Drawing
foreach ($s in @(@('StoreLogo', 50), @('Square44x44Logo', 44), @('Square150x150Logo', 150))) {
    $size = $s[1]
    $b = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::FromArgb(0, 120, 215))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [Math]::Max(2, $size / 14))
    $m = $size * 0.25
    $g.DrawRectangle($pen, $m, $m, $size - 2 * $m, $size - 2 * $m)
    $g.DrawLine($pen, $m, $size - $m, $size / 2, $size * 0.45)
    $g.DrawLine($pen, $size / 2, $size * 0.45, $size - $m, $size - $m)
    $g.Dispose()
    $b.Save((Join-Path $assets "$($s[0]).png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
}

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
# libwebp.dll は 64bit (x64) 版なので、exe も x64 に固定する
& $csc /nologo /target:winexe /platform:x64 /optimize+ /out:"$bin\ImageConverterCmd.exe" `
    /r:System.Drawing.dll /r:System.Windows.Forms.dll "$root\src\ImageConverterCmd.cs"
if ($LASTEXITCODE -ne 0) { throw 'ビルドに失敗しました' }
Copy-Item "$root\tools\webp\*.dll" $bin -Force
Write-Host 'ビルド完了'
