# Polhem — 範例

[English](README.md) | **繁體中文**

每個 UI 框架一個控制項 DemoCenter。都不需要後端或登入，主題、案例和範例資料相同，所以同一個控制項行為在各框架都有對應範例。日後新增 UI 套件時，再加它自己的 DemoCenter。

| 專案 | UI 套件 | 執行 |
|------|---------|------|
| [`Avalonia.DemoCenter`](Avalonia.DemoCenter/README.zh-TW.md) | `Polhem.UI.Avalonia` | `dotnet run --project samples/Avalonia.DemoCenter` |
| [`Blazor.Mud.DemoCenter`](Blazor.Mud.DemoCenter/README.zh-TW.md) | `Polhem.Web.Blazor.Mud` | `dotnet run --project samples/Blazor.Mud.DemoCenter` → <http://localhost:5055> |

哪些案例有差異、哪些沒有對應，見 [Blazor DemoCenter 的 README](Blazor.Mud.DemoCenter/README.zh-TW.md#主題與案例)。

含後端、登入和資料庫的完整應用見 [`apps/Polhem.Northwind`](../apps/Polhem.Northwind/README.zh-TW.md)。從空資料夾建立後端見 [Getting Started](../docs/zh-TW/getting-started/getting-started.md)。

## 建置

```bash
dotnet build samples/Polhem.Samples.slnx
```

專案直接參考 `src/`，函式庫改動下次執行就會生效。主要的 `Polhem.slnx` 和 `./test.sh` 不包含它們。
