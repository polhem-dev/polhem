<!-- source: en/getting-started/platform-support.md blob: ced581e2e5b25b93805f2a04f428d63f1d071b88 -->
# 平台支援

[English](../../en/getting-started/platform-support.md) · [← 文件索引](../README.md)

> Polhem 支援哪些應用程式 head、哪些發佈設定可用，以及瀏覽器或行動裝置 head 需要自行設定的項目

---

## 目錄

1. [支援的 head](#1-支援的-head)
2. [裁剪與 AOT](#2-裁剪與-aot)
3. [瀏覽器或行動裝置 head 的檢查清單](#3-瀏覽器或行動裝置-head-的檢查清單)
4. [哪些項目有驗證、在哪裡驗證](#4-哪些項目有驗證在哪裡驗證)

---

## 1. 支援的 head

所有 Polhem 套件的目標框架都是 `net10.0`。*head* 是把共用的應用程式程式碼包成某個平台 App 的專案；平台專屬的目標框架
（`net10.0-ios`、`net10.0-android`、`net10.0-browser`）屬於 head，而不是 Polhem 套件。

| Head | UI 套件 | 如何連到後端 |
|------|---------|--------------|
| 桌面（Windows、macOS、Linux） | `Polhem.UI.Avalonia` 搭配 `Avalonia.Desktop` | Remote（HTTP），或 Local（後端在同一個行程中執行） |
| 瀏覽器（WebAssembly） | `Polhem.UI.Avalonia` 搭配 `Avalonia.Browser` | Remote |
| iOS | `Polhem.UI.Avalonia` 搭配 `Avalonia.iOS` | Remote |
| Android | `Polhem.UI.Avalonia` 搭配 `Avalonia.Android` | Remote |
| Blazor Server | `Polhem.Web.Blazor.Server` | 預設 Local，或 Remote |
| 沒有 .NET 的用戶端（JavaScript、TypeScript…） | — | Remote，直接使用 JSON-RPC；見 [JSON-RPC 前端整合指引](../api/jsonrpc-frontend-integration.md) |

- **Remote** 表示 head 透過 HTTP 呼叫後端主機的 JSON-RPC 端點。**Local** 表示 head 也自行建立後端的服務
  （`AddPolhemFramework`），把產生的 provider 指定給 `ClientInfo.LocalServiceProvider`，並在行程內呼叫後端。
  Avalonia head 接受哪一種由 `ApiClientInfo.SupportedConnectTypes` 決定；Northwind 的每個 head 都設為 `Remote`。
- 四種 Avalonia head 共用同一套 UI 函式庫與同一套用戶端執行環境（`Polhem.UI.Core` 的 `ClientInfo`），每個行程只保存一位
  已登入的使用者。[`apps/Polhem.Northwind`](../../../apps/Polhem.Northwind/README.zh-TW.md) 以同一個共用 UI 專案建出這四種
  head，是下方檢查清單的參考設定。
- **Blazor Server** 的元件在 ASP.NET Core 伺服器行程中執行，因此下文的裁剪與 AOT 問題與它無關。它的 API session 是每個
  circuit 一份，而不是每個行程一份。它預設的 Local 模式會把每位瀏覽器使用者的呼叫都當成受信任的行程內呼叫（略過存取權杖與
  `LocalOnly` 檢查）：只有在網站的每位使用者都可以看到整個後端時才使用，否則請在 `AddPolhemBlazor` 中呼叫
  `UseRemoteProvider(endpoint)`。Remote 模式還需要在啟動時把應用程式的 API key 設到 `ApiClientInfo.ApiKey`，否則伺服器會以
  `401 Unauthorized` 拒絕第一個呼叫。`PolhemBlazorOptions` 的 remarks 詳細說明 Local 會略過哪些檢查，以及 Remote 為何從那裡讀取
  key；另見
  [`samples/Blazor.Server.Demo`](../../../samples/Blazor.Server.Demo/README.zh-TW.md)。

---

## 2. 裁剪與 AOT

### 支援的設定

| 發佈設定 | 是否支援 | 會發生什麼事 |
|----------|----------|--------------|
| 不裁剪 | 是 | — |
| 部分裁剪（`TrimMode=partial`）——iOS、Mac Catalyst 與 Android SDK 的預設 | 是 | Polhem 組件原樣複製，只有 SDK 組件被裁剪 |
| 完整裁剪（`TrimMode=full`；在桌面與 browser-wasm 上未指定 `TrimMode` 的 `PublishTrimmed=true` 也是、`AndroidLinkMode=Full`、`MtouchLink=Full`） | 否 | 送出的請求缺少 envelope 的 `jsonrpc` 與 `id` 成員 |
| NativeAOT（`PublishAot=true`） | 否 | JSON-RPC envelope 會序列化成空物件，每次呼叫都失敗 |
| `JsonSerializerIsReflectionEnabledByDefault=false` | 否 | 每次序列化 envelope 都會擲出例外 |

每一列的原因都相同：JSON-RPC envelope 由 System.Text.Json 透過反射序列化，沒有任何東西保留它讀取的成員，而且沒有任何 Polhem
組件標示為 `IsTrimmable` 或 `IsAotCompatible`。這些不支援的設定本身都不會在建置時失敗，所以套件額外加上一個警告。

### POLHEM9004

當某個專案直接或經由其他 Polhem 套件參考了 Polhem 套件，且設定成上述任一種不支援的方式時，`Polhem.Definition` 套件的
`buildTransitive` targets 會發出 **POLHEM9004**。訊息會指出該修改哪個設定。若要關閉它（例如某個專案已自行驗證過其設定），
請設定：

```xml
<PropertyGroup>
  <PolhemSuppressTrimSupportWarning>true</PolhemSuppressTrimSupportWarning>
</PropertyGroup>
```

POLHEM9004 是 MSBuild 警告，不是編譯器診斷。它只會送達套件使用者：以 `ProjectReference` 參考 Polhem 原始碼的專案不會匯入
`buildTransitive/`，因此不會收到警告。Polhem 診斷的完整清單見 [Analyzer 規則](../reference/analyzer-rules.md)。

### 執行期產生程式碼（iOS）

.NET for iOS SDK 在所有組態（包含 Debug）都停用動態程式碼（`Reflection.Emit`），除非 App 開啟直譯器。Android 保留 JIT。
框架自身的 wire 不需要動態程式碼：每個 wire 型別都有明確註冊的 MessagePack formatter（缺少時 `WireContractDriftTests`
會失敗），框架的值型別、Polhem 列舉與 `ParameterCollection` 都走封閉泛型路徑。

只有一條路徑需要動態程式碼：**應用程式自訂型別**的值，放在 `object` 型別的成員中（ExecFunc 參數、篩選值），經由
具名型別逃生口傳遞，也就是該型別的命名空間被應用程式加進了 `SysInfo.AllowedTypeNamespaces`。在 MessagePack 上它走非泛型
序列化器，在 iOS 上會擲出指出該型別名稱的 `NotSupportedException`。請改用框架的值型別傳送這類值（字串、數字、`Guid`、
日期、`ParameterCollection`）。

運算式引擎（`Polhem.Expressions`）在 iOS 上以直譯方式執行，但直譯器仍須為每個編譯後的運算式建立與其簽章相同的委派，
而 iOS 無法建立參數超過兩個的委派。因此 `DynamicExpressoEvaluator` 把每個運算式都編譯成只有一個 `object?[]` 參數的委派，
不是這個形狀時 `InterpretedInvokerGateTests` 會失敗。在 2026-09-28 實測發現之前，引用三個以上欄位的運算式（例如明細列的
`quantity * unit_price * (1 - discount)`）一經計算就會讓 iOS App 終止。不經過 `IExpressionEvaluator`、直接以
DynamicExpresso 求值的程式碼也有相同限制。

### 套件內附的裁剪描述檔

有兩個套件內嵌了裁剪器會自動套用的 `ILLink.Descriptors.xml`。head 不需要為它們另外準備 linker 檔。

| 套件 | 保留什麼 | 何時有作用 |
|------|----------|------------|
| `Polhem.Expressions` | 運算式能以名稱呼叫的 CoreLib 成員（`Math`、`string`、`DateOnly` 與其他公開的型別） | 在行動裝置 SDK 預設的部分裁剪下（會裁剪 CoreLib）。少了它，`Math.Round(x, 2)` 這類運算式會無法剖析，表單的即時計算也會自行關閉。`TrimmerDescriptorGateTests` 檢查它涵蓋直譯器公開的每個型別 |
| `Polhem.Definition` | `XmlSerializer` 以反射讀取的定義型別 | 只有在 `Polhem.Definition` 本身被裁剪時，也就是完整裁剪或 NativeAOT 下才有作用，而這兩者本來就不支援 |

---

## 3. 瀏覽器或行動裝置 head 的檢查清單

桌面 head 不需要這些項目。標示 **（瀏覽器）** 的項目只適用於 browser-wasm head。

1. **裁剪。** iOS 與 Android 保持 SDK 預設；不要設定 `TrimMode=full`、`AndroidLinkMode=Full` 或 `MtouchLink=Full`。
   **（瀏覽器）** 未指定 `TrimMode` 的 browser-wasm 裁剪發佈就是完整裁剪，所以請設定 `PublishTrimmed=false`（Northwind 的
   瀏覽器 head 就是如此）或 `TrimMode=partial`。
2. **（瀏覽器）保持 System.Text.Json 反射開啟。** browser-wasm 預設會關閉它，之後每次呼叫都會在請求送出前失敗：

   ```xml
   <JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>
   ```

3. **保留時區與全球化資料。** 用戶端透過 `TimeZoneInfo` 在 UTC 與使用者時區之間轉換每個時間點
   （[時區處理](../database/datetime-timezone.md)），因此缺少時區資料的建置會在顯示第一個日期時失敗。Northwind 的瀏覽器與行動裝置 head
   明確固定這兩個設定，因為這個失敗只會在裝置上出現：

   ```xml
   <InvariantGlobalization>false</InvariantGlobalization>
   <InvariantTimezone>false</InvariantTimezone>
   ```

4. **端點與 API 金鑰的儲存。** `ClientInfo.EndpointStorage` 與 `ClientInfo.ApiKeyStorage` 預設共用同一個
   `FileEndpointStorage`，寫入每位使用者本機應用程式資料目錄下的應用程式專屬資料夾。這個資料夾在桌面、iOS（位於 App 的
   沙箱容器內；會被納入裝置備份）與 Android（App 的私有資料目錄）上都可寫入，所以這些 head 維持預設即可。
   `FileEndpointStorage` 的 remarks 列出各平台實際解析出的路徑。**（瀏覽器）** 瀏覽器沒有持久的檔案系統：寫入只存在記憶體中，
   重新載入後就消失。請以瀏覽器儲存體實作 `IEndpointStorage` 與 `IApiKeyStorage`，並在呼叫 `ClientInfo.InitializeAsync` 或
   `ClientInfo.SetEndpointAsync` 之前指定這兩個屬性。Northwind 瀏覽器 head 的
   [`BrowserLocalStorageEndpointStorage`](../../../apps/Polhem.Northwind/Polhem.Northwind.Browser/Storage/BrowserLocalStorageEndpointStorage.cs)
   是以 `localStorage` 實作的可用範例。
5. **維持非同步。** 以 `ClientInfo.InitializeAsync` 連線，並以 `ClientDefineAccess` 的 `…Async` 成員載入定義。絕不要阻塞等待
   Task（`.Result`、`.Wait()`、`GetAwaiter().GetResult()`）：瀏覽器執行環境是單執行緒，在那裡阻塞會擲出
   「Cannot wait on monitors on this runtime」。
6. **對話框。** 框架自己的對話框不需要處理：`LookupDialog` 與 `RowEditDialog` 只在桌面的傳統視窗生命週期中以原生視窗開啟，
   其他情況都顯示在 top level 的覆蓋層上。你自己的對話框也需要同樣的考量：iOS 與 Android 的視窗後端在被要求建立 `Window`
   時會擲出 `NotSupportedException`。
7. **（瀏覽器）字型。** 瀏覽器沙箱沒有系統字型可以遞補。請附帶涵蓋所有顯示語言的字型；否則，例如來自 `zh-TW` 語系資源的
   中文標題會顯示成空白方框。

有兩項行為會因平台而不同，但不需要任何設定：

- API 用戶端的 `HttpClient` 在瀏覽器（fetch）、Android 與 Apple 行動平台上使用平台預設的 handler，因此這些平台的系統 proxy
  與憑證設定會生效；桌面與伺服器主機則使用 `SocketsHttpHandler`。
- 登入後，`ClientInfo` 會把伺服器為該使用者回傳的 culture 設為行程的 culture，它決定標題、框架文字，以及數字與日期的顯示。

---

## 4. 哪些項目有驗證、在哪裡驗證

- **不支援的設定**：POLHEM9004，位於 `src/Polhem.Definition/buildTransitive/Polhem.Definition.targets`。
- **在沒有動態程式碼的情況下執行**：本 repository 的 CI 會以 `DynamicCodeSupport=false`（iOS SDK 設定的開關）把用戶端
  head 會帶到的每個套件的測試專案（`Polhem.Base`、`Polhem.Definition`、`Polhem.Expressions`、`Polhem.Api.Core`、
  `Polhem.Api.Client`、`Polhem.UI.Core`、`Polhem.UI.Avalonia`）再跑一次。
- **運算式描述檔**：`tests/Polhem.Expressions.UnitTests` 中的 `TrimmerDescriptorGateTests`。
- **head 本身**：Northwind 的瀏覽器、iOS 與 Android head 是參考設定，但本 repository 的 CI 不會建置它們。修改上述任何設定後，
  要靠建置並執行該 head 來檢查。
