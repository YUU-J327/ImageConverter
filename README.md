# 画像コンバーター (ImageConverter)

Windows 11 の**新しい右クリックメニュー**に「画像コンバーター」を追加する小さなツールです。
画像ファイルを右クリックして、PNG / JPEG / BMP / GIF / TIFF / WebP にその場で変換できます。

*A tiny Windows 11 shell extension that adds an "Image Converter" submenu (PNG / JPEG / BMP / GIF / TIFF / WebP) to the new File Explorer context menu.*

## 特長

- 「その他のオプションを確認」を開かずに、新メニューへ直接表示
- 複数ファイルの一括変換に対応
- 変換後のファイルは元の画像と同じフォルダに保存(同名があれば `名前 (1).png`)。変換に失敗したときは、途中までのファイルを残しません
- JPEG / BMP は透過非対応のため、透明部分は白になります(JPEG 品質 95)
- スマホ写真などの EXIF の向きは画素に反映し、解像度(DPI)も引き継ぎます(その他のメタデータは引き継ぎません)
- GIF は画像ごとに最適な 256 色のパレットを作って出力します(透過は 1 色分)。256 色に収まらない画像は、誤差拡散(ディザリング)で階調を補います
- アニメーション GIF / 複数ページ TIFF は、GIF・TIFF への変換では全フレームを保持し、PNG / JPEG / BMP / WebP への変換では先頭フレームだけを変換します
- WebP は読み込みと書き出しの両方に対応します(書き出しは非可逆・品質 90。透明度は劣化しません)。アニメーション WebP は読み込めません
- 対応する元画像の拡張子: `.jpg` `.jpeg` `.png` `.bmp` `.gif` `.tif` `.tiff` `.webp`
- 開発者モードは不要

## 仕組み

`IExplorerCommand` を実装したアウトプロセス COM サーバー(`src/ImageConverterCmd.cs`、.NET Framework / C#)を、
スパースパッケージ(`pkg/AppxManifest.xml`)として登録し、Explorer の新メニューに verb を追加します。
変換は `System.Drawing` で行います。`System.Drawing` が扱えない WebP だけは、[libwebp](https://developers.google.com/speed/webp)(`libwebp.dll`)を直接呼び出して読み書きします。

## 必要なもの

- Windows 11 (22000 以上)
- PowerShell 5.1(Windows 標準)
- .NET Framework 4 の `csc.exe`(Windows 標準: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`)
- Visual C++ 再頒布可能パッケージ(x64)。WebP の変換に使う `libwebp.dll` が必要とします。多くの PC には他のソフトと一緒に入っていますが、
  無い場合は [Microsoft の配布ページ](https://aka.ms/vs/17/release/vc_redist.x64.exe)から入れてください(WebP 以外の変換には不要です)
- インターネット接続(初回のみ。`makeappx` / `signtool` を含む NuGet パッケージ
  `Microsoft.Windows.SDK.BuildTools` 約21MB と、libwebp を含む NuGet パッケージ
  `Imazen.WebP.NativeRuntime.win-x64` 約0.26MB を取得します)

## ビルドとインストール

PowerShell で、このリポジトリのフォルダから順に実行します。

```powershell
# 1. ビルド (bin\ImageConverterCmd.exe とロゴを生成し、libwebp.dll を bin\ に配置)
powershell -ExecutionPolicy Bypass -File .\build.ps1

# 2. 署名付きパッケージを作成 (自己署名証明書を作り ImageConverter.msix に署名)
powershell -ExecutionPolicy Bypass -File .\package.ps1

# 3. 証明書を信頼済みに登録 (管理者権限が必要。最初の1回だけ)
Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PWD\trust-cert.ps1`""

# 4. 登録 (エクスプローラーが再起動します)
powershell -ExecutionPolicy Bypass -File .\install-newmenu.ps1
```

画像を右クリックすると、新メニューに「画像コンバーター」が表示されます。

> 登録後は `bin\ImageConverterCmd.exe`(と同じフォルダの `libwebp.dll` など)を移動・削除しないでください(メニューが動かなくなります)。
> 移動する場合は手順 4 をやり直してください。

## アンインストール

```powershell
powershell -ExecutionPolicy Bypass -File .\uninstall-newmenu.ps1
```

信頼登録した証明書(`CN=ImageConverter`)も不要なら、管理者権限の PowerShell で削除します。

```powershell
Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq 'CN=ImageConverter' | Remove-Item
```

## セキュリティについて

- 署名には、手順 2 でお使いの PC 上に作られる**自己署名証明書**を使います。秘密鍵はリポジトリに含まれず、証明書ストアにだけ保存されます。
- 手順 3 で証明書を「信頼された発行元」に登録します。自分でビルドしたパッケージにだけ使う前提です。不要になったら上記の方法で削除してください。
- ビルド済みバイナリは配布していません。ソースを確認のうえ、各自でビルドしてください。
- WebP 用の `libwebp.dll` / `libsharpyuv.dll` は、`build.ps1` が NuGet(`Imazen.WebP.NativeRuntime.win-x64` 1.6.1、MIT。中身の libwebp は BSD 3-Clause)から取得します。
  取得したパッケージは `get-libwebp.ps1` に記載した SHA-256 と一致しない場合は使いません。リポジトリには含めていません。

## ファイル構成

| パス | 内容 |
|---|---|
| `src/ImageConverterCmd.cs` | COM サーバー本体とコンバーター |
| `pkg/AppxManifest.xml` | スパースパッケージのマニフェスト |
| `build.ps1` | ビルド(csc)とロゴ生成 |
| `get-tools.ps1` | makeappx / signtool の取得 |
| `get-libwebp.ps1` | libwebp(WebP の読み書き)の取得と検証 |
| `package.ps1` | 証明書作成・パッケージ化・署名 |
| `trust-cert.ps1` | 証明書の信頼登録(管理者) |
| `install-newmenu.ps1` / `uninstall-newmenu.ps1` | 登録 / 削除 |

## ライセンス

[MIT](LICENSE)
