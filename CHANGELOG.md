# Changelog

[繁體中文](CHANGELOG.zh-TW.md)

Notable changes to the Polhem packages. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versions follow [Semantic Versioning](https://semver.org/). Each version lists its changes here in one line each;
the reasons and the background are in its detailed notes under [`docs/changelogs/`](docs/changelogs/).

## [Unreleased]

## [1.0.0] - Unreleased

> Polhem continues [Bee.NET](https://github.com/jeff377/bee-library) under a new name. The changes below are relative to
> the last Bee.NET release, 4.33.0: apart from the renaming, the code is that of Bee.NET 4.33.0. How to move an
> application over is described in [Migrating from Bee.NET](README.md#migrating-from-beenet).

📄 Full notes and background: [docs/changelogs/1.0.0.md](docs/changelogs/1.0.0.md)

### Changed

- Package IDs and namespaces are renamed from `Bee.*` to `Polhem.*`, and versions restart at 1.0.0. The split into
  packages is unchanged.
- Types and members named after Bee are renamed: `AddBeeFramework` → `AddPolhemFramework`, `UseBeeFramework` →
  `UsePolhemFramework`, `AddBeeBlazor` → `AddPolhemBlazor`, `IBeeContext` / `BeeContext` → `IPolhemContext` /
  `PolhemContext`, `BeeStringLocalizer<T>` → `PolhemStringLocalizer<T>`, `BeeBlazorOptions` → `PolhemBlazorOptions`,
  `BeeBlazorProviderMode` → `PolhemBlazorProviderMode`, `BeeApiConnectorFactory` → `PolhemApiConnectorFactory`,
  `BeeAccessTokenProvider` → `PolhemAccessTokenProvider`, `BeeLoginPanel` → `PolhemLoginPanel`, and the extension
  classes `BeeFrameworkServiceCollectionExtensions`, `BeeFrameworkApplicationBuilderExtensions` and
  `BeeBlazorServiceCollectionExtensions` → `Polhem…`.
- The command-line tool `Bee.Cli` is now `Polhem.Cli`, invoked as `dotnet polhem`.
- Analyzer diagnostic IDs are renamed from `BEE` to `POLHEM` with the same numbers, for example `BEE1008` →
  `POLHEM1008`.
- The MSBuild properties for definition file checks are renamed: `BeeDefinitionFilesGlob`, `BeeRequireDefinitionFiles`
  and `BeeAnalyzeDefinitionFiles` → `PolhemDefinitionFilesGlob`, `PolhemRequireDefinitionFiles` and
  `PolhemAnalyzeDefinitionFiles`.
- The default environment variable of the master key is `POLHEM_MASTER_KEY` instead of `BEE_MASTER_KEY`.
- Type names in payloads and in the default settings are `Polhem.*`. A Bee.NET client and a Polhem server, or the other
  way around, cannot talk to each other.
- The labels that derive session encryption keys are `polhem-api-session-key` and `polhem-api-encryption-root-key`
  instead of `bee-api-*`, so sessions created by Bee.NET end when a deployment switches.
- The CSS classes of the Blazor components start with `polhem-` instead of `bee-`.
- The `DataColumn` extended property that records a declared field type is `Polhem.FieldDbType` instead of
  `Bee.FieldDbType`, and `SerializationErrorData.FilePath` is `Polhem.FilePath` instead of `Bee.FilePath`.
- Package metadata: the authors and the copyright holder are Polhem contributors, the repository is
  `polhem-dev/polhem`, and the packages have a new icon.

[Unreleased]: https://github.com/polhem-dev/polhem/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/polhem-dev/polhem/releases/tag/v1.0.0
