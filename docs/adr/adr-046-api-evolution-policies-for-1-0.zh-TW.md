<!-- source: adr/adr-046-api-evolution-policies-for-1-0.md blob: f63238dd412a2d06f23dbc2a7f849a14a7481779 -->
# ADR-046：1.0 的 API 演進政策：同步的伺服器路徑、可擴充的主機介面、行程層級的設定

[English](adr-046-api-evolution-policies-for-1-0.md)

## 狀態

**已採納（Accepted，2026-09-27）**

## 背景

Polhem 1.0 會定下公開 API 的基準。自此之後，各套件的 `PublicAPI.Shipped.txt` 記錄消費端據以編譯的介面範圍，
會破壞它的變更要等到下一個主版本。

在定下基準前對公開 API 所做的檢視，找出三個會被 1.0 原封不動凍結的形狀。每一個都可以在基準之前付出代價改掉，
也可以保留，前提是把它日後還能怎麼演進寫下來。沒有成文的政策，每一個都會讓日後的改善變成「這算不算破壞性變更」
的爭論：

- **伺服器路徑從頭到尾都是同步的。** BO 契約（`FormBusinessObject` 的 CRUD virtual 成員與 `IFormBusinessObject`）、
  repository 契約（`IDataFormRepository` 以及 `src/Polhem.Repository.Abstractions/` 裡的其他契約），以及它們發出的
  資料庫呼叫（`DbAccess.Execute`）全都會阻塞。`DbAccess` 已經有非同步 API（`src/Polhem.Db/DbAccess.Async.cs`），
  但 `Polhem.Db` 以外沒有任何框架程式碼呼叫它。`JsonRpcExecutor` 能 await 回傳 `Task` 的 BO 方法，但框架裡沒有
  任何 BO 方法是這樣。
- **主機可替換的介面很大，而且會隨框架成長。** `IDefineAccess` 與 `IDefineStorage` 對每一種定義型別都有成員，
  所以下一種定義型別需要在兩者上新增成員。依嚴格的語意化版本，每一次這樣的新增，都會破壞每一個自行實作該介面的
  主機。
- **設定放在行程層級的 static 裡。** `ApiServiceOptions`、`SysInfo` 這類類別是 static，帶有公開 setter 與
  `Initialize` 方法。[ADR-011](adr-011-di-replaces-service-locator.zh-TW.md) 以 DI 取代靜態 service locator 時，
  已經以「每個主機只寫一次」為由保留了它們。1.0 會讓這些 setter 每一個都成為永久的 API。

## 決策

### 1. 伺服器路徑在 1.0 維持同步

BO 與 repository 契約，以及其下的伺服器資料庫路徑，在 1.0 都是同步的。範圍包括 `FormBusinessObject` 的 CRUD
virtual 成員（`GetList`、`GetLookup`、`GetNewData`、`GetData`、`Save`、`Delete`，以及它們呼叫的
`protected virtual` hook）、`IFormBusinessObject`、`IDataFormRepository`，以及
`src/Polhem.Repository.Abstractions/` 裡的其他 repository 契約。

非同步的對應成員日後可以**新增在同步成員旁邊**：

- 在 `FormBusinessObject` 這類類別上，新的 virtual 成員屬於新增。既有的 override 照樣編譯、照樣執行。
- 在 `IDataFormRepository` 這類介面上，新成員適用決策 2：允許在次版本加入，衍生自框架類別的實作不必修改就會得到它。

因此加入它們不是破壞性變更，1.0 也不必現在就定下非同步的形狀。

**`DbAccess` 的非同步 API 維持公開。** 它是應用程式自行做 I/O 時可取消的路徑，例如在 BO 裡實作的報表或批次作業
（[ADR-005](adr-005-formschema-driven.zh-TW.md) 稱這條路線為 AnyCode）。1.0 裡框架自己的呼叫不使用它。

**在用戶端，`Polhem.Api.Client` 每一個公開的非同步成員都在最後接受一個 `CancellationToken`**（預設值為 `default`），並把它傳給傳輸層。
這項變更與本決策在同一批 1.0 前的改動中落地。已取消的 token 會讓用戶端停止等待，也會攔下尚未分派的呼叫。它攔不住
已經在執行的 BO 方法：分派以下的伺服器路徑不接受 token，所以即使呼叫端已經離開，同步的 BO 方法仍會執行到底。
`tests/Polhem.Api.Client.UnitTests` 的 `ClientAsyncSurfaceTests` 會掃描該套件每一個 public 與 protected 非同步成員，
以此強制這條規則；其他地方沒有 analyzer 要求這個參數，UI 套件自己的非同步成員也不在它的涵蓋範圍內。

### 2. 主機可替換的介面可以在次版本新增成員

下列介面可以在**次版本**新增成員。這不視為破壞性變更，版本說明會列出每一個新增的成員。

| 介面 | 框架實作 |
|------|----------|
| `IDefineAccess` | `CacheDefineAccess` |
| `IDefineStorage` | `FileDefineStorage`、`DbDefineStorage` |
| `ICustomizeDefineReader` | `CustomizeDefineReader`、`DbDefineStorage` |
| `ICustomizeDefineWriter` | `CustomizeDefineWriter`、`DbDefineStorage` |
| `ICacheDataSourceProvider` | `CacheDataSourceProvider` |
| `ICacheProvider` | `MemoryCacheProvider` |
| `IAccessTokenValidator` | `AccessTokenValidator` |
| `IApiEncryptionKeyProvider` | `DerivedApiEncryptionKeyProvider`、`DynamicApiEncryptionKeyProvider`、`StaticApiEncryptionKeyProvider` |
| `ISessionInfoService` | `SessionInfoService` |
| `ICompanyInfoService` | `CompanyInfoService` |
| `IRepositoryFactory` | `RepositoryFactory` |
| `IDataFormRepository` 以及 `src/Polhem.Repository.Abstractions/` 裡的其他 repository 契約 | `src/Polhem.Repository/` 裡對應的類別（`DataFormRepository` 等） |

其中多數由 `BackendComponents`（`SystemSettings.xml`）裡的型別名稱選定。自訂內容的 reader 與 writer 隨定義儲存體
一起提供，repository 契約則經由 repository factory 或 DI 註冊提供。

**消費端判斷某個介面是否適用，就是看它在不在這張表裡。** 這張表是權威來源：沒有列在這裡、也不是
`src/Polhem.Repository.Abstractions/` 裡 repository 契約的介面，依一般的語意化版本處理，要為它新增成員得等主版本。
擴充這張表本身也是一項決策，記錄在本 ADR 的〈實作演進〉段落。

**實作者應衍生自框架實作**（在它沒有 sealed 的情況下），只 override 要改的部分。這樣次版本新增的成員會帶著框架的
實作一起到來，主機照樣能編譯。從零實作這些介面、或包裝 sealed 實作的主機，則接受次版本升級可能要求它實作新成員。
哪些實作是 sealed，以型別宣告為準；本 ADR 不複寫。

### 3. 行程層級的靜態設定在 1.0 保留：一個行程一個主機

靜態設定類別在 1.0 維持現狀：`ApiServiceOptions`（`Polhem.Api.Core`）、`ApiClientInfo`（`Polhem.Api.Client`）、
`SysInfo`（`Polhem.Base`）、`CacheInfo`（`Polhem.ObjectCaching`）、`ClientInfo`（`Polhem.UI.Core`）與
`GlobalEvents`（`Polhem.Definition`）。它們的成員以各型別的宣告為準；本 ADR 不列出。

這代表：

- **支援的模型是一個行程一個主機。** 同一個行程裡設定不同的兩個主機不受支援，例如 payload 選項不同的兩個後端，
  或設定不一致的用戶端與行程內伺服器。最後寫入者對整個行程生效。
- **測試要在它們上面序列化。** 寫入這些類別之一的測試類別，與所有寫入同一個類別的測試類別共用一個 xUnit
  `[Collection]`，避免平行執行的測試類別在它上面競爭。
- **每個 session 的狀態不放這裡。** 在同一個行程裡服務多位使用者的 head，把每位使用者的狀態放在 scoped 物件裡，
  就像 Blazor head 以 `ApiSessionContext` 所做的，而不是放在這些 static 裡。

**日後改用 DI options 屬於新增。** 之後的版本可以經由 `AddPolhemFramework` 與各 head 的註冊方法註冊 options，
讓框架從 DI 讀取。屆時 static 會為了相容而保留或標記 `[Obsolete]`，只在主版本才移除。這項遷移不必現在決定，
1.0 也不會擋住它。

## 後果

- **決策 1 的已知代價：被阻塞的執行緒。** ASP.NET Core 路徑上的每一次資料庫往返都會佔住一條 thread pool 執行緒。
  在突發負載下，thread pool 超過最小值後依注入速率成長，請求在它後面排隊。這是阻塞於 I/O 的伺服器常見的延展性
  天花板，1.0 接受它。碰到的部署可以調高 thread pool 的最小值，或水平擴充。
- **決策 1 的已知代價：本機模式下呼叫端的執行緒。** BO 方法是同步的時候，`JsonRpcExecutor` 實際上從不 await，
  所以行程內的 `LocalApiProvider` 會在呼叫端的執行緒上跑完整個呼叫，包括資料庫往返。對本機模式的桌面 head 而言，
  那是 UI 執行緒；對 Blazor Server 而言，是 circuit 的執行緒。在本決策當下，`LocalApiProvider` 不會把呼叫移到別的
  執行緒，所以必須讓 UI 執行緒保持空閒的 head 要自己處理。
- **取消只做到一部分。** 用戶端可以停止等待，也可以在分派前攔下呼叫；但攔不住 BO 方法已經開始的資料庫操作。
  日後新增的非同步伺服器成員可以接受 token 來補上這個缺口，而不必改動用戶端 API，因為用戶端成員已經接受 token。
- **決策 2 改變了次版本可以包含的內容。** 遵循建議（衍生自框架實作）的主機不受影響。從零實作表中介面的主機，
  每次次版本升級都要看版本說明。
- **`ICacheProvider` 的契約仍帶著以檔案為基礎的失效機制**（`CacheItemPolicy` 可以指定要監看的檔案），分散式快取
  無法遵守。決策 2 沒有修正這一點；它只表示日後對這個介面的修改可以在次版本進行。
- **決策 3 保留了測試成本。** 寫入靜態設定的測試類別繼續在各自的 collection 裡序列執行，
  [ADR-011](adr-011-di-replaces-service-locator.zh-TW.md) 已經把這記錄為這些 static 剩下的成本。

## 考慮過的替代方案

### 在 1.0 之前把 BO 與 repository 契約改成非同步

把 `FormBusinessObject` 的 CRUD virtual 成員、`IFormBusinessObject` 與 repository 契約改成回傳 `Task`，並接到
既有的 `DbAccess` 非同步 API 上。這能消除被阻塞的執行緒，也讓伺服器能真正取消。

1.0 不採用的原因：

- 工作量很大。這項變更貫穿每一個 BO、每一個 repository、寫入管線、稽核軌跡與權限檢查，而每一個範例與應用程式的
  override 都得在同一個版本裡跟著改。
- 日後新增不是破壞性變更（決策 1 與決策 2），所以現在付出代價，買到的是提早定下形狀，而不是保留選項。
- 它消除的代價是突發負載下的吞吐量天花板。本框架鎖定的部署還沒碰到它；在碰到之前，調高 thread pool 最小值或
  水平擴充就能應付。

日後才新增的代價也在此記錄：屆時 BO 介面的每一個操作都會有同步與非同步兩個成員，以及框架該呼叫哪一個的規則。

### 為主機介面採用 default interface method 或抽象基底類別

- **所有未來成員都用 default interface method。** 新成員會在介面裡帶著實作，所以任何實作都不會壞。不採為政策：
  許多新成員沒有合理的預設（新的定義型別需要真正的儲存），而會擲出例外的預設只是把破壞從編譯期移到執行期。
  個別新成員在確實有合理預設時，仍可以使用。
- **新增抽象基底類別（例如 `DefineStorageBase`）供主機衍生。** 這會在每個介面旁邊為同一件事多出第二個型別。
  框架實作本來就能當基底類別，所以決策 2 改為指引實作者衍生自它們。

### 在 1.0 之前把靜態設定改成 DI options

經由 `AddPolhemFramework` 與各 head 的註冊方法註冊 options，並把靜態類別改為 `internal` 或只用於啟動。這能允許
一個行程多個主機，也能消除序列化的測試。

1.0 不採用的原因：這項變更會碰到每一個主機、head 與範例，而 payload 管線與快取在每個使用處都得改為經由 DI 取得
options。延後它不會造成無法挽回的損失，因為 DI 路徑日後可以新增在 static 旁邊（決策 3）。
