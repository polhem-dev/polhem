// Polhem JSON-RPC client for pure JavaScript browsers.
// No build step, no npm — just import as an ES module.
//
// Wire format: PayloadFormat.Plain (params.format = 0, no params.type required).
// Server uses BO method reflection to determine the target args type, so JS sends
// plain JSON objects with camelCase property names (System.Text.Json on the server
// is configured case-insensitive).

const ENDPOINT = 'http://localhost:5050/api';

// Every call except System.Ping carries an X-Api-Key, which identifies the calling
// application (the Bearer token identifies the user). Until a deployment issues its
// first key in st_api_key, any non-empty value passes, which is why this demo value
// works against QuickStart.Server. Once a key is issued, the server checks the
// value against the stored hash and this constant must hold a real key.
const API_KEY = 'quickstart-demo';

let _accessToken = null;

/** Sets the AccessToken used as Bearer for subsequent authenticated calls. */
export function setAccessToken(token) { _accessToken = token; }

/** Gets the current AccessToken, or null if not logged in. */
export function getAccessToken() { return _accessToken; }

/** Clears the AccessToken (after Logout or session expiry). */
export function clearAccessToken() { _accessToken = null; }

/**
 * JSON-RPC error surfaced to callers with the server-side error code. When the
 * server answered with an HTTP error status, `httpStatus` carries it; `code` is
 * the JSON-RPC error code from the body, or the HTTP status when the body had none.
 */
export class RpcError extends Error {
  constructor(code, message, data, httpStatus = null) {
    super(message);
    this.name = 'RpcError';
    this.code = code;
    this.data = data;
    this.httpStatus = httpStatus;
  }
}

/**
 * Calls a JSON-RPC method on the Polhem backend.
 * @param {string} method   e.g. "System.Login" or "Employee.GetList"
 * @param {object} value    The args object (camelCase property names accepted).
 * @returns {Promise<object>} The result.value payload from the server.
 */
async function rpcCall(method, value) {
  const body = {
    jsonrpc: '2.0',
    method,
    params: { format: 0, value },
    id: crypto.randomUUID(),
  };
  const headers = {
    'Content-Type': 'application/json',
    'X-Api-Key': API_KEY,
  };
  if (_accessToken) headers['Authorization'] = `Bearer ${_accessToken}`;

  const res = await fetch(ENDPOINT, {
    method: 'POST',
    headers,
    body: JSON.stringify(body),
  });
  // The server reports most failures (a rejected API key, a malformed request) as
  // an HTTP error status with a JSON-RPC error envelope in the body, so read the
  // body before falling back to the bare status line.
  const data = await readJson(res);
  if (data?.error) {
    throw new RpcError(data.error.code, data.error.message, data.error.data, res.ok ? null : res.status);
  }
  if (!res.ok) {
    throw new RpcError(res.status, `HTTP ${res.status} ${res.statusText}`, undefined, res.status);
  }
  return data?.result?.value ?? null;
}

/** Parses a response body as JSON, or returns null when it is empty or not JSON. */
async function readJson(res) {
  const text = await res.text();
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

export const systemApi = {
  /** Ping the backend; no AccessToken required. */
  ping: (traceId = crypto.randomUUID()) =>
    rpcCall('System.Ping', { clientName: 'Web.Js.Demo', traceId }),

  /**
   * Authenticate against the backend. ClientPublicKey is intentionally empty so
   * the server skips the RSA key exchange and the client stays in Plain format.
   */
  login: (userId, password) =>
    rpcCall('System.Login', { userId, password, clientPublicKey: '' }),

  /**
   * Enter the specified company. Company-scoped forms (every form in the demo)
   * refuse calls until the session has entered one. The demo backend seeds a
   * single company, "DEMO"; an ID it does not know, or one the user has no
   * access to, returns a "Company access denied" RpcError.
   */
  enterCompany: (companyId) =>
    rpcCall('System.EnterCompany', { companyId }),

  /** Leave the current company context (idempotent). */
  leaveCompany: () =>
    rpcCall('System.LeaveCompany', {}),

  /** Destroy the current session (idempotent). */
  logout: () =>
    rpcCall('System.Logout', {}),

  /**
   * Fetch the raw FormSchema definition for the given progId, parsed from the
   * XML the server returns. See parseDefineXml for why definitions travel as XML.
   */
  getFormSchema: async (progId) =>
    parseDefineXml((await rpcCall('System.GetFormSchema', { progId })).xml),

  /**
   * Fetch the raw base-layer FormLayout definition for the given progId.
   * Returns null when no layout is stored. Layouts are authored at design time,
   * so a missing one is a configuration error for the caller to report; the
   * .NET clients report it too rather than generating a layout from the schema.
   * An empty layoutId resolves to the progId server-side, matching the
   * {ProgId}.FormLayout.xml convention. This is the base layer only: a tenant
   * customization layout is a separate call (System.GetCustomizeFormLayout).
   */
  getFormLayout: async (progId, layoutId = '') =>
    parseDefineXml((await rpcCall('System.GetFormLayout', { progId, layoutId })).xml),
};

/**
 * Parses a definition XML document into the same shape the JSON wire format
 * used to produce: attributes become camelCase properties and each container
 * element (Sections, Fields, Details, Columns, Tables...) becomes an array.
 *
 * Definitions travel as XML on every API because their nested collections are
 * get-only on the .NET side: XmlSerializer populates the existing instance,
 * while JSON and MessagePack bind by writability and would silently drop those
 * collections on the way back. One wire format for every client beats a
 * JSON-shaped API that only works one way.
 *
 * @param {string} xml  The definition XML; empty means "no such definition".
 * @returns {object|null} The parsed definition, or null when xml is empty.
 */
function parseDefineXml(xml) {
  if (!xml) return null;
  const doc = new DOMParser().parseFromString(xml, 'application/xml');
  const failure = doc.querySelector('parsererror');
  if (failure) throw new Error(`Definition XML is malformed: ${failure.textContent}`);
  return elementToObject(doc.documentElement);
}

const camelCase = (name) => name.charAt(0).toLowerCase() + name.slice(1);

// Collections .NET maps with a repeated [XmlElement] have no wrapper element:
// XmlSerializer writes their items straight into the owner, one element each
// (LanguageEnum's <Entry> items, for example). These tags always become an
// array, even with a single item, so callers can iterate them unconditionally.
const REPEATED_ELEMENTS = new Set(['Entry']);

function elementToObject(el) {
  const obj = {};
  const collected = new Set();
  for (const attr of el.attributes) {
    // Skip the xsi/xsd namespace declarations XmlSerializer emits on the root.
    if (attr.name.startsWith('xmlns')) continue;
    obj[camelCase(attr.name)] = coerce(attr.value);
  }
  for (const child of el.children) {
    const key = camelCase(child.tagName);
    if (REPEATED_ELEMENTS.has(child.tagName)) {
      (obj[key] ??= []).push(elementToObject(child));
      continue;
    }
    // A wrapper element (Sections, Fields, ...) holds a list; anything else is
    // a nested object. Wrappers are recognised by having element children whose
    // tag differs from their own, which is how XmlSerializer writes XmlArray.
    const items = Array.from(child.children);
    const value = items.length > 0 && items[0].tagName !== child.tagName
      ? items.map(elementToObject)
      : elementToObject(child);
    // An unlisted tag that still repeats is collected into an array rather than
    // letting the last occurrence overwrite the others.
    if (collected.has(key)) {
      obj[key].push(value);
    } else if (key in obj) {
      obj[key] = [obj[key], value];
      collected.add(key);
    } else {
      obj[key] = value;
    }
  }
  return obj;
}

// XML carries everything as text; restore the booleans and integers the
// renderer branches on (visible, showCaption, columnCount, rowSpan...).
function coerce(value) {
  if (value === 'true') return true;
  if (value === 'false') return false;
  if (/^-?\d+$/.test(value)) return Number(value);
  return value;
}

/**
 * Builds a thin form-API wrapper for the given progId. Mirrors
 * FormApiConnector on the .NET side.
 * @param {string} progId  The FormSchema ProgId (e.g. "Employee").
 */
export function formApi(progId) {
  return {
    getList: (
      selectFields = 'sys_id,sys_name,hire_date,sys_rowid',
      filter = null,
      sortFields = null,
      paging = null,
    ) =>
      rpcCall(`${progId}.GetList`, { selectFields, filter, sortFields, paging }),

    getNewData: () =>
      rpcCall(`${progId}.GetNewData`, {}),

    getData: (rowId) =>
      rpcCall(`${progId}.GetData`, { rowId }),

    save: (dataSet) =>
      rpcCall(`${progId}.Save`, { dataSet }),

    delete: (rowId) =>
      rpcCall(`${progId}.Delete`, { rowId }),
  };
}

// Expose the endpoint constant for diagnostics / display.
export const apiEndpoint = ENDPOINT;
