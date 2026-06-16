# Codex運用メモ

このメモは、Codexにこのプロジェクトの作業を依頼するときの運用ルールです。

## 基本方針

Codexには、調査、修正、ビルド、確認、ローカルコミットまでを任せます。

ただし、外部へ影響する操作や破壊的な操作は、必ず確認してから実行します。

## Codexのアクセス許可

Codexには、このプロジェクトフォルダーをワークスペースとして開き、プロジェクト内の読み書きを許可します。

Codexデスクトップアプリでは、新しいプロジェクトを追加するときに「既存のフォルダーを指定」相当の操作を使います。
このとき、Visual Studioの外側のソリューションフォルダーではなく、内側のC#プロジェクトフォルダーを選択します。

プロジェクトルート例:

```text
C:\Users\noran\source\repos\VbeLineNumbers\VbeLineNumbers
```

別PCではユーザー名や配置先が変わるため、Codexに作業を依頼する前に、現在のワークスペースがプロジェクトルートになっているか確認します。

Codexに許可する主な操作:

- ワークスペース内ファイルの読み書き
- PowerShellコマンド実行
- Git操作
- MSBuildによるビルド
- Access GUI確認
- 必要に応じたスクリーンショット確認

Codexに許可しない、または毎回確認する操作:

- `git push`
- GitHub Release更新
- RegAsm登録/登録解除
- `MSACCESS.EXE` などのプロセス停止
- ワークスペース外への書き込み
- 破壊的な削除

Codexが承認ダイアログを出した場合は、操作内容を確認してから許可します。

## 別PCで作業するときの確認

別PCで同じ作業をする場合、最初にCodexへ次を確認させます。

```text
プロジェクトルート、Git状態、Visual Studio/MSBuild、Git、GitHub CLI、Access、RegAsmの場所を確認してください。
まだ修正はしないでください。
```

確認しておく項目:

- `Get-Location` がプロジェクトルートを指しているか
- `git status --short --branch` が正常に動くか
- MSBuildが存在するか
- 64bit/32bit Accessのどちらを使うか
- `RegAsm.exe` のパスが正しいか
- GitHub CLIでログイン済みか
- `VbeLineNumbers_Test.accdb` を使うか、新しく確認用DBを作るか

## 追加確認なしで実行してよい操作

- `git status`
- `git diff`
- `git add`
- `git commit`
- ファイル一覧確認
- grep / rg
- ビルド
- ワークスペース内のファイル編集
- Access GUI操作

## 確認が必要な操作

- `git push`
- GitHub Releaseへのアップロード
- RegAsm登録/登録解除
- `MSACCESS.EXE` などのプロセス停止
- ワークスペース外への書き込み
- 破壊的な削除

## Access確認時のルール

Accessを使った確認では、次の流れを守ります。

1. 必要ならAccessを終了する。
2. DLLをビルドする。
3. テストDBを開く。
4. VBEを表示する。
5. 行番号の位置を確認する。
6. 確認後、Accessを終了する。
7. `MSACCESS.EXE` が残っていないか確認する。

`Application.Quit()` 後も `MSACCESS.EXE` が残ることがあります。

残ったプロセスを停止する場合は、必ず確認します。

## Computer Use / GUI確認の頼み方

AccessやVBEの確認を依頼するときは、次のように具体的に指定します。

```text
Accessを開いてVBEを表示し、フォントサイズ14、16、18で、
表示範囲の先頭行と最終行が行番号と揃っているか確認してください。
確認後はAccessを終了してください。
```

ウィンドウ状態も確認する場合:

```text
フォント14、最大化、元に戻す、左上へ移動、中央へ移動、右下へ移動、
フォント18、最大化、元に戻す、の順で確認してください。
```

行番号ずれの判断基準:

- 先頭行が合っているか
- 最終行が合っているか
- 途中の行だけで判断しない

## GitHub運用

通常の流れ:

1. ローカルで修正する。
2. ビルドする。
3. Accessで確認する。
4. ローカルコミットする。
5. ある程度まとまったら `git push` する。
6. 配布する段階でGitHub Releaseのzipを更新する。

`git push` と GitHub Release更新は別操作です。

`git push` しても、Release zipは自動更新されません。

## 32bit版テスト

32bit版は、GitHub Releaseからx86 zipをダウンロードして確認します。

理由:

- GitHubに公開した配布物そのものを確認できる
- ローカルDebug DLLとRelease配布DLLの取り違えを防げる

## RegAsmの注意

このプロジェクトは.NET FrameworkのCOMアドインなので、登録には `regsvr32` ではなく `RegAsm.exe` を使います。

64bit:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" `
  "C:\path\to\VbeLineNumbers.dll" `
  /codebase
```

32bit:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" `
  "C:\path\to\VbeLineNumbers.dll" `
  /codebase
```

## 推論effortの使い分け

料金を抑えたい場合、通常作業は low で進めます。

medium に上げる場面:

- C#の実装修正方針を判断する場合
- ビルドエラーの原因解析をする場合
- Access / COM / RegAsm / VBE 絡みの判断をする場合
- git commit前に差分を精査する場合
- 禁止事項に触れる可能性がある操作を判断する場合

low のままでよい場面:

- grep / rg
- ファイル一覧確認
- `git status`
- `git diff`
- 単純な文言修正
- 方針が決まっている小修正

## Codexへの最初の依頼例

```text
このVbeLineNumbersプロジェクトを引き継いでください。
まず全ファイルを確認し、現在の実装状況と問題点を簡潔に報告してください。
その後、必要な修正を直接行ってください。

GuidとProgIdは変更しないでください。
RegAsm、git push、プロセス停止、ワークスペース外書き込みは実行前に確認してください。
ローカルコミットは確認不要です。
Access GUI操作は確認不要です。
```
