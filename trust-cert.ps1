# 管理者権限で実行: 証明書を「信頼された発行元(TrustedPeople)」に登録する
Import-Certificate -FilePath (Join-Path $PSScriptRoot 'ImageConverter.cer') -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
Write-Host '証明書を信頼済みに登録しました。'
Start-Sleep 2
