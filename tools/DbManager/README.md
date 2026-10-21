# Polhem.DbManager

[繁體中文](https://github.com/polhem-dev/polhem/blob/main/tools/DbManager/README.zh-TW.md)

The database manager of the [Polhem](https://github.com/polhem-dev/polhem) framework, packaged as a .NET tool. It
creates, checks and upgrades the databases a definition folder describes, for SQL Server, PostgreSQL, MySQL, Oracle and
SQLite, without an application project. DefineEditor runs it for its database work.

## Run

```bash
dotnet tool install --global Polhem.DbManager
polhem-db check --define ./Define
```

Or run it once without installing it, at a chosen version:

```bash
dotnet tool exec Polhem.DbManager@<version> -- upgrade --define ./Define
```

`--define` names the definition folder, the one that holds `SystemSettings.xml`; without it the current directory is
used. Run `polhem-db --help` for the commands and their options.

| Command | What it does |
|---------|--------------|
| `create` | Creates the databases named in DatabaseSettings that do not exist |
| `check` | Opens a connection to each database in DatabaseSettings |
| `upgrade` | Builds or upgrades the tables registered in DbCategorySettings; `--table` limits it to named tables, `--dry-run` only lists the statements |
| `seed-admin` | Creates the first administrator, a company and the access between them |

The commands are those an application's server runs with `dotnet run -- db <command>`, described with their exit codes
in [Initializing a New Deployment](https://github.com/polhem-dev/polhem/blob/main/docs/en/database/database-settings-guide.md#7-initializing-a-new-deployment).
The tool reads the definition folder the way the server does, master key included: an environment-variable source with
no key gets a key for this run only, which is enough unless DatabaseSettings holds encrypted passwords; a key file is
never created.

The tool redistributes the database drivers; their licenses are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
