# External evidence on pagination (prior art)

**This is not a pitfall log**, so it is not in `gotchas/`. It is **external evidence for a design decision**: a look at
how other ERPs and frameworks handle pagination, used to check that polhem's decision "deep pagination is not
handled" is not laziness.

The decision itself, the measurements and the scope it does not cover are all in the entry
"Deep pagination: the cost of `OFFSET` grows with the page number, and none of the four escapes it" of
[`gotchas/database.md`](gotchas/database.md). **This file does not copy them**; it holds only the external
comparison.

> **This is the state found on 2026-09-08, not a continuously maintained comparison.** Each product evolves; confirm
> that it still holds before citing it next time. The limits of the verification are at the end of the file; read
> them too.

## All four default to offset

| | Default | Is keyset available? |
|---|---|---|
| Odoo | offset | **No** (the concept does not exist in `query.py`) |
| SAP RAP | offset | **No** (`IF_RAP_QUERY_PAGING` can only provide an offset) |
| SAP CAP | offset | Yes, **service-side** opt-in (`reliablePaging`) |
| Microsoft ASP.NET OData | offset (the documentation says "By default, we will use `$skip`") | Yes, **per-route** opt-in |

For Odoo and RAP it is not even a "default": **there is no second option**. The two that have keyset both make it
opt-in, and in both the motive is consistency, not deep-page latency (see "What it means for polhem" below). What
really avoids deep paging is the design of the **UI layer**, not an algorithm in the framework layer.

**Odoo: exactly like us today.** It is the closest analogue to this framework (ORM + generic list view + jumping to
any page):

- The end of `select()` in `odoo/tools/query.py` is just `LIMIT %s` + `OFFSET %s`, and `_read_group` in `models.py`
  is the same; neither that file nor `models.py` contains keyset / cursor / seek.
- Its pager (`addons/web/static/src/core/pager/pager.js`) **is not just previous/next**: clicking the numbers lets you
  type a `min[,max]` range, which is parsed and clamped into an offset, so it allows jumping directly to any depth.

**SAP: keeps users from getting there by design, not by keyset.** Look at it in three layers:

- **UI layer**: the SAPUI5 documentation states that a responsive table shows no more than 200 rows at a time, and
  that more (up to 1000) should use the growing feature and "make sure the user can filter the data". The Fiori
  elements list report defaults to a growing table (a `More` button), which **can only append downwards and has no
  page numbers**; the main interaction is the filter bar at the top.
- **The query API of RAP (ABAP, OData V4) is offset-based.** `IF_RAP_QUERY_PAGING` provides `get_page_size()` and
  **`get_offset()`**, and the example in SAP's own openSAP course material is
  `DATA(top) = io_request->get_paging( )->get_page_size( ).` /
  `DATA(skip) = io_request->get_paging( )->get_offset( ).`, which then map to SQL's `UP TO n ROWS` and `OFFSET`.
  RAP does add a next link with `$skiptoken` when the client does not page properly (without `$top` it cuts at 100
  rows by default, and `$top` has a hard limit of 5000), but that is **the framework's capping mechanism**; the API
  underneath can still only provide an offset.
- **And the UI5 client simply sends `$skip` / `$top`.** In openui5#3487 someone argued that this was "server side
  paging not properly implemented". The UI5 maintainers replied that OData is a stateless protocol, that what the
  specification requires is `$top` with a stable ordering (when `$orderby` is not given, the service must apply a
  stable order itself), and that server-driven paging is not meant to introduce state. **The issue was closed as
  completed.** So when a Fiori grid table's scroll bar is dragged deep, what it sends is a large `$skip`.

> **"The caller chooses which kind" is a misreading; here it is untangled.** Two axes are often mixed up:
>
> | Axis | Options | Who decides |
> |---|---|---|
> | **A. Who computes the page boundary** | client-driven (sends its own `$skip`/`$top`) vs server-driven (follows `@odata.nextLink`) | **The caller** |
> | **B. How the server continues** | offset arithmetic vs keyset predicate | **Service configuration** |
>
> "Two kinds of paging" refers to axis B, while what the caller can choose is axis A. **RAP has no option at all on
> axis B**: both paths lead to the same `get_offset()`; it is the same thing in two coats. CAP has an option on
> axis B, but the switch is on the service side, and the client cannot ask for "keyset this time".
>
> Also, `get_offset()` is a parameter the **service implementer** receives in their own `select()`, not a method the
> caller calls; on the caller's side there are only the URL and query options.

**Where SAP really has keyset is CAP, and it is a service-side option.** CAP's **Reliable Pagination**
(`cds.query.limit.reliablePaging`, `.enabled` in Java) **generates the skip token from "the values of the last row of
the page"**; that is genuine keyset. But three things must be read together:

1. **It is off by default**, and limited to OData V4 endpoints.
2. **It solves consistency, not deep-page latency.** The official wording is that a numeric skip token "can result in
   duplicate or missing rows if the entity set is modified between the calls".
3. **The restrictions are significant**: `$orderby` cannot use the results of functions or arithmetic expressions,
   elements must be of simple types, if there is a `$select` then every `$orderby` element must be included in it,
   and complex result set concatenation is not supported.

Incidentally, SAP's own ABAP keyword documentation on `SELECT ... OFFSET` says only that it "must be used with
`ORDER BY`" and mentions strict syntax checks; **it says nothing about performance**.

> **RAP and CAP are not two versions of the same thing; they are two independent implementations in two stacks.**
> That is why they give different answers on pagination, not because SAP changed its mind. RAP is **ABAP**, runs on
> the ABAP Platform (inside S/4HANA or the BTP ABAP Environment), and is used to modernize existing ABAP logic and to
> access core data directly; CAP is **Node.js / Java**, runs on BTP, and is used for new cloud applications and
> cross-system integration. Both produce OData services and both drive Fiori elements front ends.
>
> The structural difference is visible: CAP's generic service provider **builds the query itself**, so keyset can be a
> runtime switch; RAP's unmanaged query **hands `top` / `skip` to the service implementer to write the SELECT**, and
> the framework has no place to intervene. **But this causal link is only a plausible reading, not a verified
> explanation**: RAP also has a managed scenario where the framework does everything, and that path was not checked.
>
> **Naming trap**: both sides say "CDS", but **ABAP CDS** (the DDL in the ABAP Dictionary) and **CAP CDS / CDL**
> (`.cds` files, compiled to CSN) **are two different languages** that share only the name and a conceptual lineage.
> When you find "CDS view" in your research, first confirm which side it is about.

## What it means for polhem (two points)

**First**, the answer of mature ERPs to "deep paging is slow" is **not to let users get there**, rather than to make
`OFFSET` fast. The maintainer's remark that "people usually set query conditions and then look at shallow pages" is a
**judgement** here and a **hard UI guideline** at SAP. If a UI is ever built, growing / `More` is closer to this
conclusion than jumping by page number, and it **does not require touching `PagingOptions`**.

**Second, and more worth remembering: the industry's motive for keyset is "consistency", not "faster deep pages".**
CAP and Microsoft's ASP.NET OData both say explicitly that replacing the numeric offset is meant to avoid duplicate or
missing rows when the data changes between two calls. **That is a different problem from the one we measured**: our
stage B evaluated only latency.
If this decision is ever reopened, **consistency will be a stronger reason than latency**, and this framework has the
same exposure: `PagingOptions.IncludeTotalCount` defaults to `false` and `HasMore` is probed with `PageSize + 1`, and
that design has no protection against "data changing while paging" either. **This has not been tested, and there is
no conclusion.**

**If keyset is ever adopted**, the shape of `$skiptoken` is worth copying: **the server issues an opaque continuation
token in the response**, and the caller sends it back unchanged. That way the wire contract does not have to change
when the underlying mechanism switches from offset to keyset, whereas the current `PagingOptions` is page-number
based, and switching would be a breaking change.

## Limits of the verification

> **Do not treat this as settled**: the three points on Odoo were confirmed by reading the 17.0 source (code moves;
> confirm the paths still exist before matching them against a newer version); SAP's UI guidelines, CAP's Reliable
> Pagination, RAP's `get_offset()` and the openSAP example, and the UI5 maintainers' reply in openui5#3487 all come
> from official or SAP's own sources.
>
> **This section was corrected once**: an earlier version said "`$skiptoken` is in practice used as an offset" and
> cited a community tutorial, and it nearly **misattributed a description to SAP**: the claim that "the skiptoken
> consists of the orderby values plus the key of the last row" comes from **Microsoft's ASP.NET OData WebAPI** (and
> even there it is a per-route option, with `$skip` still the default), not from SAP. When checking framework
> behavior, **first confirm who the subject of the sentence is**.
>
> **The technical facts in the RAP vs CAP passage** (language, runtime, modeling language, the difference between the
> two CDS) are backed by capire and the official ABAP documentation, but **"when to choose which" comes mainly from
> community write-ups**: no single official SAP decision document was found (the closest is a community blog post
> written by SAP staff, and that site always returned 403, so the original was not read).
>
> Three things are still unchecked: the `$skiptoken` semantics that each SAP Gateway (OData V2) service defines for
> itself (a per-service implementation decision by nature, so there is no single answer); RAP's **managed** scenario
> (the path where the framework does everything); and **what happens when CAP has `reliablePaging` on and the client
> still sends `$skip`**. The official documentation says only that "clients must not interpret or modify the value of
> nextLink", and says nothing on this point.
