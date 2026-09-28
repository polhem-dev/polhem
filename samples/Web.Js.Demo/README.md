# Web.Js.Demo

**English** | [繁體中文](README.zh-TW.md)

Demonstrates calling the Polhem JSON-RPC API from pure JavaScript in a browser —
no `npm`, no build step, no framework. The JS frontend uses
`PayloadFormat.Plain` (see [ADR-014](../../docs/adr/adr-014-jsonrpc-plain-public-default.md)),
so all requests are plain JSON.

Every method it calls is declared `Public`, which is what lets a Plain request
reach it. The page also renders a form from the FormSchema / FormLayout
definitions:

| Section | Methods |
|---------|---------|
| Login | `System.Login` |
| Ping | `System.Ping` (no auth) |
| Enter Company | `System.EnterCompany` / `System.LeaveCompany` (required before Staff CRUD; an unknown ID shows the error path) |
| Staff CRUD | `Staff.GetList` / `GetData` / `GetNewData` / `Save` / `Delete` |
| FormDefinition-driven rendering | `System.GetFormSchema` / `System.GetFormLayout` → definition XML → dynamic form |
| Logout | `System.Logout` |

## How to run

1. Start the backend (in another terminal):

   ```sh
   cd samples/QuickStart.Server
   dotnet run
   ```

   The server listens on `http://localhost:5050` and has CORS enabled for any
   origin (demo-only — production hosts must restrict origins).

2. Open the demo in a browser. Two options:

   - **Direct file open** — `open samples/Web.Js.Demo/index.html` (Mac) or
     equivalent on Windows / Linux. Modern browsers accept `file://` as an
     origin against an `AllowAnyOrigin` CORS policy.

   - **Static file server** — useful if your browser blocks `file://` for
     `fetch`:

     ```sh
     # Python 3
     cd samples/Web.Js.Demo
     python3 -m http.server 8080
     # or, if installed
     dotnet serve -p 8080
     ```

     Then open `http://localhost:8080/index.html`.

3. Click **Login** (default credentials `demo` / `demo`), then **EnterCompany**
   (the company ID is pre-filled with `DEMO`), then **GetList** — each result
   shows up in the output panel at the bottom. The Staff form is
   company-scoped, so the CRUD and form buttons fail until the session has
   entered the company.

## Files

| File | Purpose |
|------|---------|
| `index.html` | Minimal UI — Login form, CRUD buttons, dynamic form area, result panel. Vanilla CSS, no external dependencies. |
| `polhem-api-client.js` | ES module: exports `systemApi.*`, `formApi(progId)`, `RpcError` and the access token helpers. Every call goes through the module-private `rpcCall(method, value)`, which reads an HTTP error response for its JSON-RPC error before falling back to the status line, and the definition XML is parsed into plain objects here. |
| `form-renderer.js` | ES module: takes the parsed `FormLayout` object and produces a working HTML form (CSS Grid, control-type dispatch). Exposes `bindDataSet` / `collectDataSet` for two-way data binding. |
| `app.js` | UI event wiring; depends on `polhem-api-client.js` and `form-renderer.js`. |
| `.smoke.yaml` | Config for the `demo-smoke` skill — launches both prerequisite servers and verifies the page loads with the expected section text. See file header for the browser-tier limitation (clicks blocked → load-only smoke). |

## Headers used

| Header | Value | Notes |
|--------|-------|-------|
| `Content-Type` | `application/json` | JSON-RPC body |
| `X-Api-Key` | `quickstart-demo` (hard-coded) | Identifies the calling application. The default `ApiAuthorizationValidator` accepts any non-empty value only while the deployment has issued no API key; once a key is issued, the value must be that key. Production deployments issue keys. |
| `Authorization` | `Bearer <accessToken>` | Sent only after `Login` returns an AccessToken. Required for any method whose `[ApiAccessControl]` is `Authenticated`. |

## FormDefinition-driven rendering

Section 5 in the UI demonstrates the end-to-end path for schema-driven
forms — the typical workflow a React / Vue / Angular app would build on top of.
Definitions travel as an XML string (the `xml` member of the response), which
`polhem-api-client.js` parses into plain objects; the data travels as JSON:

```
GetFormSchema + GetFormLayout  (parallel) → definition XML → parsed FormLayout
        ↓
renderFormLayout(layout, container)        ← produces a working HTML form
        ↓
GetNewData / GetData                        → DataSet JSON
        ↓
controller.bindDataSet(dataSet)             ← fills the form
        ↓
controller.collectDataSet()                 ← extracts form state, marks RowState=Modified
        ↓
Save                                        → server returns refreshed DataSet
```

**Design notes:**

- **No client-side validation.** The renderer trusts `Save` to fail with an
  `RpcError` and surfaces the message in the output panel. A real app may add a
  small validation layer driven by `FormSchema` field metadata (`allowNull`,
  `maxLength` etc.) — out of scope for this demo.
- **Detail tables are read-only.** Renders `LayoutGrid` as a plain `<table>`;
  adding / editing / deleting detail rows would need a more elaborate UI.
- **CSS Grid for layout.** `LayoutField.rowSpan` / `columnSpan` map directly to
  `grid-row: span N` / `grid-column: span N`, so multi-column forms work
  without a layout library.

**Porting to React / Vue / Angular:**

- Replace `renderFormLayout`'s direct DOM construction with your framework's
  component tree (a `<FormLayout layout={…} />` component that maps each
  section/field to a child component).
- Keep the control-type → component map (`TextEdit` → `<TextField>`, etc.) —
  this is the bit that varies per framework.
- Reuse `bindDataSet` / `collectDataSet` as pure-data helpers; they have no
  DOM dependency apart from the field-name → control lookup, which becomes a
  ref or state-binding in framework land.

## Why pure JavaScript

The whole point of opening up the Plain wire format is so JS frontends can talk
to the Polhem backend without dragging in `Microsoft.JSInterop`, MessagePack,
or the AES-CBC-HMAC pipeline. Keeping the demo dependency-free demonstrates the
minimum surface a real JS framework integration (React / Vue / Angular) needs to
build on.

If your project already uses a TypeScript toolchain, look at
[`polhem-connector-js`](https://github.com/polhem-dev/polhem-connector-js), the
TypeScript client, instead of porting this file. Its types come from the
declarations generated in [`wire-contracts/`](../../wire-contracts/README.md), so
they follow the server's message types.

## Related

- Integration guide: [docs/en/jsonrpc-frontend-integration.md](../../docs/en/jsonrpc-frontend-integration.md)
- Backend host: [samples/QuickStart.Server](../QuickStart.Server/)
- Demo credentials: [samples/Polhem.Samples.Shared/DemoCredentials.cs](../Polhem.Samples.Shared/DemoCredentials.cs)
