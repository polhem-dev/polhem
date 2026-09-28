# Polhem.Base

> 跨層共用工具程式庫：型別轉換、加密原語、序列化、集合、ADO.NET 輔助與運算式抽象。

[English](README.md)

## 架構定位

- **層級**：基礎設施層（Polhem 框架最底層）
- **在相依圖中的位置**：見[專案相依性全景圖](../../docs/zh-TW/dependency-map.md)。**此處不逐一列出** —— 權威來源是 csproj，而散落在每份套件 README 的散文拷貝會漂且無人察覺。它們確實漂了：`Polhem.Hosting` 抽出後，有四份 README 的下游數個月都沒把它補上。

## 目標框架

- `net10.0` -- 使用現代執行階段 API 與效能改進

## 主要功能

### 型別轉換與字串工具

- `ValueUtilities` -- 安全型別轉換（`CInt`、`CStr`、`CBool` 等）。時間家族（`CDateOnly`、
  `CDateTime`、`CTimeOnly`）的單參數多載回傳 nullable，雙參數多載才顯式帶預設值——無法解析的
  輸入回 `null`，而不是一個可能流進報表的 sentinel 日期。`CDateTime` 也能解析民國曆日期字串。
  `CBool` 只接受與語言無關的代碼（`1`、`T`、`TRUE`、`Y`、`YES`，不分大小寫），詳見其 XML 文件
- `FrameworkClock` -- 使用者時區的「今天」與「現在」：`Today(timeZoneId)` 與 `Now(timeZoneId)`，
  回傳 `Unspecified`（絕不回 `Local`）；時區 id 為空即代表 UTC。`Now(timeZoneId, DateTimeBasis)`
  在連線器的伺服端改用 UTC。系統時間戳記直接使用 `DateTime.UtcNow`
- `StringExtensions` / `StringUtilities` -- 字串切割、裁切與不分大小寫比較的輔助方法
- `DateTimeExtensions` -- `DateTime` 擴充方法

### 加密與安全

- `AesCbcHmacCryptor` -- AES-256-CBC 加密搭配 HMAC-SHA256 驗證（每次加密使用隨機 IV）
- `RsaCryptor` -- RSA 非對稱加密
- `PasswordHasher` -- PBKDF2-SHA256 密碼雜湊
- `ApiKeyHasher` / `AccessTokenHasher` -- API 金鑰密鑰與存取權杖在儲存前的雜湊
- `FileHashValidator` -- 透過 SHA-256 驗證檔案完整性
- `AesCbcHmacKeyGenerator` -- 加密金鑰產生器

### 序列化與壓縮

- `XmlCodec` -- 透過 `XmlSerializer` 的 XML 序列化
- `JsonCodec` -- 透過 `System.Text.Json` 的 JSON 序列化（camelCase）
- `Gzip` -- Gzip 壓縮 / 解壓縮，用於 Payload 處理

### 集合

- `CollectionBase<T>` / `KeyCollectionBase<T>` -- 框架（鍵值）集合的抽象基底類別

### 資料存取輔助

- `DataTable` / `DataSet` / `DataRow` / `DataRowView` / `DataView` 擴充方法，簡化 ADO.NET 操作
  （`DataRowViewExtensions.GetFieldValue<T>` 是 `DataRowExtensions` 在資料繫結場景的對應版本）
- `FieldDbType` 與 `DbTypeConverter` -- 資料庫型別對應工具

### 例外

- `UserMessageException` -- 給終端使用者看的訊息，帶有依使用者文化解析的鍵值與參數；
  `ForbiddenException`、`AuthenticationRequiredException` 與公司範圍相關例外同在 `Exceptions/`

### 運算式抽象

- `IExpressionEvaluator` -- 對一組具名變數求值運算式。以 DynamicExpresso 為底的實作位於
  `Polhem.Expressions`；抽象放在這裡，讓定義層與商業邏輯層消費引擎時不必相依第三方套件
  （[ADR-038](../../docs/adr/adr-038-definition-dependency-boundary.zh-TW.md)）
- `ExpressionPolicy` -- 欄位值餵進引擎前套用的共用型別／null 政策，使計算欄在伺服端與
  UI 用戶端得到相同結果
- `ExpressionEvaluationException` -- 運算式無法解析或編譯時擲出

## 主要公開 API

| 類別 / 介面 | 用途 |
|-------------|------|
| `ValueUtilities` | 安全型別轉換（含預設值） |
| `FrameworkClock` | 使用者時區的今天／現在 |
| `StringExtensions` / `StringUtilities` | 字串切割、裁切、比較 |
| `AesCbcHmacCryptor` | 認證式對稱加密 |
| `PasswordHasher` | 密碼雜湊（PBKDF2-SHA256） |
| `XmlCodec` / `JsonCodec` | XML / JSON 序列化 |
| `IObjectSerializeFile` | 綁定序列化檔案路徑的物件 |
| `IKeyObject` | 跨層鍵值實體介面 |
| `UserMessageException` | 帶可在地化鍵值的終端使用者訊息 |
| `IExpressionEvaluator` | 運算式求值抽象（實作位於 `Polhem.Expressions`） |
| `ExpressionPolicy` | 運算式變數的共用型別／null 政策 |

## 設計慣例

- **靜態工具類別** -- `ValueUtilities`、`StringUtilities`、`FileUtilities` 以靜態方法公開功能，不持有實例狀態。
- **常數時間比較** -- `CryptographicOperations.FixedTimeEquals` 用於 HMAC / 雜湊驗證，防止時序攻擊（Timing Attack）。
- **啟用 Nullable Reference Types**（`<Nullable>enable</Nullable>`）。

## 目錄結構

資料夾依功能分組原始碼：`Attributes/`、`Collections/`、`Data/`、`Exceptions/`、`Expressions/`、
`Security/`、`Serialization/`。通用工具（`ValueUtilities`、`FrameworkClock`、`StringUtilities`、
`FileUtilities`、`SysInfo`、`IKeyObject` 等）放在專案根目錄。
