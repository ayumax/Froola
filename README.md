# Froola
Unreal Engineコードプラグインプロジェクトのマルチプラットフォーム（Windows, Mac, Linux）自動ビルド・テストツール


## 概要
Froolaは、Unreal Engineコードプラグインプロジェクトのビルドとテストを自動化し、複数プラットフォーム（Windows/Mac/Linux）での動作検証を容易にします。

またコードプラグインのパッケージを全プラットフォーム入りのマージされた成果物として出力します。

## 特徴
- UE5.0以降に対応(5.3以降のみ動作確認を実施済み)
- コマンドラインから簡単に実行可能
- CI/CDやクロスプラットフォーム開発現場での利用を想定
- .NET9.0 + C# 12で開発

## サポート環境
- Unreal Engine: 5.0以降
- OS: Windows, Mac, Linux（LinuxはWindows上のDocker経由で実行）
- パッケージタイプ: Win64, Mac, Linux, Android, IOS

## Setup方法
開発のベースはWindowsを前提としています。Windows以外はオプションです。
そのためFroolaもWindows上で実行します。

またAndroidビルドはWindowsのUEで、IOSビルドはMacのUEで行います。
LinuxビルドはWindows PCで行いますが、Windows UEではなく、Dockerを使用します。


### 共通設定
1. UE Pluginプロジェクトを作成しテスト、パッケージビルドを可能な状態にする
2. GitHubリポジトリにプッシュする
   - リポジトリ構成はPluginsディレクトリのみでなくUnreal Editorで読み込み可能なように、UEプロジェクトディレクトリをトップディレクトリにする
   - 参考リポジトリ : [ObjectDeliverer](https://github.com/ayumax/ObjectDeliverer)

### Windows設定
1. Unreal Engine 5.0以降のバージョンをインストール
2. Visual Studioインストール(C++ビルド環境を構築)

- 以下はAndroid向けパッケージ作成をしたい場合

3. Unreal Engineをインストールする際にAndroidにチェックをいれておく
4. Android開発環境構築(参考：[公式ドキュメント](https://dev.epicgames.com/documentation/ja-jp/unreal-engine/set-up-android-sdk-ndk-and-android-studio-using-turnkey-for-unreal-engine))

### Mac設定
1. Unreal Engine 5.0以降のバージョンをインストール
2. Xcodeインストール(C++ビルド環境を構築)
3. WindowsからMacへSSH接続設定（SSH/SCPを利用）
   - SSH接続はパスワード、公開鍵認証のいずれかで設定してください
4. SSHでxcode-selectをsudoなしで実行できる設定(任意)
   - 通常、xcode-selectコマンドは管理者権限（sudo）が必要ですが、セキュリティ上の理由からsudoなしで実行できるようにするには、特定コマンドのみパスワードなしsudoを許可する設定を行います。
   - 以下の手順で設定してください。
   - Xcodeを切り替えない場合はxcode-selectは不要です
   - UEとXcodeのバージョン関係は[公式ドキュメント](https://dev.epicgames.com/documentation/ja-jp/unreal-engine/ios-ipados-and-tvos-development-requirements-for-unreal-engine)を参考にしてください
   - Xcodeを切り替える場合は、別途appsettings.jsonのMac.XcodeNamesを更新してください

   1. ターミナルでvisudoを実行してsudoersファイルを編集します。
      ```sh
      sudo visudo
      ```
   2. sudoersファイルの末尾に以下の行を追加します（youruserはMacにSSH接続するユーザー名に置き換えてください）。
      ```sh
      youruser ALL=(ALL) NOPASSWD: /usr/bin/xcode-select
      ```
   3. これでSSH経由でも `sudo xcode-select` をパスワードなしで実行できるようになります。
   4. Froolaのビルドプロセスでは `xcode-select` をsudoをつけずに実行するため、上記設定が必要です。
   5. セキュリティ上、他のsudoコマンドは許可しないように注意してください。


### Linux（Docker）設定
1. Dockerのインストール(Podmanでも可)
2. Unreal EngineのDockerイメージ取得（slim推奨）
   1. GitHubのUnreal Engineのリポジトリへのアクセス権を取得 [公式ドキュメント](https://www.unrealengine.com/ja/ue-on-github)
   2. docker loginするためのGitHubアクセストークン(read:package 権限をつける)を取得
   3. コマンドを実行してイメージをダウンロード

#### Dockerイメージ取得例(UE5.5 slimイメージ)
```sh
// ログイン(GitHubのIDとアクセストークンを入力する)
docker login ghcr.io
// イメージをダウンロード
docker pull ghcr.io/epicgames/unreal-engine:dev-slim-5.5.0
```

## Froolaのインストール・セットアップ
1. GitHubリリースから最新のFroolaをダウンロードして解凍
2. `appsettings.json`ファイルを更新して基本設定を行う（例：デフォルトのパスや認証情報など）

## 設定方法
- 基本設定は `appsettings.json` に記述
- 実行時の詳細設定や上書きはコマンドライン引数で指定

#### appsettings.json例
```json
{
  "Git": {
    "GitRepositoryUrl": "",
    "GitBranch": "main",
    "GitSshKeyPath": "C:\\Users\\user\\.ssh\\id_rsa"
  },
  "InitConfig": {
    "OutputPath": ""
  },
  "Mac": {
    "MacUnrealBasePath": "/Users/Shared/Epic Games",
    "SshUser": "",
    "SshPassword": "",
    "SshPrivateKeyPath": "",
    "SshHost": "192.168.1.100",
    "SshPort": 22,
    "XcodeNames": {
      "5.5": "/Applications/Xcode.app",
      "5.4": "/Applications/Xcode_14.1.app",
      "5.3": "/Applications/Xcode_14.1.app"
    }
  },
  "Plugin": {
    "EditorPlatforms": ["Windows","Mac","Linux"],
    "EngineVersions": ["5.5"],
    "ResultPath": "",
    "RunTest": true,
    "RunPackage": true,
    "PackagePlatforms": ["Win64","Mac","Linux","Android","IOS"]
  },
  "Windows": {
    "WindowsUnrealBasePath": "C:\\Program Files\\Epic Games"
  },
  "Linux": {
    "DockerCommand": "docker",
    "DockerImage": "ghcr.io/epicgames/unreal-engine:dev-slim-%v"
  }
}
```

最新のappsettings.jsonテンプレートはFroolaから以下のコマンドで出力可能です。出力後に編集してください。
```sh
Froola.exe init-config -o "path to save config template(*.json)"
```

### appsettings.json 各項目の説明テンプレート（全項目分）

| 項目名（パス）                        | 型         | 説明                                           | 例                                             |
|--------------------------------------|------------|------------------------------------------------|-----------------------------------------------|
| Git.GitRepositoryUrl                 | string     | GitリポジトリのURL                             | "git@github.com:xxx/yyy.git"                 |
| Git.GitBranch                        | string     | チェックアウトするブランチ名                    | "main"                                       |
| Git.GitSshKeyPath                    | string     | GitHub用のSSH秘密鍵ファイルのパス (※1)         | "C:\\Users\\user\\.ssh\\id_rsa"            |
| InitConfig.OutputPath                | string     | appsetting.jsonテンプレート出力先パス (※2)      | "C:\\FroolaConfig"                           |
| Mac.MacUnrealBasePath                | string     | Mac上のUnreal Engineインストールベースパス      | "/Users/Shared/Epic Games"                   |
| Mac.SshUser                          | string     | Mac接続時のSSHユーザー名                        | "macuser"                                    |
| Mac.SshPassword                      | string     | Mac接続時のSSHパスワード（公開鍵認証時は不要）   | "password"                                   |
| Mac.SshPrivateKeyPath                | string     | Mac接続時のSSH秘密鍵パス（公開鍵認証の場合に必要）     | "/Users/user/.ssh/id_rsa"                    |
| Mac.SshHost                          | string     | MacのIPアドレスまたはホスト名                   | "192.168.1.100"                              |
| Mac.SshPort                          | int        | MacのSSHポート番号                              | 22                                            |
| Mac.XcodeNames                       | Key-Value  | UEバージョンごとのXcodeパス  (※3)                 | {"5.5":"/Applications/Xcode.app"}            |
| Plugin.EditorPlatforms               | array      | 利用するエディタープラットフォーム                | ["Windows","Mac","Linux"]                   |
| Plugin.EngineVersions                | array      | 利用するUnreal Engineバージョン                 | ["5.5"]                                      |
| Plugin.ResultPath                    | string     | 結果出力先ディレクトリ (※4)                     | "C:\\UEPluginResults"                        |
| Plugin.RunTest                       | bool       | テスト実行するか                                | true                                          |
| Plugin.RunPackage                    | bool       | パッケージ作成を実行するか                      | true                                          |
| Plugin.PackagePlatforms              | array      | パッケージ作成対象のプラットフォーム             | ["Win64","Mac","Linux","Android","IOS"]     |
| Windows.WindowsUnrealBasePath        | string     | Windows上のUnreal Engineインストールベースパス   | "C:\\Program Files\\Epic Games"              |
| Linux.DockerCommand                  | string     | Dockerコマンド("docker" or "podman")           | "docker"                                     |
| Linux.DockerImage                    | string     | Dockerイメージ(%vにはUEのバージョンが入る)      | "ghcr.io/epicgames/unreal-engine:dev-slim-%v" |

※1 Git.GitSshKeyPathが未設定の場合はHTTPSでクローンをためします
※2 InitConfig.OutputPathが未設定、または空文字列の場合は、カレントディレクトリにappsettings.jsonを出力します
※3 UEのバージョンに応じて異なるバージョンのXcodeを使用する場合に設定します。この機能を使用する場合は別途xcode-selectコマンドをsudoなしで実行できる設定を行ってください。
※4 Plugin.ResultPathが未設定、または空文字列の場合は、Froola.exe同じディレクトリのoutputsディレクトリに出力します

## 使い方

最低限の引数を指定して実行する場合は以下のようになります。
```sh
Froola.exe plugin -n <plugin name> -p <project name> -u <git repository url> -b <git branch>
```

- 例1 ObjectDelivererプラグインをWindows, Mac環境でWin64, Mac, Android, IOS向けにテスト実施とパッケージ作成
```sh
Froola.exe plugin -n ObjectDeliverer -p ObjectDelivererTest -u git@github.com:ayumax/ObjectDeliverer.git -b master -e [Windows,Mac]  -v [5.5] -t -c -g [Win64,Mac,Android,IOS]
```

上記コマンドを実行すると、appsettings.jsonの設定を元に処理が行われます。

### pluginコマンドの主な引数

| オプション名                  | 型         | 説明                                             |
|------------------------------|------------|--------------------------------------------------|
| -n, --plugin-name            | string     | プラグイン名（必須）                             |
| -p, --project-name           | string     | プロジェクト名（必須）                           |
| -u, --git-repository-url     | string     | GitリポジトリURL（必須）                         |
| -b, --git-branch             | string     | ブランチ名（必須）                               |
| -e, --editor-platforms       | string[]?  | Editorプラットフォーム（例: Windows, Mac, Linux）|
| -v, --engine-versions        | string[]?  | Unreal Engineバージョン（例: 5.3, 5.4, 5.5）     |
| -o, --result-path            | string?    | 結果保存先                                       |
| -t, --run-test               | bool?      | テスト実行                                       |
| -c, --run-package            | bool?      | パッケージ実行                                   |
| -g, --package-platforms      | string[]?  | ゲームプラットフォーム（Win64, Mac, Linux, Android, IOS）|


※必須でない項目はappsettings.jsonでも設定可能。両方で指定時はコマンドライン引数が優先されます。
※各プラットフォーム・バージョン指定はカンマ区切りの配列で指定します。
※配列表記は["Windows","Mac","Linux"]のように指定します。

### pluginコマンド実行時の主な流れ
1. 指定したGitリポジトリのブランチWindows上にクローン
2. クローンしたディレクトリを各プラットフォームごとにコピー
3. 各プラットフォームごとにビルド・テスト・パッケージ処理を実行します。
4. 結果は `results` ディレクトリに保存されます。
5. プラグインパッケージの成果物はUEバージョンごとにマージされます。
6. releaseディレクトリをzip圧縮すればそのままマルチプラットフォーム対応プラグインとしてFabへアップロードできます。


## 結果の保存先
結果は --result-path で指定したディレクトリ（もしくはappsettins.jsonのPlugin.ResultPath）に以下のように保存されます：

プラグイン名=ObjectDeliverer、UE5.5でWindwos, Mac, Linux環境で実行した場合
```
20250502_205034_ObjectDeliverer/
├── build
│   ├── Windows_UE5.5
│   │   └── Build.log
│   ├── Mac_UE5.5
│   │   └── Build.log
│   └── Linux_UE5.5
│       └── Build.log
├── tests
│   ├── Windows_UE5.5
│   │   ├── AutomationTest.log
│   │   ├── index.html
│   │   └── index.json
│   ├── Mac_UE5.5
│   │   ├── AutomationTest.log
│   │   ├── index.html
│   │   └── index.json
│   └── Linux_UE5.5
│       ├── AutomationTest.log
│       ├── index.html
│       └── index.json
├── packages
│   ├── Windows_UE5.5
│   │   ├── BuildPlugin.log
│   │   └── Plugin
│   │       ├── ObjectDeliverer.uplugin
│   │       ├── Binaries
│   │       ├── Intermediate
│   │       ├── Resources
│   │       └── Source
│   ├── Mac_UE5.5
│   │   ├── BuildPlugin.log
│   │   └── Plugin
│   │       ├── ObjectDeliverer.uplugin
│   │       ├── Binaries
│   │       ├── Intermediate
│   │       ├── Resources
│   │       └── Source
│   └── Linux_UE5.5
│       ├── BuildPlugin.log
│       └── Plugin
│           ├── ObjectDeliverer.uplugin
│           ├── Binaries
│           ├── Intermediate
│           ├── Resources
│           └── Source
├── releases
│   └── ObjectDeliverer_UE5.5
│       ├── ObjectDeliverer.uplugin
│       ├── Binaries
│       ├── Intermediate
│       ├── Resources
│       └── Source
├── froola.log
└── settings.json
```

- buildディレクトリ : 各プラットフォームごとのビルド結果
  - Build.log : UEがビルド時に出力したログ
- testsディレクトリ : 各プラットフォームごとのテスト結果
  - AutomationTest.log : UEがテスト時に出力したログ
  - index.html : テスト結果のHTML
  - index.json : テスト結果のJSON
- packagesディレクトリ : 各プラットフォームごとのパッケージ結果
  - BuildPlugin.log : UEがパッケージ時に出力したログ
  - Pluginディレクトリ : パッケージ結果
- releasesディレクトリ : 各プラットフォームごとのリリース結果
  - <プラグイン名>_<UEバージョン>ディレクトリ : packagesディレクトリをマージして1つにしたプラグインパッケージ
- froola.log : Froolaのログ
- settings.json : appsettings.jsonとコマンドライン引数両方をマージした実行時の設定


### 注意事項
- Froola実行前に、各プラットフォームごとに必要な依存サービス（Docker, SSH, Xcode, Visual Studioなど）が正しくセットアップされている必要があります。
- Macのパッケージ作成はSSH経由でMac上でビルド・テストされます。
- LinuxビルドはWindows上のDockerで行われます。


## ライセンス・注意事項
- 本ツールはMITライセンスで提供されています。
- セキュリティ上、SSHやsudo設定は十分ご注意ください。

## Contribute

Froolaへの貢献を歓迎しています！バグ報告や新機能の提案、プルリクエスト（PR）はいつでもお待ちしています。

- バグや改善点があれば、まずはIssueを作成してください。
- コードの変更を提案する場合は、Forkしてブランチを作成し、PRを送ってください。
- PRの際は、できるだけ詳細な説明と、関連するIssueがあればリンクをお願いします。
- 新規追加されたクラスやメソッドは、可能な限りテストを追加してください。
- すべての貢献者に感謝します！
