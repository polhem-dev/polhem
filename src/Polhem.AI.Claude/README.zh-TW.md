# Polhem.AI.Claude

[English](README.md)

透過官方 `Anthropic` SDK，提供以 Anthropic Claude 模型為底的 `Microsoft.Extensions.AI` `IChatClient`。
需要與模型對話的 Polhem 功能（例如 [Polhem.Definition.Drafting](../Polhem.Definition.Drafting/README.zh-TW.md)）只認
`IChatClient`；本套件是這個接縫的 Claude 那一端，由應用程式把兩邊接起來。

本套件不參考任何其他 Polhem 專案，所以功能套件從不帶著 Anthropic SDK，改用其他廠商的應用程式也不會載入它。

## 主要公開 API

| 型別 | 用途 |
|------|------|
| `ClaudeChatClientFactory` | `Create(options)` 以 SDK 自己的憑證解析建立 client；`Create(client, options)` 包裝你自行設定的 `AnthropicClient` |
| `ClaudeChatOptions` | 模型、effort、輸出上限，以及是否啟用拒答 fallback。預設值與選擇理由寫在各屬性的 XML 文件；`DefaultModel` 是未修改時使用的模型 |
| `ClaudeCredentials` | `Describe()` 回報 SDK 將使用哪個憑證（`ClaudeCredentialSource`：種類、設定檔、組織與 workspace、警告），不讀取密鑰本身，讓工具在花費額度之前就能顯示 |
| `ClaudeChatException` | 使用者能採取行動的失敗，帶有 `ClaudeFailureKind`：模型拒答、回答達到輸出上限，或憑證不被接受 |

```csharp
var source = ClaudeCredentials.Describe();
if (source.Kind == ClaudeCredentialKind.None)
    return ShowHowToSignIn();
Show($"Requests are charged to: {source}");

using IChatClient chatClient = ClaudeChatClientFactory.Create(new ClaudeChatOptions());
```

## 憑證

本套件不保存、也不索取任何憑證。Anthropic SDK 在送出第一個請求時解析憑證，先看環境變數（`ANTHROPIC_API_KEY`、
`ANTHROPIC_AUTH_TOKEN`、`ANTHROPIC_PROFILE` 指名的設定檔、workload identity federation），再看 `ant auth login`
存下的設定檔。確切順序寫在 `ClaudeCredentials` 的 XML 文件。登入設定檔存放 SDK 會自動更新的短效 token，
範圍是登入時選定的 Claude Console 組織與 workspace。

開發者的電腦建議以 `ant auth login` 登入；無法執行 CLI 的環境再設定 `ANTHROPIC_API_KEY`。請求的費用由憑證所屬的
Console 組織支付。Max、Ultra 或 Team 方案可以把每月附送的 API 額度綁定到某個組織
（claude.ai → Settings → Billing → API credits）。Anthropic 自家應用程式使用的 claude.ai 登入不是 API 憑證，
本套件不使用它。

環境變數優先於登入的設定檔，所以殘留在 shell 設定檔裡的 key 會不知不覺取而代之。`Describe()` 會對此提出警告，
也會警告空字串的 `ANTHROPIC_API_KEY`：這個 SDK 把它當作未設定，`ant` CLI 與其他 SDK 卻會把它送出。

## 每個請求都帶著什麼

- **Adaptive thinking 與明確的 effort。** effort 一律送出，因為各模型的預設值不同。
- **串流**，即使透過 `GetResponseAsync` 也一樣，長回答不會碰上請求逾時。
- **Strict 工具。** 伺服器依 schema 檢查每個工具輸入。strict 模式不接受的幾種 schema 形狀
  （以型別聯集表示的可為 null 列舉、選填屬性上的 `null` 分支）會被改寫，功能套件的工具 schema 因此可以維持與廠商無關。
- **Prompt 快取**：自動快取，再加上第一則使用者訊息結尾的斷點，功能套件把文件放在那裡。
- **伺服器端的拒答 fallback**（`EnableFallbacks` 開啟時）：模型拒答的請求改由 fallback 模型回答。
  SDK 的轉接器會丟掉標示切換點的區塊；本 client 保留它，並移除被拒的那次嘗試，這是 API 在下一個請求前的要求。

因拒答或輸出上限而停止的回應，以及 401，會轉成 `ClaudeChatException`，而不是被截斷的回答或原始的 HTTP 錯誤。
rate limit、伺服器過載與網路失敗由 SDK 自行重試；重試用盡時，SDK 自己的例外會傳到呼叫端。

## 背景

[ADR-064](../../maintainers/adr/adr-064-document-form-drafting.md) 記錄為何每家模型廠商各自一個套件，
以及為何憑證交給廠商自己的工具處理（ADR 只有英文版）。為 DefineEditor 設定憑證的方式見
[從文件草擬表單](../../docs/zh-TW/guides/document-form-drafting.md)。

## 相依性

Anthropic · Microsoft.Extensions.AI.Abstractions
