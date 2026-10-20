# makeappx / signtool を含む Microsoft.Windows.SDK.BuildTools (NuGet, 約21MB) を tools\ に取得する
$ver = '10.0.28000.2705'
$dir = Join-Path $PSScriptRoot 'tools'
$dest = Join-Path $dir 'pkg'
if (Get-ChildItem $dest -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue) { return }

New-Item -ItemType Directory -Force $dir | Out-Null
$zip = Join-Path $dir 'buildtools.zip'
$url = "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$ver/microsoft.windows.sdk.buildtools.$ver.nupkg"
Write-Host "ダウンロード中: $url"
Invoke-WebRequest $url -OutFile $zip -UseBasicParsing
Expand-Archive $zip $dest -Force
Remove-Item $zip
