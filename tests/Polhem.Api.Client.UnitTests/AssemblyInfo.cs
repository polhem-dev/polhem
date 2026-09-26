// `ApiClientInfoTests` and `ApiConnectValidatorTests` have to temporarily overwrite the process-wide statics
// `ApiClientInfo.SupportedConnectTypes`, `ConnectType`, `Endpoint`, `ApiKey` and `ApiEncryptionKey` to check
// their logic. Meanwhile `ApiConnector` reads the encryption key on every request path, `RemoteApiProvider`
// reads `ApiKey` and `ApiConnectValidator` reads `SupportedConnectTypes`, so under parallel execution a reader
// lands inside a writer's try/finally restore window.
//
// The `ApiClientInfoState` collection already groups several classes (including the reader
// `TenantCustomizationEndToEndTests`, added on purpose), but `SystemApiConnectorTests` and
// `ClientDefineAccessTests`, which also drive the connector, were missed. This is the same read/write asymmetry
// that `Polhem.Api.Core.UnitTests` hit in CI build #31169045420. That time it actually went red; here it is
// only latent for now.
//
// Readers grow with new tests, so adding `[Collection]` class by class is bound to miss some. Turning off
// parallelization for the whole assembly is simplest and hard to get wrong (the same approach as
// `Polhem.ObjectCaching.UnitTests` and `Polhem.Api.Core.UnitTests`). The root fix is to move the per-session
// state of `ApiClientInfo` into DI.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
