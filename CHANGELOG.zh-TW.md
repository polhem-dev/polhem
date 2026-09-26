# 版本變更記錄

[English](CHANGELOG.md)

Polhem 套件的重要變更。格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循
[語意化版本](https://semver.org/lang/zh-TW/)。每個版本在這裡以一行列一項變更；理由與背景寫在
[`docs/changelogs/`](docs/changelogs/) 下該版本的明細。

## [Unreleased]

## [1.0.0] - Unreleased

> Polhem 以新名稱延續 [Bee.NET](https://github.com/jeff377/bee-library)。以下變更相對於 Bee.NET 最後發佈的 4.33.0：
> 除了改名，程式碼就是 Bee.NET 4.33.0 的程式碼。應用程式如何遷移，見 [從 Bee.NET 遷移](README.zh-TW.md#從-beenet-遷移)。

📄 完整說明與背景：[docs/changelogs/1.0.0.zh-TW.md](docs/changelogs/1.0.0.zh-TW.md)

### 變更

- 套件 ID 與命名空間由 `Bee.*` 改名為 `Polhem.*`，版號從 1.0.0 重新起算。套件的切分不變。
- 以 Bee 命名的型別與成員改名：`AddBeeFramework` → `AddPolhemFramework`、`UseBeeFramework` →
  `UsePolhemFramework`、`AddBeeBlazor` → `AddPolhemBlazor`、`IBeeContext` / `BeeContext` → `IPolhemContext` /
  `PolhemContext`、`BeeStringLocalizer<T>` → `PolhemStringLocalizer<T>`、`BeeBlazorOptions` → `PolhemBlazorOptions`、
  `BeeBlazorProviderMode` → `PolhemBlazorProviderMode`、`BeeApiConnectorFactory` → `PolhemApiConnectorFactory`、
  `BeeAccessTokenProvider` → `PolhemAccessTokenProvider`、`BeeLoginPanel` → `PolhemLoginPanel`，以及擴充方法類別
  `BeeFrameworkServiceCollectionExtensions`、`BeeFrameworkApplicationBuilderExtensions`、
  `BeeBlazorServiceCollectionExtensions` → `Polhem…`。
- 命令列工具 `Bee.Cli` 改為 `Polhem.Cli`，以 `dotnet polhem` 呼叫。
- analyzer 診斷代號由 `BEE` 改為 `POLHEM`，數字不變，例如 `BEE1008` → `POLHEM1008`。
- 定義檔檢查用的 MSBuild 屬性改名：`BeeDefinitionFilesGlob`、`BeeRequireDefinitionFiles`、`BeeAnalyzeDefinitionFiles`
  → `PolhemDefinitionFilesGlob`、`PolhemRequireDefinitionFiles`、`PolhemAnalyzeDefinitionFiles`。
- 主金鑰的預設環境變數由 `BEE_MASTER_KEY` 改為 `POLHEM_MASTER_KEY`。
- payload 與預設設定中的型別名稱改為 `Polhem.*`。Bee.NET 用戶端與 Polhem 伺服端（或反過來）無法互通。
- 衍生 session 加密金鑰的標籤由 `bee-api-*` 改為 `polhem-api-session-key` 與 `polhem-api-encryption-root-key`，
  所以部署切換時，Bee.NET 建立的 session 會失效。
- Blazor 元件的 CSS class 由 `bee-` 開頭改為 `polhem-` 開頭。
- 記錄欄位宣告型別的 `DataColumn` 擴充屬性由 `Bee.FieldDbType` 改為 `Polhem.FieldDbType`；
  `SerializationErrorData.FilePath` 由 `Bee.FilePath` 改為 `Polhem.FilePath`。
- 套件中繼資料：作者與著作權人改為 Polhem contributors，repository 改為 `polhem-dev/polhem`，並換上新的圖示。

[Unreleased]: https://github.com/polhem-dev/polhem/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.0.0
