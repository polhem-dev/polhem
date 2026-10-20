# Polhem.Cli

[English](README.md)

[Polhem](https://github.com/polhem-dev/polhem) 框架的命令列工具，以 .NET tool 形式發佈。

## 安裝

```bash
dotnet tool install --global Polhem.Cli
```

工具指令是 `dotnet-polhem`，所以以 `dotnet polhem` 執行。

## 指令

| 指令 | 用途 |
|------|------|
| `dotnet polhem defines materialize` | 把 `Polhem.Definition` 內嵌的預設定義檔寫到指定目錄，讓應用程式以它們為起點再客製 |
| `dotnet polhem defines list` | 列出內嵌預設定義檔的相對路徑 |
| `dotnet polhem keys protect` | 產生一把新金鑰並以主金鑰加密後印出，即 `SystemSettings.xml` 的 `SecurityKeySettings` 所存的值（例如 `ApiEncryptionKey`） |
| `dotnet polhem forms template` | 寫出描述新表單用的空白 Excel 活頁簿 |
| `dotnet polhem forms export` | 寫出既有表單的 Excel 活頁簿，包含中文文字與選單位置 |
| `dotnet polhem forms import` | 把表單活頁簿轉成可執行表單需要的 FormSchema、每張表一份的 TableSchema、FormLayout 與各項登記；`--dry-run` 只列出檔案、不寫入 |

表單活頁簿的結構，以及匯入時依慣例補上的內容，見
[用 Excel 建模表單](https://github.com/polhem-dev/polhem/blob/main/docs/zh-TW/guides/excel-form-modeling.md)。

各指令的選項由工具本身列出，以它為準：

```bash
dotnet polhem --help
dotnet polhem help defines
dotnet polhem defines materialize --help
```

## 授權

MIT，見 [repository](https://github.com/polhem-dev/polhem)。
