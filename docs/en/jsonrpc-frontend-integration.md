# JSON-RPC Frontend Integration Guide

[繁體中文](../zh-TW/jsonrpc-frontend-integration.md) · [← Docs Index](README.md)

How to call the Polhem JSON-RPC backend from a JavaScript / TypeScript frontend
(React, Vue, Angular, Svelte, or vanilla) **without any .NET on the client**.

The whole thing fits in one small module of plain JS. See the working sample at
[`samples/Web.Js.Demo/`](../../samples/Web.Js.Demo/README.md) — this guide explains
*why* it works.

---

## When to use this vs the .NET client

| Frontend stack | Client to use | Wire format |
|----------------|---------------|-------------|
| Blazor Server (in-process) | `Polhem.Api.Client` (Local) | Direct DI dispatch, no HTTP |
| Blazor WASM | `Polhem.Api.Client` (Remote) | MessagePack + AES-CBC-HMAC over HTTP |
| .NET MAUI / WPF / WinForms | `Polhem.Api.Client` (Remote) | Same as above |
| **React / Vue / Angular / vanilla JS** | **fetch + JSON-RPC** (this guide) | **Plain JSON over HTTPS** |
| TypeScript SPA / Node.js client | Same — fetch + JSON-RPC, with optional TS types | Plain JSON over HTTPS |

If the frontend can host a .NET runtime, use `Polhem.Api.Client` — it gives you typed
contracts, MessagePack throughput, and payload encryption "for free". If the
frontend is JS, this guide is the path.

Payload encryption is **not** a .NET-only capability. The body codec is negotiated per request: a JS
client that wants `Encoded` or `Encrypted` declares `"codec": "json"` on the payload envelope and the
server answers with the same codec, while a request that declares no codec is read as MessagePack —
that is what [ADR-044](../adr/adr-044-payload-codec-negotiation.md) exists for. The cross-language
artefacts for that path are [`wire-contracts/`](../../wire-contracts/README.md) (a TypeScript contract
generated from the message types, the authoritative description of every request and response shape)
and [`wire-fixtures/`](../../wire-fixtures/README.md) (golden body samples to check an implementation
against). [`polhem-connector-js`](https://github.com/polhem-dev/polhem-connector-js) is a TypeScript
client built on them.

See [ADR-013: Frontend API connection strategy](../adr/adr-013-frontend-api-connection-strategy.md)
for the broader policy.

---

## Wire format

The Polhem JSON-RPC endpoint accepts the standard
[JSON-RPC 2.0](https://www.jsonrpc.org/specification) envelope with a custom
`params` shape:

### Request

```http
POST /api HTTP/1.1
Host: your.backend
Content-Type: application/json
X-Api-Key: <api-key>
Authorization: Bearer <access-token>     // omit for anonymous calls

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

- `method` — `<ProgId>.<Action>`, dispatched to the BO by reflection
- `params.format` — `0` (`PayloadFormat.Plain`) for the plain path this guide covers. It is **not** restricted to that: since [ADR-044](../adr/adr-044-payload-codec-negotiation.md) a JS client can also use `Encoded` / `Encrypted` by declaring `"codec": "json"` on the envelope, which needs only JSON, gzip, AES-CBC-HMAC and RSA — all available in the browser
- `params.value` — your args object, with **camelCase or PascalCase property names**
  (server deserializes case-insensitive)
- `id` — any client-chosen identifier echoed back in the response

### Response — success

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

### Response — error

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

Mutually exclusive: a response carries **either** `result` **or** `error`, never both. A framework
message such as this one is translated before it leaves the server: into the session's culture, or,
for a call with no session such as a failed sign-in, into the deployment's default language.

### Why JS does not need `params.type`

The .NET client's wire format always carries `params.type` (e.g.
`"Polhem.Api.Core.Messages.System.LoginRequest, Polhem.Api.Core"`) because Encoded /
Encrypted bodies are opaque bytes. Even there the server does not take the type from the
caller: it decodes the body into the parameter type of the method the request addresses, and
refuses the call when `type` names a different type. `type` is a consistency check, not a
choice. Plain format does not need it at all:

- The target type is the parameter type of the addressed BO method, resolved by reflection
- The server reads `params.value` into that type directly; a Plain request is never decoded
  through `type`
- Polymorphic fields (e.g. `GetListArgs.Filter` → `FilterNode` subclasses)
  use their own `JsonConverter` with an inline `kind` discriminator, not the outer `type`

You may send `params.type` if you want (it's ignored on Plain); leaving it out
keeps payloads smaller. Regression coverage:
[`JsonRpcExecutorTests.Ping_PlainWith*`](../../tests/Polhem.Api.Core.UnitTests/JsonRpcExecutorTests.cs)
asserts omitted, empty, and bogus `type` values all succeed.

### Values in a Plain body

A member typed `object` on the server — a filter condition's `value` and `secondValue`, a
`parameters` entry — names no CLR type in a Plain body, so the server binds it by JSON kind: a
string stays a string, an integer that fits becomes a `long` and any other number a `decimal`, a
boolean a `bool`, and an array an `object[]` (the shape the `In` operator expects). **A date or a
`Guid` is sent as a string and stays a string**: the server does not guess types from text. A
client that needs typed values uses the JSON body codec (`"codec": "json"` with `Encoded` or
`Encrypted`), whose `[code, value]` envelope carries the type — that envelope is what
`WireValueEnvelope` in `wire-contracts/messages.d.ts` describes; a Plain body carries the bare value
instead.

Cells of a `DataTable` read by their column's `type`:

- **`Decimal` and `Int64` cells are JSON strings**, so no precision is lost to JavaScript's
  double-only numbers.
- **`Date`** is a calendar day: render it without a time of day and do not shift it through the
  browser time zone.
- **`DateTime`** is an instant and is always UTC in a response: convert it for display. A value
  sent back for saving needs no conversion, since the server does not use it; a filter value must be
  converted back to UTC before it is sent.

See [Temporal Types](temporal-types.md) and [Time Zones](datetime-timezone.md).

---

## Headers

| Header | Required | Value | Notes |
|--------|----------|-------|-------|
| `Content-Type` | Yes | `application/json` | JSON-RPC envelope |
| `X-Api-Key` | Yes, except for `System.Ping` | The application's API key | Until the deployment issues its first enabled key (`st_api_key`), any non-empty value passes; after that, only an issued, enabled, unexpired key does. See [API Key Management](api-key-management.md). |
| `Authorization` | For methods declared `Authenticated` | `Bearer <accessToken>` | The GUID from `System.Login`'s response. A request without the header is an anonymous call, which only methods declared `Anonymous` accept. |

A rejected API key, or an `Authorization` header that is not a well-formed `Bearer <guid>`, is refused
before dispatch with **HTTP 401** and a JSON-RPC error body (`-32600`). A missing, unknown or expired
token on a method that needs one reaches the method's access check and answers `-32001` with HTTP 200.

CORS must be configured on the host. The demo backend opens an `AllowAnyOrigin`
policy in [`samples/QuickStart.Server/Program.cs`](../../samples/QuickStart.Server/Program.cs);
production hosts must restrict origins explicitly.

---

## Authentication flow

```
1. POST System.Login        →  { accessToken, expiredAt, userId, userName, timeZone, culture, ... }
2. POST <Method> (with token)
   ...
3. POST System.Logout       →  {}
```

Pass `clientPublicKey: ""` to `System.Login` to skip RSA key exchange (JS Plain
path does not need an encryption key). The server returns the AccessToken
unencrypted in `result.value.accessToken`.

The token is a `Guid` string. Tokens expire at the `expiredAt` the login response carries; after
that the backend answers authenticated calls with `-32001` (`Unauthorized`) and the client must sign
in again.

Some methods require entering a company first (`System.EnterCompany`) to set
`SessionInfo.CompanyId` — this routes form CRUD to the company-specific database. A form whose
`CategoryId` is `company` called without a company answers `-32002` (`CompanyNotEntered`).
Business forms belong in `company`, and the samples' `Employee` form is no exception: every sample
client calls `EnterCompany` right after `Login`, with the one company the demo backend seeds
(`DEMO`). That form replaces the framework's own `Employee` form (`st_employee`, see
[Framework-Reserved Names](framework-reserved-names.md)), which is company-scoped too.

---

## Available methods

The complete catalog (with `[ApiAccessControl]` per method) lives at
[`docs/en/api-method-reference.md`](api-method-reference.md). Two of its columns decide what a
browser can call:

- **Protection.** A Plain call reaches only methods whose protection is `Public`. `Encoded` methods
  need an Encoded or Encrypted body, and `Encrypted` methods (the audit-log queries and API key
  management) need an Encrypted one — the JSON codec path described above. `LocalOnly` methods
  (such as `System.CreateSession` and `System.SaveDefine`) refuse every remote call, whatever the
  format.
- **Auth.** `Anonymous` methods need no token; `Authenticated` ones do.

The `Public` methods, which a Plain client can call:

| Category | Methods |
|----------|---------|
| Anonymous | `System.Ping`, `System.GetCommonConfiguration`, `System.Login`, `<ProgId>.ExecFuncAnonymous` |
| Authenticated — Session | `System.EnterCompany`, `System.LeaveCompany`, `System.Logout`, `System.GetDepartmentTree` |
| Authenticated — Definition | `System.GetDefine`, `System.GetFormSchema`, `System.GetFormLayout`, `System.GetLanguage`, `System.GetCustomizeFormLayout`, `System.GetCustomizeLanguage` |
| Authenticated — Form | `<ProgId>.GetList`, `GetLookup`, `GetNewData`, `GetData`, `Save`, `Delete`, `ExecFunc` |

**Definitions arrive as XML**

Every definition method returns the definition as stored, serialized as XML, in `result.value.xml`
— the same XML `System.GetDefine` returns for that type. Definitions travel as XML on every path
because their nested collections are get-only on the .NET side: XmlSerializer fills the existing
instance, while JSON and MessagePack bind by writability and would drop those collections.

| Method | Args | Returns |
|--------|------|---------|
| `System.GetFormSchema` | `{ progId }` | `{ xml }` — the `FormSchema`: tables, fields, db types, relations |
| `System.GetFormLayout` | `{ progId, layoutId? }` | `{ xml }` — the base-layer `FormLayout`: sections, fields, controlType, row/column spans; an empty string when none is stored |
| `System.GetLanguage` | `{ lang, namespace }` | `{ xml }` — the `LanguageResource` for one namespace × one language (`Items` + `Enums`); an empty string when none is stored |
| `System.GetDefine` | `{ defineType, keys }` | `{ xml }` — any definition type a remote caller may read: `FormSchema`, `FormLayout`, `Language`, `MenuSettings`, `CurrencySettings`, `UnitSettings`. Other types are refused. |

Parse the XML with the browser's `DOMParser`; the sample's `parseDefineXml` (and the TypeScript
wrapper below) turns it into plain objects. Nothing is localized or merged on the server: the
captions in a schema are the base text, and the tenant's customization layer is a separate call
(`System.GetCustomizeFormLayout`, `System.GetCustomizeLanguage`). To show captions in the user's
language, fetch `System.GetLanguage` with the user's culture and the form's `progId` as the
namespace, and apply its `Items` by the keys `FormSchemaLocalizer` uses (`Schema.DisplayName`,
`Table.<TableName>.DisplayName`, `Field.<FieldName>.Caption`).

A layout is authored at design time, never generated from the `FormSchema`, so JS can request
either independently. For schema-driven UI rendering, both are usually fetched together
(`GetFormSchema` for validation rules and captions, `GetFormLayout` for the UI shape). A layout
file describes structure only, so its captions are taken from the schema; an empty result means no
definition is stored, which is a configuration error rather than a cue to build one.

Method names are **case-sensitive** — `system.ping` will not dispatch.

---

## Error handling

`response.error.code` maps to [`JsonRpcErrorCode`](../../src/Polhem.Api.Core/JsonRpc/JsonRpcErrorCode.cs).
Errors raised while the method runs come with HTTP 200; the transport's own refusals (`-32700`,
`-32600`) come with a 4xx status and still carry the JSON-RPC error body.

| Code | Name | Meaning | Typical action |
|------|------|---------|----------------|
| `-32700` | `ParseError` | Malformed JSON in the request body (HTTP 400) | Fix client serialization |
| `-32600` | `InvalidRequest` | Wrong content type (HTTP 415), empty body or missing method (HTTP 400), rejected API key or malformed `Authorization` header (HTTP 401) | Inspect headers and body |
| `-32601` | `MethodNotFound` | Declared; no producer today. An unknown `progId.action` currently answers `-32000` | Check method name / casing |
| `-32602` | `InvalidParams` | Declared; no producer today. Invalid arguments currently answer `-32099` | Inspect `message` |
| `-32000` | `InternalError` | Unhandled server-side exception | Not user-facing. The message is "Internal server error" unless the server runs in debug mode |
| `-32001` | `Unauthorized` | The method needs a signed-in caller and the access token is missing, unknown or expired | Sign in again |
| `-32002` | `CompanyNotEntered` | Method needs company context | Call `System.EnterCompany` first |
| `-32003` | `CompanyAccessDenied` | The company does not exist or the user has no rights to it | Display denial, switch company |
| `-32004` | `PermissionDenied` | Authenticated but no rights for this model/action | Display denial |
| `-32005` | `ReplayRejected` | The wire frame is missing, unreadable, repeats a sequence number, or its timestamp is outside the accepted window | Retrying the same frame will not help; check the client clock |
| `-32099` | `UserMessage` | A business-rule violation (validation, domain rule) with a message written for the end user; also a refused call such as a Plain call to an `Encrypted` method or a remote call to a `LocalOnly` one, which carries a fixed generic message ("Access denied.", "The request is not valid.") while the real reason is logged on the server | Display `message` to user |

For every code except `-32000`, `message` is written for the caller and safe to surface to the
end user. For `-32000`, never display the message — log internally and show a generic "request
failed" instead.

---

## TypeScript wrapper

A TypeScript counterpart of [`samples/Web.Js.Demo/polhem-api-client.js`](../../samples/Web.Js.Demo/polhem-api-client.js)
for TS projects. Standalone, no framework — bring your own state management. The message shapes
come from [`wire-contracts/messages.d.ts`](../../wire-contracts/messages.d.ts), the generated contract,
rather than from copies written here: copy or sync that file into your project as `messages.d.ts`.

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
  // A refusal before dispatch (API key, Authorization header, unreadable body) has a 4xx
  // status and still carries a JSON-RPC error body, so read the body first.
  const data = (await res.json().catch(() => undefined)) as JsonRpcResponse<T> | undefined;
  if (data?.error) throw new RpcError(data.error.code, data.error.message, data.error.data);
  if (!res.ok || !data?.result) throw new RpcError(res.status, `HTTP ${res.status} ${res.statusText}`);
  return data.result.value;
}

// ---- Definitions: stored XML parsed into plain objects ----

export type DefineValue = string | number | boolean | DefineNode | DefineNode[];
export interface DefineNode { [name: string]: DefineValue | undefined }

// Attributes become camelCase properties, and a wrapper element (Sections, Fields, Tables,
// Items ...) becomes an array. XmlSerializer leaves out attributes that hold their default
// value, so treat every property as optional.
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
    // Repeated elements without a wrapper (a LanguageEnum's entries) collect into an array.
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

// Stubs for the common path; extend them as you consume more of the definition.
// The members of ControlType are those of Polhem.Definition.Layouts.ControlType.
export type ControlType =
  | 'Auto' | 'TextEdit' | 'ButtonEdit' | 'DateEdit' | 'YearMonthEdit' | 'DropDownEdit'
  | 'MemoEdit' | 'CheckEdit' | 'NumericEdit' | 'TimeEdit';

export interface LayoutField {
  fieldName: string;
  caption?: string;
  controlType?: ControlType;   // absent means the default, TextEdit
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

Without `paging`, `GetList` returns the first page of `PagingOptions.MaxPageSize` rows, and the
response's `paging.hasMore` says whether rows were left out. The request and response shapes of
every other method are in `wire-contracts/messages.d.ts`; regenerate rather than hand-edit them
(the file's [README](../../wire-contracts/README.md) says how). Two differences from that file apply
to a Plain body: an `object`-typed member such as a filter value is a bare JSON value rather than a
`WireValueEnvelope` (see [Values in a Plain body](#values-in-a-plain-body)), and definitions are the
`xml` strings described above.

---

## See also

- [`samples/Web.Js.Demo/README.md`](../../samples/Web.Js.Demo/README.md) — runnable demo of the calls above
- [`docs/en/api-method-reference.md`](api-method-reference.md) — full method catalog with `[ApiAccessControl]` per method
- [`wire-contracts/README.md`](../../wire-contracts/README.md) — the generated TypeScript contract and how it is kept in step with the server
- [`docs/adr/adr-013-frontend-api-connection-strategy.md`](../adr/adr-013-frontend-api-connection-strategy.md) — broader frontend connection policy
- [ADR-044](../adr/adr-044-payload-codec-negotiation.md) — per-request codec negotiation
- [`src/Polhem.Api.Core/README.md`](../../src/Polhem.Api.Core/README.md) — server-side dispatch internals
- [`src/Polhem.Api.Client/README.md`](../../src/Polhem.Api.Client/README.md) — the .NET client this guide is the JS counterpart to
