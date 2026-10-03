<!-- source: en/security/api-key-management.md blob: 4adbf7e5d4a902286ea92cf0478fbd86e476ac71 -->
# API 金鑰管理

[English](../../en/security/api-key-management.md) · [← 文件索引](../README.md)

API 金鑰回答的是**哪個應用程式在呼叫**。它不是使用者鑑別 —— 那由 Bearer access token 負責，
金鑰本身不授予任何資料存取權。兩者在每次遠端呼叫並行：`X-Api-Key` 說明**什麼**在呼叫，
`Authorization: Bearer` 說明**是誰**。

> 用戶端持有的金鑰在密碼學意義上並非機密：它隨桌面或行動應用一起出貨，可以從中還原出來。
> 請把它當作「可撤銷的應用程式身分」，不是密碼。

## 1. 閘門會自己啟用

從未發放過金鑰的部署維持啟用前的行為 —— 任何非空的 `X-Api-Key` 皆通過 —— 所以升級框架不會把
任何人擋在外面。**發出第一把啟用中的金鑰就等於關上閘門**，此後只接受已發放的金鑰。沒有開關要切。

| 狀態 | 行為 |
|---|---|
| `st_api_key` 不存在，或無任何啟用中的金鑰 | 閘門未生效：任何非空標頭皆通過。`AddPolhemApiKeyGateCheck` 會在啟動時回報 —— 在 Development 環境為警告，其他環境為錯誤。 |
| 至少一把啟用中的金鑰 | 閘門生效：標頭必須帶有效、啟用中、未過期的金鑰。 |

預設驗證器只讓一個方法免金鑰：`System.Ping`，讓健康檢查在金鑰存放讀不到時仍能回應。

拒絕的理由刻意合併為單一結果 —— 格式錯誤、查無此金鑰、已停用、已過期，對呼叫端而言不可區分，
因此無法用 API 探測哪些識別碼存在。理由只在稽核記錄中區分。

未發放（或金鑰已停用）的識別碼會以「查無」記住一分鐘，存在一個有上限的集合裡，所以拿已撤銷
金鑰不斷重試的用戶端不會每次都打到資料庫。每個不同的未知識別碼仍要付一次資料庫讀取；節流探測
流量是 API 前端速率限制的工作。發放或啟用金鑰時，這個標記會透過與其他金鑰變更相同的
cache-notify 清掉。

## 2. 發放金鑰

`SystemBusinessObject.CreateApiKey` 由伺服端產生祕密段，完整明文金鑰**只回傳一次**。伺服端只存加鹽雜湊，
框架無法再次顯示金鑰 —— 遺失就得重發一把，而那本來就是輪替流程。

```csharp
var response = await connector.CreateApiKeyAsync(
    sysId: "acme-portal",              // 小寫字母、數字與連字號
    sysName: "ACME 客戶入口",
    keyType: ApiKeyType.ThirdParty,
    contact: "ops@acme.example",       // 出事時找得到人
    expiredAt: null);                  // 或指定 UTC 到期時間

// 這個值離開伺服端的唯一時刻：
Console.WriteLine(response.ApiKey);    // "acme-portal.<secret>"
```

識別碼是金鑰本身的前段，**不是**祕密 —— 它按設計會出現在日誌與稽核記錄裡，
「這是哪個應用造成的」才回答得出來。

## 3. 誰能管理金鑰

金鑰屬於**整個部署**、不屬於任何公司，因此不由公司角色管轄，改由部署層那條軸把關：

- **遠端呼叫者必須是部署層管理員**（`st_user.deployment_admin`）。僅「已登入」不足，
  公司管理員在這裡也什麼都不是。
- **行程內（本機）呼叫免管理員。** 這是 bootstrap 路徑：尚無管理員的部署仍必須能在主機上
  鑄出第一把金鑰。

這條規則涵蓋所有金鑰管理方法：`CreateApiKey`、`ListApiKeys`、`SetApiKeyEnabled` 與
`SetApiKeyExpiry`。它們全都要求 Encrypted 呼叫，其中 `CreateApiKey`、`SetApiKeyEnabled` 與
`SetApiKeyExpiry` 另外宣告了重放防護（`ApiReplayProtection.UniqueSequence`）：部署開啟
wire frame（`AddPolhemPayload` 的 `RequireFrame`）後，這類呼叫被重放的副本會遭拒絕。這道檢查的限制見
[開發限制 § API 重放防護](../architecture/development-constraints.md#api-重放防護限制)。

部署層模型與第一位管理員的指派方式，見[權限與授權指南第三部分](permission-authorization.md)。

## 4. 輪替金鑰

輪替過程中不會有應用被鎖在外面的空窗，因為新舊兩把金鑰在中間階段同時有效。

1. **發第二把。** `sys_id` 具唯一性，新金鑰必須有自己的識別碼 —— 慣例是加後綴：
   `acme-portal` → `acme-portal-2`。兩把都啟用、都能用。
2. **用戶端逐步換過去。** 逐一更新各安裝的存放金鑰（見 §6）。流量會慢慢移轉，過程中不中斷。
3. **確認舊金鑰已無流量。** 登入、異動、檢視與 API 異常日誌都記錄 `api_key_id`，查一下即可確認
   是否還有人在用舊識別碼。
4. **停用舊金鑰。** `SetApiKeyEnabled(sysId, false)` 會撤銷它。變更透過 cache-notify 在與寫入
   相同的交易中發布，所以每個伺服器行程會在下一次輪詢（`CacheNotifyOptions.IntervalSeconds`）
   時丟棄快取副本，而不是一直接受該金鑰到快取項目過期為止。

第 4 步也可以改用 `SetApiKeyExpiry` 設定到期時間，把退役排程而不是當下執行。該方法接受已過去的
時間，等於「即刻起失效」，同時把理由留在資料列上。

> **只有一把金鑰的部署，停用它會重新打開閘門** —— 沒有任何啟用中的金鑰後，行為退回啟用前，
> 任何非空標頭又會被接受。這正是輪替要**先**發第二把的原因。切勿一路停用到零。

框架不刪除金鑰。停用的資料列讓稽核裡的 `api_key_id` 仍解析得到；刪掉它會讓歷史日誌列指向不存在
的東西。

## 5. 第三方介接

**每個第三方發一把獨立金鑰**，設 `KeyType = ApiKeyType.ThirdParty` 並填 `Contact`。
類型只是標籤、不帶授權意義，但它與聯絡人合起來才讓事件可處理 —— 你分得出是誰的金鑰出問題、
該打給誰。

第三方金鑰請設到期時間。一個安靜下來的介接，與一個仍在運作的介接從外面看沒有差別；
而活得比合作關係還久的金鑰，正是沒人記得去撤銷的那一把。

## 6. 用戶端把金鑰放在哪

用戶端透過 `IApiKeyStorage`（`Polhem.UI.Core`）讀寫金鑰，以 `ClientInfo.ApiKeyStorage` 指派。
`ClientInfo.ApplyApiKey(defaultApiKey)` 在存放為空時以應用內建值作為種子寫入，否則一律以存放值
為準，因此更換金鑰不需要重新編譯任何用戶端。

預設是 `FileEndpointStorage`：金鑰存在端點旁的 `apikey.txt`，位於每位使用者本機應用程式資料目錄下、
以應用程式命名的資料夾。這個檔案只有擁有者能讀：在 Unix 上以 `0600` 模式建立，在 Windows 上繼承
該使用者資料夾的存取清單。瀏覽器（WebAssembly）宿主沒有持久的檔案系統，會把
`ClientInfo.EndpointStorage` 與 `ClientInfo.ApiKeyStorage` 都換成以瀏覽器儲存體實作的版本；
框架並未出貨這樣的實作。

## 7. 留下什麼記錄

登入、異動、檢視與 API 異常日誌的資料列都帶 `api_key_id` 與 `api_key_name`，因此「這是哪個應用
做的」不需要 join 就答得出來。

金鑰管理操作本身也會留痕：記在異動軸、`prog_id` 為 `System`、標為敏感、帶前後值 ——
發放一把金鑰會記下識別碼、名稱、類型、聯絡人與到期時間，**絕不記錄祕密段或其雜湊**。

## 延伸閱讀

- [權限與授權指南](permission-authorization.md) —— 部署層授權模型
- [API 方法參考](../api/api-method-reference.md) —— 各金鑰管理方法與其保護等級
- [框架保留命名](../reference/framework-reserved-names.md) —— `st_api_key` 與其他框架表
