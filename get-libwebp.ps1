# WebP の読み書きに使う libwebp (libwebp.dll / libsharpyuv.dll, 約 0.4MB) を NuGet から取得して tools\webp\ に置く
# 取得元: Imazen.WebP.NativeRuntime.win-x64 (MIT)。中身の libwebp は BSD 3-Clause。build.ps1 から呼ばれる。
$ver = '1.6.1'
$sha256 = '32DF07F31F18B5F4E35409A73621D776D97761F4B601CBBBDC4EFBACB6AB62F6'
$dir = Join-Path $PSScriptRoot 'tools'
$dest = Join-Path $dir 'webp'
if ((Test-Path (Join-Path $dest 'libwebp.dll')) -and (Test-Path (Join-Path $dest 'libsharpyuv.dll'))) { return }

New-Item -ItemType Directory -Force $dir | Out-Null
$zip = Join-Path $dir 'webp.zip'
$extract = Join-Path $dir 'webp-extract'
$url = "https://api.nuget.org/v3-flatcontainer/imazen.webp.nativeruntime.win-x64/$ver/imazen.webp.nativeruntime.win-x64.$ver.nupkg"
Write-Host "ダウンロード中: $url"
Invoke-WebRequest $url -OutFile $zip -UseBasicParsing

# 取得したファイルが想定どおりか確認してから展開する
$actual = (Get-FileHash $zip -Algorithm SHA256).Hash
if ($actual -ne $sha256) {
    Remove-Item $zip -Force
    throw "libwebp パッケージのハッシュが一致しません (期待: $sha256 / 実際: $actual)"
}

if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
Expand-Archive $zip $extract -Force
New-Item -ItemType Directory -Force $dest | Out-Null
$native = Join-Path $extract 'runtimes\win-x64\native'
Copy-Item (Join-Path $native 'libwebp.dll'), (Join-Path $native 'libsharpyuv.dll') $dest -Force
Remove-Item $zip -Force
Remove-Item $extract -Recurse -Force
