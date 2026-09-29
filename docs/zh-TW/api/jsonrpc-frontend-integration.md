<!-- source: en/api/jsonrpc-frontend-integration.md blob: 32748148be71c9168853b8c86be9e6e6ffef8a83 -->
# JSON-RPC 前端整合指引

[English](../../en/api/jsonrpc-frontend-integration.md) · [← 文件索引](../README.md)

如何從 JavaScript / TypeScript 前端（React、Vue、Angular、Svelte、vanilla）
呼叫 Polhem 的 JSON-RPC 後端，**client 端完全不需要 .NET**。

現成的 client 是 TypeScript 套件 [`polhem-connector-js`](https://github.com/polhem-dev/polhem-connector-js)；
本文檔解釋它所用的 wire，給想知道它「為什麼這樣寫」或需要自己寫 client 的人。

---

## 何時用本指引 vs .NET client

| 前端類型 | 用哪個 client | Wire format |
|----------|-------------|-------------|
| Blazor Server（in-process） | `Polhem.Api.Client`（Local） | 直接 DI 派遣，不走 HTTP |
| Blazor WASM | `Polhem.Api.Client`（Remote） | HTTP + MessagePack + AES-CBC-HMAC |
| .NET MAUI / WPF / WinForms | `Polhem.Api.Client`（Remote） | 同上 |
| **React / Vue / Angular / vanilla JS** | **fetch + JSON-RPC**（本指引） | **HTTPS + 純 JSON** |
| TypeScript SPA / Node.js client | 同上 — fetch + JSON-RPC，可加 TS 型別 | HTTPS + 純 JSON |

如果前端能跑 .NET runtime，請用 `Polhem.Api.Client` — 你會免費拿到強型別契約、
MessagePack 效能、payload 加密。如果前端是 JS，走本指引。

payload 加密**不是 .NET 專屬能力**。body codec 是逐請求協商的：JS 用戶端要用 `Encoded` 或
`Encrypted`，只需在 payload 信封宣告 `"codec": "json"`，伺服端會以同一個 codec 回應；未宣告 codec 的
請求則以 MessagePack 解讀 —— 那正是 [ADR-044](../../../maintainers/adr/adr-044-payload-codec-negotiation.md)
存在的理由。這條路徑的跨語言素材是 [`wire-contracts/`](../../../wire-contracts/README.md)
（由訊息型別產生的 TypeScript 合約，是每個請求與回應形狀的權威描述）與
[`wire-fixtures/`](../../../wire-fixtures/README.md)（可拿來對照自家實作的 golden body 樣本）。
[`polhem-connector-js`](https://github.com/polhem-dev/polhem-connector-js) 是建立在它們之上的
TypeScript client。

整體策略見 [ADR-013：前端 API 連線策略](../../../maintainers/adr/adr-013-frontend-api-connection-strategy.md)。

---

## Wire format

Polhem 的 JSON-RPC endpoint 接受標準
[JSON-RPC 2.0](https://www.jsonrpc.org/specification) 信封，配上自訂的
`params` 結構：

### Request

```http
POST /api HTTP/1.1
Host: your.backend
Content-Type: application/json
X-Api-Key: <api-key>
Authorization: Bearer <access-token>     // 匿名呼叫可省略

{
  "jsonrpc": "2.0",
  "method": "System.Login",
  "params": {
    "format": 0,
    "value": { "userId": "demo", "password": "demo", "clientPublicKey": "" }
  },
  "id": "<uuid>"
}
```

- `method` — `<ProgId>.<Action>`，server 用 reflection 派遣到對應 BO
- `params.format` — 本指引涵蓋的 plain 路徑用 `0`（`PayloadFormat.Plain`）。**並非只能如此**：自 [ADR-044](../../../maintainers/adr/adr-044-payload-codec-negotiation.md) 起，JS 用戶端只要在信封宣告 `"codec": "json"` 就能走 `Encoded` / `Encrypted`，所需的 JSON、gzip、AES-CBC-HMAC 與 RSA 瀏覽器全都有
- `params.value` — args 物件，**camelCase 或 PascalCase 屬性名都可以**
  （server 反序列化 case-insensitive）
- `id` — client 任選的識別字串，response 會原樣回傳

### Response — 成功

```json
{
  "jsonrpc": "2.0",
  "method": "System.Login",
  "result": {
    "format": 0,
    "value": {
      "accessToken": "f32bcd07-be16-44b9-be4a-db2bc669a6c2",
      "expiredAt": "2026-05-25T15:53:47.408399Z",
      "apiEncryptionKey": "",
      "userId": "demo",
      "userName": "Demo User",
      "timeZone": "Asia/Taipei",
      "culture": "en-US"
    },
    "type": ""
  },
  "id": "<echoed>"
}
```

### Response — 錯誤

```json
{
  "jsonrpc": "2.0",
  "method": "System.Login",
  "error": {
    "code": -32099,
    "message": "Invalid username or password.",
    "data": null
  },
  "id": "<echoed>"
}
```

互斥：response 一定**只有** `result` 或 `error`，不會兩者並存。像這樣的框架訊息在離開伺服端前會先翻譯：
翻成 session 的語系；沒有 session 的呼叫（例如登入失敗）則翻成部署的預設語系。

### 為什麼 JS 不需要 `params.type`

.NET client 的 wire format 永遠帶 `params.type`（例如
`"Polhem.Api.Core.Messages.System.LoginRequest, Polhem.Api.Core"`），因為
Encoded / Encrypted 的 body 是不透明的位元組。即使在那條路徑上，伺服端也不採用呼叫端給的型別：
它把 body 解碼成請求所指方法的參數型別，`type` 指名的若是別的型別就拒絕呼叫。`type` 只是一致性檢查，
不是選擇權。Plain 格式則完全不需要它：

- 目標型別是所指 BO 方法的參數型別，靠 reflection 解析
- Server 直接把 `params.value` 讀成該型別；Plain 請求從不經由 `type` 解碼
- 多型欄位（例如 `GetListArgs.Filter` → `FilterNode` 多個 subclass）由
  自訂 `JsonConverter` 以 inline 的 `kind` discriminator 處理，不依賴外層 `type`

你想送 `params.type` 也可以（Plain 路徑會忽略），省略則 payload 較小。
回歸保障：[`JsonRpcExecutorTests.Ping_PlainWith*`](../../../tests/Polhem.Api.Core.UnitTests/JsonRpcExecutorTests.cs)
驗證了省略 / 空字串 / 帶錯誤型別字串三種情境都會成功。

### Plain body 裡的值

伺服端型別為 `object` 的成員 —— 過濾條件的 `value` 與 `secondValue`、`parameters` 的項目 —— 在 Plain body
裡沒有指名 CLR 型別，因此伺服端依 JSON 種類綁定：字串仍是字串，放得下的整數成為 `long`、其他數字成為
`decimal`，布林值成為 `bool`，陣列成為 `object[]`（`In` 運算子要的形狀）。**日期或 `Guid` 以字串送出，
也維持字串**：伺服端不會從文字猜型別。需要帶型別的值時，client 改用 JSON body codec（`"codec": "json"`
搭配 `Encoded` 或 `Encrypted`），它的 `[code, value]` 信封會帶上型別 —— `wire-contracts/messages.d.ts`
裡的 `WireValueEnvelope` 描述的正是這個信封；Plain body 帶的則是裸值。

`DataTable` 的儲存格依其欄位的 `type` 解讀：

- **`Decimal` 與 `Int64` 儲存格是 JSON 字串**，以免在 JavaScript 只有 double 的數字裡失去精度。
- **`Date`** 表示日曆日：不顯示時刻，且不要透過瀏覽器時區位移。
- **`DateTime`** 為時間點，回應裡一律是 UTC：顯示前需自行換算。存檔送出時不必換算，伺服端不採用其中的值；
  過濾條件的值則要換回 UTC 再送出。

見 [時間型別總覽](../database/temporal-types.md) 與 [時區處理](../database/datetime-timezone.md)。

---

## Headers

| Header | 必要 | 值 | 說明 |
|--------|------|-----|------|
| `Content-Type` | 是 | `application/json` | JSON-RPC 信封 |
| `X-Api-Key` | 是，`System.Ping` 除外 | 應用程式的 API 金鑰 | 部署發出第一把啟用中的金鑰（`st_api_key`）之前，任何非空值都會通過；之後只有已發出、啟用中且未到期的金鑰才會通過。見 [API 金鑰管理](../security/api-key-management.md)。 |
| `Authorization` | 宣告為 `Authenticated` 的方法 | `Bearer <accessToken>` | 從 `System.Login` 回應拿到的 GUID。不帶此 header 的請求即為匿名呼叫，只有宣告為 `Anonymous` 的方法會接受。 |

金鑰被拒，或 `Authorization` header 不是格式正確的 `Bearer <guid>`，會在派遣前以 **HTTP 401** 拒絕，
body 仍是 JSON-RPC 錯誤（`-32600`）。需要 token 的方法若 token 缺漏、未知或已過期，會進到方法的存取檢查，
以 HTTP 200 回 `-32001`。

Host 必須設好 CORS。Demo 後端在
[`samples/QuickStart.Server/Program.cs`](../../../samples/QuickStart.Server/Program.cs)
開了 `AllowAnyOrigin` 政策；production host 必須明確限制 origin。

---

## 認證流程

```
1. POST System.Login        →  { accessToken, expiredAt, userId, userName, timeZone, culture, ... }
2. POST <Method>（帶 token）
   ...
3. POST System.Logout       →  {}
```

`System.Login` 的 `clientPublicKey` 傳空字串，server 會跳過 RSA key exchange
（JS Plain 路徑不需要加密金鑰），AccessToken 直接以明文回在
`result.value.accessToken`。

Token 是 `Guid` 字串。Token 在登入回應所帶的 `expiredAt` 時間到期；之後 backend 會以 `-32001`
（`Unauthorized`）回應 authenticated 呼叫，client 必須重新登入。

某些方法需要先進入公司（`System.EnterCompany`）以設定 `SessionInfo.CompanyId`，
讓 form CRUD 路由到公司專屬資料庫。`CategoryId` 為 `company` 的表單若在未進公司時呼叫，會回 `-32002`
（`CompanyNotEntered`）。業務表單應放在 `company`，範例的 `Staff` 表單（`ft_staff`）也不例外：每個範例
client 都在 `Login` 之後緊接著呼叫 `EnterCompany`，進入 demo 後端種下的唯一一間公司（`DEMO`）。範例把它命名為
`Staff` 而不是 `Employee`，因為框架把 `Employee` 保留給自己的表單（`st_employee`，見
[框架保留命名](../reference/framework-reserved-names.md)）；應用程式的表單要用自己的 progId。

---

## 可呼叫的方法

完整方法清單（含每方法 `[ApiAccessControl]` 設定）見
[`docs/zh-TW/api/api-method-reference.md`](api-method-reference.md)。其中兩個欄位決定瀏覽器能呼叫什麼：

- **Protection。** Plain 呼叫只能到達保護等級為 `Public` 的方法。`Encoded` 方法需要 Encoded 或
  Encrypted body，`Encrypted` 方法（稽核記錄查詢與 API 金鑰管理）需要 Encrypted body —— 也就是上述的
  JSON codec 路徑。`LocalOnly` 方法（如 `System.CreateSession`、`System.SaveDefine`）不論格式，
  一律拒絕遠端呼叫。
- **Auth。** `Anonymous` 方法不需要 token；`Authenticated` 方法需要。

Plain client 可呼叫的 `Public` 方法：

| 類別 | 方法 |
|------|------|
| Anonymous | `System.Ping`、`System.GetCommonConfiguration`、`System.Login`、`<ProgId>.ExecFuncAnonymous` |
| Authenticated — Session | `System.EnterCompany`、`System.LeaveCompany`、`System.Logout`、`System.GetDepartmentTree` |
| Authenticated — Definition | `System.GetDefine`、`System.GetFormSchema`、`System.GetFormLayout`、`System.GetLanguage`、`System.GetCustomizeFormLayout`、`System.GetCustomizeLanguage` |
| Authenticated — Form | `<ProgId>.GetList`、`GetLookup`、`GetNewData`、`GetData`、`Save`、`Delete`、`ExecFunc` |

**定義以 XML 傳回**

每個定義方法都以 XML 回傳存檔原樣的定義，放在 `result.value.xml` —— 與 `System.GetDefine` 對該型別回傳的
XML 相同。定義在每條路徑上都以 XML 傳輸，因為它們的巢狀集合在 .NET 端是 get-only：XmlSerializer 會填入
既有的實體，JSON 與 MessagePack 則依可寫性綁定，會把這些集合丟掉。

| 方法 | Args | 回傳 |
|------|------|------|
| `System.GetFormSchema` | `{ progId }` | `{ xml }` — `FormSchema`：tables、fields、DB 型別、relations |
| `System.GetFormLayout` | `{ progId, layoutId? }` | `{ xml }` — base 層的 `FormLayout`：sections、fields、controlType、行列 span；未存檔時為空字串 |
| `System.GetLanguage` | `{ lang, namespace }` | `{ xml }` — 單一 namespace × 單一語系的 `LanguageResource`（`Items` + `Enums`）；未存檔時為空字串 |
| `System.GetDefine` | `{ defineType, keys }` | `{ xml }` — 遠端呼叫者可讀的任一定義型別：`FormSchema`、`FormLayout`、`Language`、`MenuSettings`、`CurrencySettings`、`UnitSettings`。其他型別一律拒絕。 |

用瀏覽器的 `DOMParser` 解析 XML；範例的 `parseDefineXml`（以及下方的 TypeScript wrapper）會把它轉成一般物件。
伺服端不做在地化也不做合併：schema 裡的標題是基底文字，租戶的客製層則是另外的呼叫
（`System.GetCustomizeFormLayout`、`System.GetCustomizeLanguage`）。要以使用者的語言顯示標題，
以使用者的語系與表單的 `progId`（作為 namespace）呼叫 `System.GetLanguage`，再依 `FormSchemaLocalizer`
使用的 key（`Schema.DisplayName`、`Table.<TableName>.DisplayName`、`Field.<FieldName>.Caption`）套用其 `Items`。

版面於設計階段產出，**不會**由 `FormSchema` 推導，JS 可獨立呼叫任一個。schema-driven UI 渲染通常會兩者都拿
（`GetFormSchema` 拿驗證規則與欄位標題、`GetFormLayout` 拿 UI 結構）。版面檔只描述結構，標題取自 schema；
回空值代表沒有存檔的定義，那屬設定錯誤，不是「自己組一份」的訊號。

方法名稱**大小寫敏感** — `system.ping` 不會派遣到 `System.Ping`。

---

## 錯誤處理

`response.error.code` 對應 [`JsonRpcErrorCode`](../../../src/Polhem.Api.Core/JsonRpc/JsonRpcErrorCode.cs)。
方法執行期間產生的錯誤以 HTTP 200 回應；傳輸層自己的拒絕（`-32700`、`-32600`）以 4xx 狀態回應，
body 仍是 JSON-RPC 錯誤。

| Code | Name | 意義 | 對應動作 |
|------|------|------|---------|
| `-32700` | `ParseError` | request body 不是合法 JSON（HTTP 400） | 修 client 序列化 |
| `-32600` | `InvalidRequest` | content type 錯誤（HTTP 415）、body 為空或缺 method（HTTP 400）、金鑰被拒或 `Authorization` header 格式錯誤（HTTP 401） | 檢查 headers 與 body |
| `-32601` | `MethodNotFound` | `progId.action` 的 action 部分不對應業務物件公開為 action 的任何方法；訊息固定為「Method not found.」 | 檢查方法名稱 / 大小寫 |
| `-32602` | `InvalidParams` | `Plain` 本文無法讀成方法所接受的型別；訊息固定為「Invalid params.」。讀得進來但內容不合法的參數回 `-32099` | 修正 `params` 的結構 |
| `-32000` | `InternalError` | 未處理的 server 端例外 | 訊息不適合對使用者顯示。除非伺服端在 debug 模式，訊息一律為「Internal server error」 |
| `-32001` | `Unauthorized` | 方法需要已登入的呼叫者，而 access token 缺漏、未知或已過期 | 重新登入 |
| `-32002` | `CompanyNotEntered` | 方法需要公司 context | 先呼叫 `System.EnterCompany` |
| `-32003` | `CompanyAccessDenied` | 公司不存在，或使用者沒有此公司權限 | 顯示拒絕、切換公司 |
| `-32004` | `PermissionDenied` | 已登入但對此 model/action 無權限 | 顯示拒絕 |
| `-32005` | `ReplayRejected` | wire frame 缺漏、無法讀取、序號重複，或時間戳超出容許區間 | 重送同一個 frame 無用；檢查 client 時鐘 |
| `-32099` | `UserMessage` | 違反業務規則（驗證、領域規則），訊息是寫給終端使用者的；被拒的呼叫也會回這個碼，例如以 Plain 呼叫 `Encrypted` 方法、或遠端呼叫 `LocalOnly` 方法，這時帶的是固定的通用訊息（「Access denied.」、「The request is not valid.」），真正原因記在伺服端 log | 直接把 `message` 顯示給使用者 |

除 `-32000` 外，每個碼的 `message` 都是寫給呼叫端的，可以顯示給終端使用者。
`-32000` 絕對不要直接顯示 — 內部 log 記下，UI 顯示通用「請求失敗」訊息。

---

## TypeScript wrapper

給不使用 `polhem-connector-js` 的專案的最小 TypeScript wrapper，可直接複製到 TS 專案。獨立檔案、無框架依賴，state 管理請自行接。
訊息形狀取自產生出來的合約 [`wire-contracts/messages.d.ts`](../../../wire-contracts/messages.d.ts)，
而不是在這裡另寫一份：把該檔複製或同步到專案中，命名為 `messages.d.ts`。

```typescript
// polhem-api-client.ts
import type {
  DeleteResponse, GetDataResponse, GetFormLayoutResponse, GetFormSchemaResponse,
  GetLanguageResponse, GetListResponse, GetNewDataResponse, LoginResponse,
  PagingOptions, SaveResponse, DataSet,
} from './messages';

const ENDPOINT = '/api';
const API_KEY = 'your-api-key';

let _accessToken: string | null = null;

export const setAccessToken = (t: string | null) => { _accessToken = t; };
export const getAccessToken = () => _accessToken;

export class RpcError extends Error {
  constructor(public code: number, message: string, public data?: unknown) {
    super(message);
    this.name = 'RpcError';
  }
}

interface JsonRpcResponse<T> {
  jsonrpc: '2.0';
  method: string;
  result?: { format: number; value: T; type: string };
  error?: { code: number; message: string; data?: unknown };
  id: string;
}

async function rpcCall<T>(method: string, value: object): Promise<T> {
  const body = {
    jsonrpc: '2.0',
    method,
    params: { format: 0, value },
    id: crypto.randomUUID(),
  };
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    'X-Api-Key': API_KEY,
  };
  if (_accessToken) headers.Authorization = `Bearer ${_accessToken}`;

  const res = await fetch(ENDPOINT, { method: 'POST', headers, body: JSON.stringify(body) });
  // 派遣前的拒絕（API 金鑰、Authorization header、無法解析的 body）帶 4xx 狀態，
  // body 仍是 JSON-RPC 錯誤，所以先讀 body。
  const data = (await res.json().catch(() => undefined)) as JsonRpcResponse<T> | undefined;
  if (data?.error) throw new RpcError(data.error.code, data.error.message, data.error.data);
  if (!res.ok || !data?.result) throw new RpcError(res.status, `HTTP ${res.status} ${res.statusText}`);
  return data.result.value;
}

// ---- 定義：把存檔原樣的 XML 解析成一般物件 ----

export type DefineValue = string | number | boolean | DefineNode | DefineNode[];
export interface DefineNode { [name: string]: DefineValue | undefined }

// 屬性轉成 camelCase 的 property，包裝元素（Sections、Fields、Tables、Items…）轉成陣列。
// XmlSerializer 會省略值為預設值的屬性，因此每個 property 都當作 optional。
export function parseDefineXml(xml: string | undefined): DefineNode | null {
  if (!xml) return null;
  const doc = new DOMParser().parseFromString(xml, 'application/xml');
  const failure = doc.querySelector('parsererror');
  if (failure) throw new Error(`Definition XML is malformed: ${failure.textContent}`);
  return elementToObject(doc.documentElement);
}

const camelCase = (name: string) => name.charAt(0).toLowerCase() + name.slice(1);

function elementToObject(el: Element): DefineNode {
  const obj: DefineNode = {};
  for (const attr of Array.from(el.attributes)) {
    if (attr.name.startsWith('xmlns')) continue;
    obj[camelCase(attr.name)] = coerce(attr.value);
  }
  for (const child of Array.from(el.children)) {
    const items = Array.from(child.children);
    const isWrapper = child.attributes.length === 0 && items.length > 0
      && items.every(item => item.tagName !== child.tagName);
    const key = camelCase(child.tagName);
    const value = isWrapper ? items.map(elementToObject) : elementToObject(child);
    const existing = obj[key];
    // 沒有包裝元素的重複元素（LanguageEnum 的 entry）收集成陣列。
    if (existing === undefined) obj[key] = value;
    else if (Array.isArray(existing) && !isWrapper) existing.push(value as DefineNode);
    else obj[key] = [existing as DefineNode, value as DefineNode];
  }
  return obj;
}

function coerce(value: string): string | number | boolean {
  if (value === 'true') return true;
  if (value === 'false') return false;
  if (/^-?\d+$/.test(value)) return Number(value);
  return value;
}

// 此處的 stub 涵蓋常見路徑；用到定義的更多部分時再擴充。
// ControlType 的成員即 Polhem.Definition.Layouts.ControlType 的成員。
export type ControlType =
  | 'Auto' | 'TextEdit' | 'ButtonEdit' | 'DateEdit' | 'YearMonthEdit' | 'DropDownEdit'
  | 'MemoEdit' | 'CheckEdit' | 'NumericEdit' | 'TimeEdit' | 'DateTimeEdit';

export interface LayoutField {
  fieldName: string;
  caption?: string;
  controlType?: ControlType;   // 省略即預設值 TextEdit
  rowSpan?: number;
  columnSpan?: number;
  visible?: boolean;
}

export interface LayoutSection {
  name: string;
  caption?: string;
  showCaption?: boolean;
  fields?: LayoutField[];
}

export interface LayoutGrid {
  tableName: string;
  caption?: string;
  allowActions?: string;
  columns?: Array<{ fieldName: string; caption?: string; controlType?: ControlType; visible?: boolean }>;
}

export interface FormLayout {
  layoutId: string;
  progId: string;
  caption?: string;
  columnCount?: number;
  sections?: LayoutSection[];
  details?: LayoutGrid[];
}

export interface FormSchema {
  progId: string;
  displayName?: string;
  categoryId?: string;
  listFields?: string;
  tables?: Array<{ tableName: string; displayName?: string; fields?: DefineNode[] }>;
}

export interface LanguageResource {
  namespace: string;
  lang: string;
  items?: Array<{ key: string; value: string }>;
  enums?: DefineNode[];
}

// ---- API surface ----

export const systemApi = {
  ping: () => rpcCall<unknown>('System.Ping', { clientName: 'app', traceId: crypto.randomUUID() }),
  login: (userId: string, password: string) =>
    rpcCall<LoginResponse>('System.Login', { userId, password, clientPublicKey: '' }),
  enterCompany: (companyId: string) =>
    rpcCall<unknown>('System.EnterCompany', { companyId }),
  logout: () => rpcCall<unknown>('System.Logout', {}),
  getFormSchema: async (progId: string) =>
    parseDefineXml((await rpcCall<GetFormSchemaResponse>('System.GetFormSchema', { progId })).xml) as FormSchema | null,
  getFormLayout: async (progId: string, layoutId = '') =>
    parseDefineXml((await rpcCall<GetFormLayoutResponse>('System.GetFormLayout', { progId, layoutId })).xml) as FormLayout | null,
  getLanguage: async (lang: string, namespace: string) =>
    parseDefineXml((await rpcCall<GetLanguageResponse>('System.GetLanguage', { lang, namespace })).xml) as LanguageResource | null,
};

export const formApi = (progId: string) => ({
  getList: (selectFields = 'sys_id,sys_name,sys_rowid', paging: PagingOptions | null = null) =>
    rpcCall<GetListResponse>(`${progId}.GetList`,
      { selectFields, filter: null, sortFields: null, paging }),
  getNewData: () =>
    rpcCall<GetNewDataResponse>(`${progId}.GetNewData`, {}),
  getData: (rowId: string) =>
    rpcCall<GetDataResponse>(`${progId}.GetData`, { rowId }),
  save: (dataSet: DataSet) =>
    rpcCall<SaveResponse>(`${progId}.Save`, { dataSet }),
  delete: (rowId: string) =>
    rpcCall<DeleteResponse>(`${progId}.Delete`, { rowId }),
});
```

未帶 `paging` 時，`GetList` 回傳第一頁、筆數為 `PagingOptions.MaxPageSize`，回應的 `paging.hasMore`
表示是否還有未回傳的列。其他方法的請求與回應形狀都在 `wire-contracts/messages.d.ts`；請重新產生而不要手改
（做法見該檔的 [README](../../../wire-contracts/README.md)）。Plain body 與該檔有兩處不同：型別為 `object` 的成員
（例如過濾條件的值）是裸的 JSON 值而非 `WireValueEnvelope`（見 [Plain body 裡的值](#plain-body-裡的值)），
而定義是上文所述的 `xml` 字串。

---

## 相關連結

- [`polhem-connector-js`](https://github.com/polhem-dev/polhem-connector-js) — 建立在這套 wire 上的 TypeScript client
- [`docs/zh-TW/api/api-method-reference.md`](api-method-reference.md) — 完整方法清單含每方法 `[ApiAccessControl]` 設定
- [`wire-contracts/README.md`](../../../wire-contracts/README.md) — 產生出來的 TypeScript 合約，以及它如何與伺服端保持一致
- [`maintainers/adr/adr-013-frontend-api-connection-strategy.md`](../../../maintainers/adr/adr-013-frontend-api-connection-strategy.md) — 前端連線策略全景
- [ADR-044](../../../maintainers/adr/adr-044-payload-codec-negotiation.md) — 逐請求的 codec 協商
- [`src/Polhem.Api.Core/README.md`](../../../src/Polhem.Api.Core/README.zh-TW.md) — server 端派遣內部細節
- [`src/Polhem.Api.Client/README.md`](../../../src/Polhem.Api.Client/README.zh-TW.md) — 本指引對應的 .NET client
