# Polhem — Samples

**English** | [繁體中文](README.zh-TW.md)

One control demo center per UI framework. Each runs without a backend or sign-in, and both use the same topics, cases and sample data, so the same control behavior has a matching example in each. A future UI package gets its own demo center when it is added.

| Project | UI package | Run it |
|---------|------------|--------|
| [`Avalonia.DemoCenter`](Avalonia.DemoCenter/README.md) | `Polhem.UI.Avalonia` | `dotnet run --project samples/Avalonia.DemoCenter` |
| [`Blazor.Mud.DemoCenter`](Blazor.Mud.DemoCenter/README.md) | `Polhem.Web.Blazor.Mud` | `dotnet run --project samples/Blazor.Mud.DemoCenter` → <http://localhost:5055> |

Which cases differ, and which have no counterpart, is in the [Blazor demo center's README](Blazor.Mud.DemoCenter/README.md#topics-and-cases).

For a complete application with a backend, sign-in and a database, see [`apps/Polhem.Northwind`](../apps/Polhem.Northwind/README.md). To build a backend from an empty folder, see [Getting Started](../docs/en/getting-started/getting-started.md).

## Build

```bash
dotnet build samples/Polhem.Samples.slnx
```

The projects reference `src/` directly, so a change to a library shows on the next run. The main `Polhem.slnx` and `./test.sh` do not include them.
