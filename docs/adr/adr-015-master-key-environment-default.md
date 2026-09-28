# ADR-015: `MasterKeySource` defaults to `Environment` — aligning with 12-factor "config in env"

[繁體中文](adr-015-master-key-environment-default.zh-TW.md)

## Status

Accepted (2026-05-26)

## Context

`SystemSettings.SecurityKeySettings.MasterKeySource` determines where the server reads the **master key** from (the
root key from which `ApiEncryptionKey` and `KeyEncryptionKey` are derived). Before v4.5, `Type` defaulted to `File` +
`Value=Master.key`; if that file did not exist at deployment, it was auto-generated and written under `DefinePath`.

This default worked well for early local development / single-machine deployment: it ran out of the box with no extra
configuration. But as production deployment forms evolved, the pain points grew:

| Pain point | Description |
|------|------|
| **Violates the "config in env" principle of the [12-factor app](https://12factor.net/config)** | A secret belongs to config and should be kept apart from the build artifact; storing it in a file ties the secret to a particular host's file system |
| **Friction in containerized deployment** | Docker / Kubernetes deployments need to `mount` an extra volume or `COPY` the file in a build step, breaking the image's "**stateless + immutable**" property |
| **Cost of keeping multiple instances in sync** | When scaling horizontally, N nodes must share the same `Master.key` file contents, which needs an extra secret distribution mechanism |
| **Hard to integrate with secret management tools** | Mainstream tools such as K8s Secret, HashiCorp Vault, AWS Secrets Manager and Azure Key Vault all inject secrets as env vars; none has a hook for "automatically read a file from disk" |
| **File artifacts are left on the file system** | After deployment, `Master.key` sits in the same directory as the other config files, and scan / backup processes easily include the secret by mistake |

The master key source needs to move from "**a file**" to "**an env var**", the industry consensus, while keeping the
existing `File` option for existing deployments / local development.

## Decision

**The default of `MasterKeySource.Type` changes from `File` to `Environment`; existing `Type=File` deployments are
completely unaffected.**

### Four key points

1. **The code default changes to `Environment`**

   The `MasterKeySource` ctor defaults to `Type = MasterKeySourceType.Environment` and leaves `Value` empty;
   `MasterKeyProvider` reads `POLHEM_MASTER_KEY` when an Environment source names no variable:

   - A new deployment that does not set a `<MasterKeySource>` block explicitly → reads from `$POLHEM_MASTER_KEY`
   - An existing `SystemSettings.xml` that explicitly says `<Type>File</Type>` → **unaffected**, keeps reading from
     the file
   - An existing `SystemSettings.xml` that explicitly says `<Type>Environment</Type>` → **unaffected** (already
     aligned)

2. **`MasterKeySourceType.File` stays a valid option**

   - The `File` enum value is not removed; it must stay for backward compatibility
   - For local development, anyone who does not want to set an environment variable can still mark
     `<Type>File</Type>` by hand
   - Some air-gapped deployments (secrets copied over USB, no env injection mechanism) still reasonably use `File`

3. **The sample / test bootstrap auto-sets a demo key (zero-setup experience)**

   With the env var as the default, running a sample / test would require the user to **set the environment variable
   first** before it could boot — a major regression for the first-run experience. The countermeasure:

   - `samples/Polhem.Samples.Shared/DemoBackend.cs` and `tests/Polhem.Tests.Shared/TestProcessBootstrap.cs` check
     `POLHEM_MASTER_KEY` during process bootstrap, and if it is not set, auto-set a **hardcoded demo key**
   - The `DemoMasterKey` constant lives in `DemoCredentials.cs` (the same file as `DemoCredentials.UserId="demo"`),
     and its value is a fixed Base64-encoded combined AES-CBC-HMAC key
   - `TestMasterKey` and `DemoMasterKey` are **separate constants** (not shared), so that samples and tests do not
     leak into each other

   This keeps the security level of the existing hardcoded demo (`DemoCredentials.UserId="demo"` /
   `Password="demo"`): samples / tests are demos to begin with, with the same risk as the "`demo` / `demo`"
   credentials, which is acceptable.

4. **Production must explicitly override the demo key**

   - The samples README (Chinese and English) and `samples/QuickStart.Server/README*.md` already state: "for a
     zero-setup experience the demo backend automatically injects a hardcoded master key; **a production host must
     inject `POLHEM_MASTER_KEY` with a real secret before the process starts**"
   - The nature of an encryption key is that "**a hardcoded master key = encryption that is as good as none**"
     (anyone who has the source can decrypt the payload). This rule cannot be enforced automatically at the
     framework level; it relies on deployment culture and README reminders

## Rationale

### Why not "generate a new key fully automatically" (autoCreate on every run)

Having the process **automatically generate a new key kept only in memory** when the env var is not set at startup
was evaluated:

- ✅ Leaves no file artifact
- ❌ **Keys differ across process runs**: SQLite persisted data such as `quickstart.db` is encrypted with the old key,
  and the next run cannot decrypt it with the new key
- ❌ **Keys differ across horizontally scaled instances**: N nodes each generate their own key and cannot decrypt
  each other's encrypted data

In practice "persisted data" must be encrypted and decrypted with a **stable key**, aligned across processes and
nodes. autoCreate per run breaks that premise.

### Why not "manual export" (no auto-set)

Having samples / tests **fail immediately and ask the user to export manually** when the env var is not set at startup
was evaluated:

- ✅ Production-like: users get used to the ritual of "set the environment variable before starting"
- ❌ The sample / test first-run experience regresses: every developer new to the project who clones the repo for the
  first time and runs `./test.sh` / `dotnet run --project samples/QuickStart.Server` sees an error, and has to dig
  through the README to find what to export
- ❌ "Forgot to export" keeps bothering returning developers (switching machines, a new shell session, a different
  CI runner job and so on)

Bootstrap auto-setting a demo key balances both ends: local / CI is zero-setup automatically, and production
overrides the env var through its deployment mechanism (K8s Secret, env file, Vault injection and so on) **without any
code change**.

### Why `Value` says `POLHEM_MASTER_KEY` (naming the env var explicitly) instead of being left empty

The `<Value>` in `samples/Define/SystemSettings.xml` and `tests/Define/SystemSettings.xml` explicitly says
`POLHEM_MASTER_KEY` rather than an empty string:

- ✅ Whoever reads the file **immediately** sees which env var to set, without tracing the fallback default in the
  source
- ✅ If a deployment wants a different env var name in the future (such as multi-tenant `TENANT_A_MASTER_KEY` /
  `TENANT_B_MASTER_KEY`), changing the XML is enough
- ⚠️ It duplicates "the code default is also `POLHEM_MASTER_KEY`", but explicit > implicit, and one more line is no
  real cost

### Why the framework does not enforce "production must override the demo key"

- The framework cannot reliably tell "is this production or a sample" (environment variables /
  `ASPNETCORE_ENVIRONMENT` can both be faked or forgotten)
- Even if it could, a hard throw would wrongly kill legitimate scenarios (such as staging wanting to run demo data)
- The real line of defense is the **deployment review process**: every production deploy should have a secret
  injection checklist, which has nothing to do with enforcement at the framework level

It relies on README warnings + a CHANGELOG migration note to remind users, together with the master key section of
`samples/README.md`, which clearly separates the demo and production flows.

## Alternatives considered (evaluated and rejected)

1. **Keep `File` as the default and add an `Environment` option for users to switch to**
   - Reason for rejection: users would have to know actively that "**there is this better option**" and switch
     themselves — the industry mainstream has long been env vars, and a default that does not align leaves the
     onboarding friction to every new user

2. **Remove `MasterKeySourceType.File` completely**
   - Reason for rejection: scenarios such as air-gapped deployment and local development still have reasonable uses;
     removing it violates the principle of "do not break existing deployments"

3. **autoCreate per run (generated in memory)**
   - Reason for rejection: see "Why not 'generate a new key fully automatically'"

4. **Manual export, no auto-set demo key**
   - Reason for rejection: see "Why not 'manual export'"

5. **Make the demo key "automatically generated per developer and written into user-local settings"**
   - Reason for rejection: over-engineering; a hardcoded demo key has the same risk as the hardcoded demo credentials
     (`demo` / `demo`), which is already acceptable. Per-developer generation brings extra keyring / user profile /
     cross-machine sync complexity, and is not worth it

6. **Integrate `dotnet user-secrets`**
   - Reason for rejection: `user-secrets` is an ASP.NET Core dev-time mechanism that moves the secret into a JSON file
     in the user profile, so it **is still "config in file"** and diverges from the env var path; and not every
     Polhem deployment runs inside ASP.NET Core (QuickStart.Console is pure console, and other future hosts may not be
     either)

## Consequences

### Deployment matrix

| Deployment scenario | Action |
|---------|------|
| **New deployment, no existing `SystemSettings.xml`** | `export POLHEM_MASTER_KEY=<base64>` before the host starts; the code default picks it up |
| **New deployment, own `SystemSettings.xml`** | The default template already has `<Type>Environment</Type><Value>POLHEM_MASTER_KEY</Value>` |
| **Existing deployment, `<Type>File</Type>` set explicitly** | **No action needed**; it keeps reading from the file |
| **Existing deployment that wants to migrate to the env var** | Two steps: (1) `export POLHEM_MASTER_KEY="$(cat $DEFINE_PATH/Master.key)"`; (2) change the XML to `<Type>Environment</Type>` |
| **Running samples / tests locally** | **No action needed**; the bootstrap auto-sets the demo key |
| **CI fresh checkout** | **No action needed**; the bootstrap auto-sets the demo key (fresh every time, no leftover file artifact) |

### Change in the security model

| Dimension | Before (File default) | After (Environment default) |
|------|------------------|--------------------------|
| Where the secret is stored | Host file system (`$DefinePath/Master.key`) | Host process environment variable |
| Secret inside the image / build artifact | Possible (if COPYed in) | No (the env var is injected by deployment) |
| Secret in backups / logs | Risk: the file backup process may include it by mistake | Risk: the env var may be read through `ps auxe` / `/proc/<pid>/environ` (needs host-level protection) |
| Container deployment | Needs a mounted volume or a build step | Directly `--env-file` / `-e POLHEM_MASTER_KEY=...` |
| Multi-instance sync | Needs file distribution | The same env var is injected into every node by the secret store |
| Secret rotation | Change the file → sync every node → restart | Change the secret store → restart pods (the cloud-native standard flow) |

Each approach has its own attack surface. **Aligning the env var path with industry secret management tools** is the
main motivation, not being "absolutely more secure".

### Zero-cost commitment to existing deployments

- Existing `<Type>File</Type>` deployments: **no change needed at all**
- Existing `Master.key` files: can keep being used, no need to delete them
- The read logic of `MasterKeyProvider`: both the File and Environment branches were already implemented, so nothing
  breaks
- The CHANGELOG already spells out the migration steps and the "no action needed" scenarios

### Marked as breaking in the CHANGELOG

Under strict SemVer, "**a default change that affects new deployments**" is a breaking change (even if existing
deployments are unaffected). `CHANGELOG.md` v4.6.0 already marks it **breaking** with migration guidance; under the
pre-stable policy the version is released as a minor (4.6.0), consistent with v4.4 / v4.5.

## Related

- `src/Polhem.Definition/Settings/SystemSettings/MasterKeySource.cs` — the config model and the ctor default
- `src/Polhem.Definition/Security/MasterKeyProvider.cs` — the read logic of the File / Environment branches
- `samples/Polhem.Samples.Shared/DemoCredentials.cs` — the hardcoded `DemoMasterKey` constant
- `tests/Polhem.Tests.Shared/TestProcessBootstrap.cs` — the `TestMasterKey` constant and the bootstrap auto-set logic
- [samples/README.md](../../samples/README.md) — the master key section, separating demo from production
- [12-factor app: Config](https://12factor.net/config) — the industry principle aligned with

## Out of scope

- **Integration templates for cloud secret managers (K8s Secret / Vault / AWS Secrets Manager helpers)** — a separate
  plan depending on actual future production deployment needs
- **Secret rotation flow** — the process restart / hot-reload mechanism after the env var changes currently relies on
  deployment tools; the framework does not get involved
- **Multi-tenant / per-tenant master key** — the framework's single master key model has not reached the per-tenant
  stage; if needed in the future, a new ADR will be opened
- **Timeline for retiring `MasterKeySourceType.File`** — maintained for the near term; the removal date depends on how
  production deployments migrate
- **A second level of indirection for the `Value` contents (such as `$POLHEM_MASTER_KEY_FILE` pointing to a file path,
  which is then read)** — for scenarios where a secret management tool produces a short-lived file; deployment tools
  injecting the env var directly already covers this, so no second level is introduced

## Implementation evolution

An ADR records the design at the time of the decision. The following are later changes, for readers comparing
with the current code:

- **2026-09-27: Auto-creating a missing master key is an explicit opt-in.** `AddPolhemFramework(configuration,
  pathOptions)` fails when the master key is missing; only the overload with `autoCreateMasterKey: true` creates one
  (`src/Polhem.Hosting/PolhemFrameworkServiceCollectionExtensions.cs`,
  `src/Polhem.Definition/Security/MasterKeyProvider.cs`). For a File source it creates the key file with owner-only permissions in the same step; for an Environment source it
  generates a key and sets it on the current process environment only, which is the per-run key rejected above, so
  it suits only hosts whose encrypted data does not outlive the process. The sample and test bootstraps pass `true`
  but set the demo or test key before, so they read the fixed key.
- **2026-09-27: Migration hint for the Bee.NET variable.** When `POLHEM_MASTER_KEY` is missing but `BEE_MASTER_KEY`
  (the Bee.NET default) is set, the error says so and names the two ways to fix it: rename the variable, or name it in
  the `MasterKeySource` of `SystemSettings.xml`.
- **2026-09-28: The migration hint is removed before 1.0.0.** Bee.NET compatibility is not a goal of 1.0.0, so the
  error for a missing variable names only the variable that was looked up.
