using Polhem.Business.Form;
using Polhem.Business.System;
using Polhem.Definition;
using Polhem.Definition.Identity;

namespace Polhem.Business
{
    /// <summary>
    /// Typed convenience wrappers around <see cref="IBusinessObjectFactory"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="IBusinessObjectFactory"/> lives in <c>Polhem.Definition</c> and returns
    /// <see cref="object"/> to avoid a reverse dependency from <c>Polhem.Definition</c> to
    /// <c>Polhem.Business</c> (where <see cref="IFormBusinessObject"/> /
    /// <see cref="ISystemBusinessObject"/> live). These extension methods are defined in
    /// <c>Polhem.Business</c> so the cast can be encapsulated once for callers that already
    /// reference <c>Polhem.Business</c> (typically other business objects making BO-to-BO calls).
    /// <para>
    /// One method per axis: each axis has a single canonical interface and the concrete
    /// type is selected by <c>progId</c> at runtime, so a generic overload would not add
    /// value over the manual cast it would otherwise replace.
    /// </para>
    /// </remarks>
    public static class BusinessObjectFactoryExtensions
    {
        /// <summary>
        /// Creates a form-level business object and returns it as <see cref="IFormBusinessObject"/>.
        /// </summary>
        /// <param name="factory">The factory.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="progId">The program identifier.</param>
        /// <param name="isLocalCall">Indicates whether the call originates from a local source.</param>
        public static IFormBusinessObject CreateFormBO(
            this IBusinessObjectFactory factory,
            Guid accessToken,
            string progId,
            bool isLocalCall)
        {
            ArgumentNullException.ThrowIfNull(factory);
            return (IFormBusinessObject)factory.CreateBusinessObject(accessToken, progId, isLocalCall);
        }

        /// <summary>
        /// Creates a form business object that shares the calling request's snapshot of the session's company scope.
        /// </summary>
        /// <param name="factory">The business object factory.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="progId">The program ID.</param>
        /// <param name="isLocalCall">Whether the call originates from a local source.</param>
        /// <param name="companyScope">The calling business object's <c>CompanyScope</c>.</param>
        /// <remarks>
        /// A business object calling another passes its own snapshot, so both act for the same company even when
        /// the session enters another company meanwhile (ADR-063).
        /// </remarks>
        public static IFormBusinessObject CreateFormBO(
            this IBusinessObjectFactory factory,
            Guid accessToken,
            string progId,
            bool isLocalCall,
            SessionCompanyScope companyScope)
        {
            ArgumentNullException.ThrowIfNull(factory);
            return (IFormBusinessObject)factory.CreateBusinessObject(accessToken, progId, isLocalCall, companyScope);
        }

        /// <summary>
        /// Creates a system-level business object and returns it as <see cref="ISystemBusinessObject"/>.
        /// </summary>
        /// <param name="factory">The factory.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="isLocalCall">Indicates whether the call originates from a local source.</param>
        public static ISystemBusinessObject CreateSystemBO(
            this IBusinessObjectFactory factory,
            Guid accessToken,
            bool isLocalCall)
        {
            ArgumentNullException.ThrowIfNull(factory);
            return (ISystemBusinessObject)factory.CreateBusinessObject(accessToken, SysProgIds.System, isLocalCall);
        }

        /// <summary>
        /// Creates the system business object that shares the calling request's snapshot of the session's company
        /// scope.
        /// </summary>
        /// <param name="factory">The business object factory.</param>
        /// <param name="accessToken">The access token.</param>
        /// <param name="isLocalCall">Whether the call originates from a local source.</param>
        /// <param name="companyScope">The calling business object's <c>CompanyScope</c>.</param>
        public static ISystemBusinessObject CreateSystemBO(
            this IBusinessObjectFactory factory,
            Guid accessToken,
            bool isLocalCall,
            SessionCompanyScope companyScope)
        {
            ArgumentNullException.ThrowIfNull(factory);
            return (ISystemBusinessObject)factory.CreateBusinessObject(accessToken, SysProgIds.System, isLocalCall, companyScope);
        }
    }
}
