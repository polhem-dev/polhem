# Polhem.AI.Claude

[繁體中文](README.zh-TW.md)

Supplies a `Microsoft.Extensions.AI` `IChatClient` backed by Anthropic's Claude models, through the official
`Anthropic` SDK. A Polhem feature that talks to a model, such as
[Polhem.Definition.Drafting](../Polhem.Definition.Drafting/README.md), knows only `IChatClient`; this package is the
Claude side of that seam, and the application joins the two.

It references no other Polhem project, so a feature never carries the Anthropic SDK and an application that uses
another vendor never loads it.

## Key Public APIs

| Type | Purpose |
|------|---------|
| `ClaudeChatClientFactory` | `Create(options)` builds a client with the SDK's own credential resolution; `Create(client, options)` wraps an `AnthropicClient` you configured yourself |
| `ClaudeChatOptions` | The model, the effort, the output limit and whether refusal fallbacks are on. The defaults and why they were chosen are in the XML docs of each property; `DefaultModel` is the model used unless you change it |
| `ClaudeCredentials` | `Describe()` reports which credential the SDK will use (`ClaudeCredentialSource`: its kind, the profile, the organization and workspace, and warnings), without reading the secret, so a tool can show it before spending credit |
| `ClaudeChatException` | A failure the person can act on, with a `ClaudeFailureKind`: the model refused, the answer reached the output limit, or the credential was not accepted |

```csharp
var source = ClaudeCredentials.Describe();
if (source.Kind == ClaudeCredentialKind.None)
    return ShowHowToSignIn();
Show($"Requests are charged to: {source}");

using IChatClient chatClient = ClaudeChatClientFactory.Create(new ClaudeChatOptions());
```

## Credentials

The package keeps no credential and asks for none. The Anthropic SDK resolves one when the first request is sent,
taking environment variables (`ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN`, a profile named by `ANTHROPIC_PROFILE`,
workload identity federation) before the profile that `ant auth login` saved. The exact order is in the XML docs of
`ClaudeCredentials`. A sign-in profile holds short-lived tokens the SDK refreshes on its own, scoped to the Claude
Console organization and workspace chosen at sign-in.

Signing in with `ant auth login` is the recommended way on a developer's machine; set `ANTHROPIC_API_KEY` where the
CLI cannot run. Requests are charged to the Console organization of the credential. On a Max, Ultra or Team plan the
monthly API credits can be bound to an organization (claude.ai → Settings → Billing → API credits). The claude.ai
sign-in that Anthropic's own apps use is not an API credential, and this package does not use it.

An environment variable wins over a signed-in profile, so a key left in a shell profile silently takes over.
`Describe()` warns about that, and about an empty `ANTHROPIC_API_KEY`, which this SDK treats as unset while the
`ant` CLI and other SDKs send it.

## What every request carries

- **Adaptive thinking and an explicit effort.** The effort is always sent, because the default differs between
  models.
- **Streaming**, even through `GetResponseAsync`, so a long answer does not run into a request timeout.
- **Strict tools.** The server checks each tool input against its schema. A few schema shapes that strict mode
  rejects (a nullable enum written as a type union, a `null` branch on an optional property) are rewritten, so a
  feature's tool schemas can stay vendor-neutral.
- **Prompt caching**: automatic caching, plus a breakpoint at the end of the first user message, where a feature
  puts its documents.
- **Server-side refusal fallbacks**, when `EnableFallbacks` is on: a request the model declines is answered by a
  fallback model. The SDK adapter drops the block that marks the switch; this client keeps it and removes the declined
  attempt, which the API requires before the next request.

A response that stops because of a refusal or the output limit, and a 401, become `ClaudeChatException` instead of a
truncated answer or a raw HTTP error. The SDK retries rate limits, overloaded servers and network failures itself;
when its retries run out, its own exception reaches the caller.

## Background

[ADR-064](../../maintainers/adr/adr-064-document-form-drafting.md) records why each model vendor is a package of its
own, and why credentials are left to the vendor's tooling. Setting up the credential for DefineEditor is described in
[Drafting Forms from Documents](../../docs/en/guides/document-form-drafting.md).

## Dependencies

Anthropic · Microsoft.Extensions.AI.Abstractions
