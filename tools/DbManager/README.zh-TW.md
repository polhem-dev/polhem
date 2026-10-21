# Polhem.DbManager

[English](README.md)

[Polhem](https://github.com/polhem-dev/polhem) 框架的資料庫管理工具，以 .NET tool 形式發佈。依定義資料夾的描述建立、檢查與升級
資料庫，支援 SQL Server、PostgreSQL、MySQL、Oracle 與 SQLite，不需要應用程式專案。DefineEditor 的資料庫相關工作就是交給它執行。

## 執行

```bash
dotnet tool install --global Polhem.DbManager
polhem-db check --define ./Define
```

或不安裝、以指定版本執行一次：

```bash
dotnet tool exec Polhem.DbManager@<版號> -- upgrade --define ./Define
```

`--define` 指定定義資料夾，也就是放 `SystemSettings.xml` 的那一層；沒給時使用目前目錄。指令與選項見 `polhem-db --help`。

| 指令 | 作用 |
|------|------|
| `create` | 建立 DatabaseSettings 中還不存在的資料庫 |
| `check` | 逐一連線 DatabaseSettings 中的每個資料庫 |
| `upgrade` | 建立或升級 DbCategorySettings 登記的資料表；`--table` 只處理指定的表，`--dry-run` 只列出陳述式 |
| `seed-admin` | 建立第一位管理者、一家公司，以及兩者之間的權限 |

這些指令與應用程式 Server 以 `dotnet run -- db <指令>` 執行的相同，結束碼等細節見
[初始化新部署](https://github.com/polhem-dev/polhem/blob/main/docs/zh-TW/database/database-settings-guide.md#7-初始化新部署)。
工具讀取定義資料夾的方式與 Server 相同，包括主金鑰：來源是環境變數而沒有金鑰時，只為這次執行產生金鑰，DatabaseSettings
沒有加密密碼時就夠用；不會建立金鑰檔。

工具會重新散佈資料庫驅動，各驅動的授權列在 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
