# 參與 Polhem 開發

[English](CONTRIBUTING.md) | **繁體中文**

感謝你對 Polhem 的興趣。本文說明變更如何進入 repo，以及變更要遵守的慣例。

## 開始之前

- 修 bug 或小幅改善，可以直接開 pull request。
- 較大的變更（新功能、修改公開 API、新增相依套件），請先開 issue，在投入時間之前先對做法取得共識。
- 架構請讀[開發者文件](docs/zh-TW/README.md)；設計為什麼是現在這個樣子，請讀[架構決策紀錄](docs/adr/README.zh-TW.md)。

## 工作流程

1. 從最新的 `main` fork 這個 repo；有寫入權限的話可以直接開分支。
2. 完成變更，並附上測試。
3. 在本機建置與測試（見下節）。
4. 對 `main` 開 pull request。

`main` 只透過 pull request 接受變更。pull request 必須通過 `build` 與 `docs` 檢查才能合併，並會請
[`.github/CODEOWNERS`](.github/CODEOWNERS) 列出的 code owner 審查。

## 建置與測試

```bash
dotnet build Polhem.slnx --configuration Release
./test.sh
```

- 建置把所有警告都視為錯誤。許多程式碼風格規則由 `.editorconfig` 與分析器強制，違反就會建置失敗；
  其餘規則寫在 `.claude/rules/code-style.md`。
- `./test.sh` 會在 Docker 有對應容器時啟動本機資料庫容器，並執行所有測試專案。需要資料庫的測試在該資料庫的
  連線字串未設定時會略過；容器名稱與覆寫方式見腳本檔頭。傳入測試專案路徑可以只跑該專案。
- 變更涉及公開 API 時，請在該專案的 `PublicAPI.Unshipped.txt` 申報，並在 pull request 中說明是否二進位相容。
  分析器只檢查變更有沒有申報，不檢查是否相容。
- 新增的公開 API 要附測試。

## 慣例

- **語言**：共同維護的內容一律用英文：程式碼、XML 文件、註解、測試名稱與 `[DisplayName]` 文字、commit message。
  給使用 Polhem 開發應用程式的開發者看的使用者文件是多語系（英文與繁體中文）；維護者文件（包括 ADR 與本指南）只用英文。
  repo 裡仍有部分內容是這項政策之前留下的中文；新寫的內容一律用英文。理由見
  [ADR-045](docs/adr/adr-045-language-policy-and-local-plans.zh-TW.md) 與
  [ADR-047](docs/adr/adr-047-documents-split-by-reader.md)。
- **commit message**：英文、祈使語氣，標題說明改了什麼；在內文說明為什麼。
- **設計決策**：日後需要讓其他人理解的決策，以 ADR 記錄在 `docs/adr/`。
- **文件**：修改 Markdown 文件後，執行 `./check-md-links.sh` 與 `./check-public-docs.sh`。
  `docs/<lang>/` 下的文件以英文為源；繁體中文譯本落後源文件時，`./check-docs-i18n.sh` 會回報。
  無法一併更新譯本時，請在 pull request 中說明。
- **個人工作文件**：計畫、草稿與筆記放在 repo 根目錄的 `local/`，git 會忽略它。不要 commit，也不要從已 commit
  的檔案連過去：其他人都開不到。

## AI coding agent

給 coding agent 的指引在 `.claude/`：`.claude/CLAUDE.md`、`.claude/rules/` 的規則與 `.claude/skills/` 的 skill。
它們入版控、和程式碼一樣維護，所以適用於每一位使用 agent 的貢獻者。那裡的規則也是上述慣例的詳細參考。

## 發版

發版由維護者進行。推送 `v*` tag 會把套件發佈到 NuGet，而已發佈的套件無法撤回，所以只在發版建置與測試通過後，
經過確認才推送 tag。發版建置失敗時，不得以修改測試或受檢的程式碼讓它轉綠。

## 授權

提交貢獻即表示你同意你的貢獻以 [MIT 授權](LICENSE.txt)釋出。
