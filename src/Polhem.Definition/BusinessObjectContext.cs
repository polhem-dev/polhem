using Polhem.Definition.Identity;
using Polhem.Definition.Language;
using Polhem.Definition.Storage;

namespace Polhem.Definition
{
    /// <summary>
    /// Default <see cref="IBusinessObjectContext"/> implementation; a plain POCO assembled
    /// by <c>BusinessObjectFactory</c> at BO construction time.
    /// </summary>
    public sealed class BusinessObjectContext : IBusinessObjectContext
    {
        /// <inheritdoc/>
        public required IDefineAccess DefineAccess { get; init; }

        /// <inheritdoc/>
        public required ISessionInfoService SessionInfoService { get; init; }

        /// <inheritdoc/>
        public required ILanguageService LanguageService { get; init; }

        /// <inheritdoc/>
        public required IBusinessObjectFactory BoFactory { get; init; }

        /// <inheritdoc/>
        public required IServiceProvider Services { get; init; }
    }
}
