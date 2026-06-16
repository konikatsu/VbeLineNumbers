# VbeLineNumbers

Access の VBE（VBAエディター）のコード画面の左側に、行番号を表示する COM アドインです。

主な対象:

- Microsoft Access 2021 64bit
- Microsoft Access 2021 32bit
- VBE / VBAエディター
- .NET Framework 4.8

## ダウンロード

最新版は GitHub Releases からダウンロードできます。

[Releases](https://github.com/konikatsu/VbeLineNumbers/releases)

Access が 64bit 版なら:

- `VbeLineNumbers-v0.1.0-x64.zip`

Access が 32bit 版なら:

- `VbeLineNumbers-v0.1.0-x86.zip`

## Access が 32bit か 64bit か確認する

Access を開いて、次の順に確認します。

```text
ファイル
-> アカウント
-> Access のバージョン情報
```

表示された画面に `64 ビット` または `32 ビット` と書かれています。

分からない場合は、まず 64bit 版を試すのではなく、必ずこの画面で確認してください。

## インストール前の準備

1. ダウンロードした zip を右クリックします。
2. `すべて展開` を選びます。
3. 分かりやすい場所に展開します。

例:

```text
C:\VbeLineNumbers\x64
```

または:

```text
C:\VbeLineNumbers\x86
```

展開したフォルダーには、次のファイルが入っています。

```text
VbeLineNumbers.dll
VbeLineNumbers.ini
README.md
```

`VbeLineNumbers.dll` と `VbeLineNumbers.ini` は同じフォルダーに置いてください。

## 64bit Access へのインストール

64bit Access を使っている場合の手順です。

1. スタートメニューで `PowerShell` を検索します。
2. `Windows PowerShell` を右クリックします。
3. `管理者として実行` を選びます。
4. 次のコマンドを実行します。

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" `
  "C:\VbeLineNumbers\x64\VbeLineNumbers.dll" `
  /codebase
```

展開先を変えた場合は、`C:\VbeLineNumbers\x64\VbeLineNumbers.dll` の部分を実際の場所に変更してください。

## 32bit Access へのインストール

32bit Access を使っている場合の手順です。

1. スタートメニューで `PowerShell` を検索します。
2. `Windows PowerShell` を右クリックします。
3. `管理者として実行` を選びます。
4. 次のコマンドを実行します。

```powershell
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" `
  "C:\VbeLineNumbers\x86\VbeLineNumbers.dll" `
  /codebase
```

展開先を変えた場合は、`C:\VbeLineNumbers\x86\VbeLineNumbers.dll` の部分を実際の場所に変更してください。

## インストール後の確認

1. Access を起動します。
2. データベースを開きます。
3. `Alt + F11` を押して VBE を開きます。
4. 標準モジュールなどのコード画面を開きます。
5. コード画面の左側に行番号が表示されれば成功です。

表示されない場合は、Access を一度終了してから、もう一度起動してください。

## アンインストール

インストール時と同じ DLL の場所を指定して、`/unregister` を実行します。

64bit 版:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" `
  "C:\VbeLineNumbers\x64\VbeLineNumbers.dll" `
  /unregister
```

32bit 版:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" `
  "C:\VbeLineNumbers\x86\VbeLineNumbers.dll" `
  /unregister
```

## 背景色の変更

行番号の背景色は `VbeLineNumbers.ini` で変更できます。

```ini
BackgroundColor=Gainsboro
```

例:

```ini
BackgroundColor=#F0F0F0
```

変更後は、Access を終了してから起動し直してください。

## 注意点

- このアドインはVBAコード自体を書き換えません。
- 行番号は画面上に重ねて表示しているだけです。
- インストールとアンインストールには管理者権限の PowerShell が必要です。
- 64bit Access には x64 版を使ってください。
- 32bit Access には x86 版を使ってください。
- x64版は Access 2021 64bit で動作確認しています。
- x86版はビルドと RegAsm 登録を確認していますが、32bit Access 実機での表示確認は未実施です。

## 開発者向け

64bit Release ビルド:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" `
  VbeLineNumbers.csproj `
  /p:Configuration=Release `
  /p:Platform=x64
```

32bit Release ビルド:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  VbeLineNumbers.csproj `
  /p:Configuration=Release `
  /p:Platform=x86
```

COM互換性のため、既存の `Guid` と `ProgId` は変更しないでください。
