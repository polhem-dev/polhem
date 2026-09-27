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
| `dotnet polhem defines split-menu` | 把仍帶著選單的 `ProgramSettings.xml` 拆成扁平的程式清單加上 `MenuSettings.xml` |
| `dotnet polhem keys protect` | 產生一把新金鑰並以主金鑰加密後印出，即 `SystemSettings.xml` 的 `SecurityKeySettings` 所存的值（例如 `ApiEncryptionKey`） |

各指令的選項由工具本身列出，以它為準：

```bash
dotnet polhem --help
dotnet polhem help defines
dotnet polhem defines split-menu --help
```

## 授權

MIT，見 [repository](https://github.com/polhem-dev/polhem)。
