# Polhem.Cli

[繁體中文](https://github.com/polhem-dev/polhem/blob/main/tools/Polhem.Cli/README.zh-TW.md)

The command-line tool of the [Polhem](https://github.com/polhem-dev/polhem) framework, packaged as a .NET tool.

## Install

```bash
dotnet tool install --global Polhem.Cli
```

The tool command is `dotnet-polhem`, so it runs as `dotnet polhem`.

## Commands

| Command | What it does |
|---------|--------------|
| `dotnet polhem defines materialize` | Writes the default definition files embedded in `Polhem.Definition` to a directory, so an application can start from them and customize them |
| `dotnet polhem defines list` | Lists the relative paths of the embedded default definition files |
| `dotnet polhem keys protect` | Generates a new key and prints it encrypted with the master key, as the value `SecurityKeySettings` in `SystemSettings.xml` stores (for example `ApiEncryptionKey`) |
| `dotnet polhem forms template` | Writes an empty Excel workbook for describing a new form |
| `dotnet polhem forms export` | Writes the Excel workbook of an existing form, with its zh-TW texts and menu placement |
| `dotnet polhem forms import` | Turns a form workbook into the FormSchema, a TableSchema per table, the FormLayout and the registrations a runnable form needs; `--dry-run` lists the files without writing them |

How a form workbook is laid out and what the import fills in by convention is described in
[Modeling Forms in Excel](https://github.com/polhem-dev/polhem/blob/main/docs/en/guides/excel-form-modeling.md).

The options of each command are printed by the tool itself, which is the authority for them:

```bash
dotnet polhem --help
dotnet polhem help defines
dotnet polhem defines materialize --help
```

## License

MIT. See the [repository](https://github.com/polhem-dev/polhem).
