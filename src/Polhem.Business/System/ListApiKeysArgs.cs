using Polhem.Api.Contracts.System;

namespace Polhem.Business.System
{
    /// <summary>
    /// Input arguments for listing the issued API keys. Empty by design — see
    /// <see cref="IListApiKeysRequest"/>.
    /// </summary>
    public sealed class ListApiKeysArgs : BusinessArgs, IListApiKeysRequest
    {
    }
}
