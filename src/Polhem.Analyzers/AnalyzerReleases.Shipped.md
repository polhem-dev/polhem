; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
;
; NOTE: this file was empty until 4.28.0 although the analyzer had shipped since 4.16.0, so the
; entries below were reconstructed from the tagged snapshots of AnalyzerReleases.Unshipped.md
; rather than written at each release. The consequence of the gap is recorded in the 4.19.0
; section: four rules were retired with nothing to say so.

## Release 4.16.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------------|----------|-------------------------------------------------------------------
POLHEM1001 | Polhem.Definition | Error | FormSchemaCategoryIdAnalyzer — CategoryId must be an accepted database scope
POLHEM1002 | Polhem.Definition | Error | DbCategorySettingsScopeAnalyzer — DbCategory Id must be an accepted database scope
POLHEM1003 | Polhem.Definition | Error | FieldDbTypeAnalyzer — DbType must be a member of the field type enumeration
POLHEM1004 | Polhem.Definition | Error | FieldListReferenceAnalyzer — field lists must only reference declared fields
POLHEM1005 | Polhem.Definition | Error | RelationMappingAnalyzer — a relation mapping must target a declared field
POLHEM1006 | Polhem.Definition | Warning | RelationMappingAnalyzer — a relation field should be populated by a mapping
POLHEM1007 | Polhem.Definition | Error | DuplicateFieldNameAnalyzer — a table must not declare the same field twice
POLHEM2001 | Polhem.Definition | Warning | FormSchemaTableRegistrationAnalyzer — table must be registered under the declared scope
POLHEM2002 | Polhem.Definition | Warning | SidecarDefinitionAnalyzer — a table schema must exist under the folder matching the scope
POLHEM2003 | Polhem.Definition | Error | RelationReferenceAnalyzer — a relation field must reference an existing FormSchema
POLHEM2004 | Polhem.Definition | Error | RelationReferenceAnalyzer — a relation mapping must read a declared field
POLHEM2005 | Polhem.Definition | Warning | SidecarDefinitionAnalyzer — a FormSchema should have a corresponding FormLayout
POLHEM2006 | Polhem.Definition | Warning | PersistedFieldAnalyzer — a persisted field must exist in the table schema
POLHEM2007 | Polhem.Definition | Info | LanguageCoverageAnalyzer — cultures should cover the same translation keys
POLHEM3001 | Polhem.Business | Warning | BusinessObjectAccessControlAnalyzer — a business object API method must declare access control
POLHEM3002 | Polhem.Definition | Warning | DefinitionCollectionPropertyAnalyzer — a framework collection property must use a framework collection type
POLHEM4001 | Polhem.Serialization | Error | CollectionFormatterRegistrationAnalyzer — a MessagePack collection must be registered with a formatter
POLHEM4002 | Polhem.Serialization | Error | WireFieldNameAnalyzer — a JSON rename must not conflict with name-based MessagePack keys
POLHEM4003 | Polhem.Serialization | Error | UnionKeyStrategyAnalyzer — a union hierarchy must use integer MessagePack keys
POLHEM4004 | Polhem.Serialization | Error | MessagePackConstructorOrderAnalyzer — constructor parameters must follow integer key order
POLHEM4005 | Polhem.Serialization | Warning | CollectionAddOverloadAnalyzer — a framework collection should expose a single public Add
POLHEM4006 | Polhem.Serialization | Error | ParameterlessConstructorAnalyzer — a serialized type must have a public parameterless constructor

## Release 4.18.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------------|----------|-------------------------------------------------------------------
POLHEM3003 | Polhem.Business | Warning | ExecFuncAccessControlAnalyzer — an ExecFunc handler method must declare access control

## Release 4.19.0

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------------|----------|-------------------------------------------------------------------
POLHEM4001 | Polhem.Serialization | Error | CollectionFormatterRegistrationAnalyzer — a MessagePack collection must be registered with a formatter
POLHEM4002 | Polhem.Serialization | Error | WireFieldNameAnalyzer — a JSON rename must not conflict with name-based MessagePack keys
POLHEM4003 | Polhem.Serialization | Error | UnionKeyStrategyAnalyzer — a union hierarchy must use integer MessagePack keys
POLHEM4004 | Polhem.Serialization | Error | MessagePackConstructorOrderAnalyzer — constructor parameters must follow integer key order

## Release 4.28.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------------|----------|-------------------------------------------------------------------
POLHEM1008 | Polhem.Definition | Info | PermissionModelAnalyzer — a form schema with no PermissionModelId is open to every authenticated caller
