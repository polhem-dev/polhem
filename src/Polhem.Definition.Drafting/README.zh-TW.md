# Polhem.Definition.Drafting

[English](README.md)

透過與 AI 模型對話，從紙本文件（PDF、Word 文件或照片）草擬 Polhem 表單。
模型讀取文件、草擬資料表與欄位，把「挑選一筆資料」的欄位對到定義資料夾裡既有的表單，並詢問缺少的資訊。
結果是一份 `FormSchema`，連同 zh-TW 文字與選單位置，可直接交給匯入流程，例如 DefineEditor 的匯入。

這是設計期套件，供定義工具使用。它依賴 `Microsoft.Extensions.AI.Abstractions`，從不依賴任何模型廠商的 SDK：
應用程式傳入一個 `IChatClient`，可以來自 [Polhem.AI.Claude](../Polhem.AI.Claude/README.zh-TW.md) 這類提供者套件，
或任何其他實作。

## 主要公開 API

| 型別 | 用途 |
|------|------|
| `DocumentSource` | 把 PDF、影像與 Word 文件讀成第一則訊息的內容。Word 文件轉成 Markdown 文字（段落與表格），其他照原樣送出。`SupportedExtensions` 列出副檔名；`MaxDocumentBytes` 與 `MaxImageBytes` 是大小上限 |
| `FormDraftSession` | 對話本身：`StartAsync` 送出文件，`SendAsync` 送出使用者的回覆，`ApplyUserEdit` 記錄使用者直接做的修改。發出 `DraftChanged` 與 `AssistantMessage`；草稿通過所有檢查時 `IsReady` 變為 true |
| `FormDraft` | 模型編輯的草稿：資料表、欄位、選項、關聯與選單位置。每個欄位保留它在文件中的原文 |
| `FormDraftConverter` | 以確定性的方式把草稿轉成 `FormDraftResult`。未完成的草稿也能轉換，未定事項列為問題，ProgId 未定前使用 `PlaceholderProgId` |
| `FormDraftResult` | `FormSchema`、依 `FormSchemaLocalizer` 讀取方式命名鍵值的 zh-TW 文字、選單位置、各欄位的原文，以及問題清單 |
| `IFormDraftingHost` | 應用程式要提供的部分：既有表單的目錄、某張表單的欄位，以及結果的驗證 |
| `FormDraftingOptions` | 每次請求帶的對話選項、一輪中允許的工具呼叫輪數，以及低於多少信心度的關聯必須由使用者確認 |

## 對話怎麼進行

```csharp
var documents = DocumentSource.FromFiles(paths);   // 讀取任何檔案前先檢查所有大小
var session = new FormDraftSession(chatClient, host);
session.DraftChanged += (_, _) => Preview(FormDraftConverter.Convert(session.Draft));
session.AssistantMessage += (_, e) => Show(e.Text);

await session.StartAsync(documents, cancellationToken);
await session.SendAsync("Use LeaveRequest as the ProgId.", cancellationToken);

if (session.IsReady)
    Import(FormDraftConverter.Convert(session.Draft));
```

- **模型從不撰寫定義。** 它透過 session 宣告的工具修改草稿，型別、列舉值與名稱由轉換器決定。
  工具只做宣告，所以包在外層的 `FunctionInvokingChatClient` 執行不了它們；由 session 執行每一次呼叫、檢查輸入，
  不合法的輸入以錯誤結果回給模型，而不是中斷對話。
- **就緒代表已檢查。** 模型的 `mark_ready` 只有在下列條件都成立時才成功：草稿轉換無錯誤、宿主的 `Validate`
  沒有回報錯誤、ProgId 已設定、主表有欄位，且信心度低於 `RelationConfirmationThreshold` 的關聯都已確認。
  之後任何修改都會清除 `IsReady`。
- **失敗或取消的一輪不留痕跡。** 草稿、`IsReady` 與對話歷史都回到這一輪之前的狀態。同一時間只跑一輪；
  一輪進行中呼叫 `SendAsync` 或 `ApplyUserEdit` 會擲出 `InvalidOperationException`。
- **慣例由宿主負責。** 草稿只給關聯欄位相關表單的 ProgId。系統欄位、`ft_` 資料表名稱、`company` 類別，
  以及關聯的 `ref_*` 欄位，都在宿主套用框架的表單慣例時補上，與活頁簿匯入相同。

## 宿主

之所以有 `IFormDraftingHost`，是因為匯入時的檢查放在工具裡，不在套件裡。宿主回傳它定義資料夾中的表單
（`GetCatalog`、`GetFields`），讓模型對應關聯，並以自己匯入時的方式驗證結果（`Validate`）。
DefineEditor 的宿主會套用慣例、跑一次匯入的試算（dry run），並解析每個值運算式，
所以只有在匯入會成功時，草稿才會就緒。

## 資料與費用

每次呼叫都會把文件與對話送到 `IChatClient` 所連接的對象，代管的模型會為此收費。呼叫 `StartAsync` 之前先徵求使用者同意，
並說明文件會送到哪裡。套件本身不儲存任何東西，除了透過交給它的 client 之外也不送出任何東西。

## 背景

[ADR-064](../../maintainers/adr/adr-064-document-form-drafting.md) 記錄為何讓模型編輯草稿而不是撰寫 XML、
為何功能只認 `IChatClient`，以及結果如何接上 [ADR-056](../../maintainers/adr/adr-056-form-workbooks.md)
的活頁簿匯入（ADR 只有英文版）。在 DefineEditor 中使用的方式見[從文件草擬表單](../../docs/zh-TW/guides/document-form-drafting.md)。

## 相依性

`Polhem.Definition` · Microsoft.Extensions.AI.Abstractions · DocumentFormat.OpenXml
