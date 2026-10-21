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
| `create` | 建立 DatabaseSettings 中還不存在的資料庫；SystemSettings 指定的主金鑰檔不存在時一併建立 |
| `check` | 逐一連線 DatabaseSettings 中的每個資料庫 |
| `upgrade` | 建立或升級 DbCategorySettings 登記的資料表；`--table` 只處理指定的表，`--dry-run` 只列出陳述式 |
| `seed-admin` | 建立第一位管理者、一家公司，以及兩者之間的權限 |

這些指令與應用程式 Server 以 `dotnet run -- db <指令>` 執行的相同，結束碼等細節見
[初始化新部署](https://github.com/polhem-dev/polhem/blob/main/docs/zh-TW/database/database-settings-guide.md#7-初始化新部署)。
工具讀取定義資料夾的方式與 Server 相同，包括主金鑰。主金鑰的環境變數沒有設定時，只有 SystemSettings 沒有任何加密金鑰
才能執行，否則會指出要設定哪個變數。主金鑰檔不存在時，只有 `create` 會建立，方式與 Server 相同：產生新金鑰，寫入只有擁有者能讀的
檔案，並輸出檔案路徑（不會印出金鑰本身）。其他指令遇到缺少金鑰檔會失敗，避免路徑打錯時多留下一把金鑰。

使用相對路徑的 SQLite 項目會被拒絕：相對路徑以開啟它的程序的工作目錄解析，對應用程式而言是 Server 的目錄，工具會用到
另一個檔案。請改用絕對路徑，或在 Server 專案執行 `db` 子指令。

工具會重新散佈資料庫驅動，各驅動的授權列在 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
