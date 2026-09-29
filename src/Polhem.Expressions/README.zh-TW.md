# Polhem.Expressions

[English](README.md)

框架運算式引擎以 DynamicExpresso 為底的實作。由商業邏輯層（存檔前的欄位計算與規則驗證）與
UI 用戶端（輸入時的即時預覽）共用，因此同一個計算欄位在兩邊得到相同結果。

## 主要公開 API

| 型別 | 用途 |
|------|------|
| `DynamicExpressoEvaluator` | 預設的 `IExpressionEvaluator`。解析與編譯一次，以「運算式文字 + 參數簽章」快取，之後逐列呼叫 |

## 抽象位於 `Polhem.Base`

`IExpressionEvaluator`、`ExpressionPolicy`、`ExpressionEvaluationException` 在
`Polhem.Base.Expressions`，不在本套件。這個分界讓 `Polhem.Definition` 與 `Polhem.Business` 完全不相依
DynamicExpresso——它們透過抽象消費引擎，只有組裝層（`Polhem.Hosting`，或自建 evaluator 的
UI head）才引用本套件。見 [ADR-038](../../maintainers/adr/adr-038-definition-dependency-boundary.md)。

**需要「挑一個實作」時引用本套件；只需要「接受一個實作」時引用 `Polhem.Base` 即可。**

## 時區

`Evaluate` 接收 `timeZoneId` 與 `DateTimeBasis`。`Today()` 回傳使用者時區的日曆日（`DateOnly`），
因此從其他地區建立的資料列仍以使用者自己的今天為預設。`Now()` 跟隨被求值資料集的基準：
`DateTimeBasis.UserZone`（預設，用戶端預覽）為使用者時區，`DateTimeBasis.Utc`（伺服端存檔前的計算，
儲存值為 UTC）為 UTC。`UtcNow()` 則明示 UTC。時區 id 為空即代表 UTC。見
[ADR-032](../../maintainers/adr/adr-032-datetime-timezone.md)。

## 安全性

**這不是沙箱。** 未註冊的**型別名稱**（`File`、`Assembly`、`Process`）會在解析期失敗，
但值上的成員存取是以反射解析的，而 `GetType()` 是 `object` 的公開成員——任何在範圍內的變數
都是通往反射 API 的起點。上游 DynamicExpresso 本身也聲明同樣的限制。

真正的控制點是運算式的**來源**而非解析器：運算式存在定義檔中，而寫入定義是部署期作業
（`SystemBusinessObject.SaveDefine` 宣告為 `ApiProtectionLevel.LocalOnly`）。任何讓遠端或低權限呼叫端得以提供運算式文字的改動，
都會使此處變成伺服端的遠端程式碼執行——請維持該邊界。

## AOT / trimming

`IsDynamicCodeSupported` 為 false 時，`Expression.Compile` 會退回直譯器，但它仍須建立與所編譯 lambda
簽章相同的委派，而沒有 JIT 的執行環境無法建立所有簽章：在 iOS 上，參數超過兩個的 lambda 會以
`ExecutionEngineException` 失敗；在 NativeAOT 下，含實值型別參數的簽章則沒有對應的程式碼。因此
`DynamicExpressoEvaluator` 把每個運算式都編譯成同一種委派 `Func<object?[], object?>`，兩種執行環境都能建立，
本引擎在 iOS、Android 與 WASM 上無需停用任何功能即可運作。有一項測試會在桌面上強制走直譯器，運算式若被編譯成
其他形狀就會失敗。

若你不經過 `IExpressionEvaluator`、而是直接用 DynamicExpresso 求值，你的程式碼也有相同限制：
`Lambda.Invoke` 與 `Lambda.Compile` 都會建立具型別的委派。

Trimming 則是另一回事：DynamicExpresso 以反射找出運算式指名的 `Math.*`、`string.*` 等成員。本套件內嵌
`ILLink.Descriptors.xml`，保留運算式可觸及的型別，因此經過 trim 的 head（行動端建置預設的 partial trim）
不需額外設定就能保有它們。

## 相依

`Polhem.Base`（本套件實作其中的 `IExpressionEvaluator` 抽象）· DynamicExpresso
