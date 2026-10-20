# 署名付きスパースパッケージ (ImageConverter.msix) を作成する
$root = $PSScriptRoot
& "$root\get-tools.ps1"
$tools = Get-ChildItem "$root\tools\pkg\bin" -Recurse -Filter makeappx.exe | Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1
$bt = $tools.DirectoryName
$out = Join-Path $root 'ImageConverter.msix'
$cer = Join-Path $root 'ImageConverter.cer'

# 自己署名証明書 (なければ作成)
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=ImageConverter' -and $_.NotAfter -gt (Get-Date) } | Select-Object -First 1
if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type Custom -Subject 'CN=ImageConverter' -FriendlyName 'ImageConverter' `
        -KeyUsage DigitalSignature -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(10) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}
Export-Certificate -Cert $cert -FilePath $cer -Force | Out-Null

if (Test-Path $out) { Remove-Item $out -Force }
& "$bt\makeappx.exe" pack /d "$root\pkg" /p $out /nv /o
if ($LASTEXITCODE -ne 0) { throw 'makeappx 失敗' }
& "$bt\signtool.exe" sign /fd SHA256 /sha1 $cert.Thumbprint $out
if ($LASTEXITCODE -ne 0) { throw 'signtool 失敗' }
Write-Host "作成しました: $out"
