# ADR-045：共同維護的內容一律英文；計畫不進 repo

[English](adr-045-language-policy-and-local-plans.md) | **繁體中文**

## 狀態

**已採納（Accepted，2026-09-26）**

## 背景

Polhem 以新名稱延續 Bee.NET 框架（`jeff377/bee-library`），放在預計由多人共同維護的組織下。Bee.NET 由一個人
維護，有三個習慣會妨礙共同維護：

- **語言。** 註解、測試描述、commit message、`.claude/` 底下給 agent 的指引與維運文件都用中文，範圍更廣的貢獻者
  看不懂。另一方面，框架的使用者裡有仰賴繁體中文文件的讀者。
- **計畫放在 repo 裡。** 計畫 commit 在 `docs/plans/`，完成後在同一處封存。計畫記錄的是某個人當時的打算，工作往前
  推進後就不再描述現行行為；連到計畫的公開文件，會把過時的樣貌傳給讀者。雖然有規則禁止這類連結、有腳本檢查，
  計畫本身仍然持續累積。
- **規則放在 repo 外。** 程式碼風格、源碼掃描、pull request 與發版的規則放在維護者的使用者層 agent 設定裡，
  其他貢獻者和他們的 agent 都看不到。

先走過同一條路的 Polhem.OAuth2，已在它的 ADR-002 採用相同的語言政策。

## 決策

1. **共同維護的內容一律用英文**：原始碼、XML 文件（也會隨套件出現在 IntelliSense）、註解、測試方法名稱與
   `[DisplayName]` 文字、commit message、`docs/repo-ops/` 的維運文件，以及 `.claude/` 與各處 `CLAUDE.md` 的
   agent 指引。這項政策覆寫任何要求使用其他語言的個人設定。
2. **公開文件與 ADR 中英雙語**（英文與繁體中文），讓每一位貢獻者都讀得懂程式碼為什麼是現在這個樣子，
   使用者也保有兩種語言的文件。
3. **計畫與其他個人工作文件不 commit。** 它們放在 repo 根目錄、由 git 忽略的 `local/`：計畫放 `local/plans/`，
   列有未修安全問題的審查紀錄放 `local/internal/`。已 commit 的檔案不指向 `local/` 底下的檔案，也不點名計畫檔；
   `check-public-docs.sh` 會回報這兩種情形。
4. **長效的內容要升格。** 有長期價值的決策寫成 ADR；其他維護者需要看到的工作放進 GitHub issue 或 pull request。
5. **agent 指引是 repo 的一部分。** 原本放在使用者層設定的規則改 commit 到 `.claude/rules/`，讓每一位貢獻者的
   agent 遵守同一套規則。repo 不宣告個人使用的 agent plugin。

## 影響

- 翻譯分階段進行，repo 裡有一段時間仍會留有中文。不論周圍的文字是哪種語言，新寫的內容一律用英文。
- 本 ADR 一開始就寫成兩種語言。先前的 ADR 目前只有中文，英文版之後補上。ADR 兩種語言版本的同步目前沒有自動檢查，
  修改其中一份時要在同一個 commit 裡同步另一份。
- 初始匯入之前的歷史留在已凍結、之後會封存的 `jeff377/bee-library`。引用其 commit 時用完整的 commit 網址，
  引用其封存計畫時用釘在匯入起點 commit 的網址。
- 計畫無法透過 repo 分享。git worktree 裡的 session 也看不到 `local/`，所以依賴計畫的工作要交給主工作樹的 session。
- 個人設定要求其他語言的貢獻者，在這個 repo 裡仍會得到英文，因為 repo 內的 agent 指引明文寫出了這項政策。
