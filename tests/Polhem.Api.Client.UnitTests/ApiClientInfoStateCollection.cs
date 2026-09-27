namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Serializes the test classes that mutate the process-wide static
    /// <see cref="Polhem.Api.Client.ApiClientInfo.SupportedConnectTypes"/>. xUnit runs every class in
    /// one collection sequentially, eliminating the cross-class race where one class sets the static
    /// to <c>Remote</c> while another sets it to <c>Both</c> mid-assertion (snapshot/restore alone
    /// only holds under serial execution, not parallel classes).
    /// </summary>
    [CollectionDefinition(Name)]
    public static class ApiClientInfoStateCollection
    {
        /// <summary>The collection name. Reference this constant instead of repeating the string.</summary>
        public const string Name = "ApiClientInfoState";
    }
}
