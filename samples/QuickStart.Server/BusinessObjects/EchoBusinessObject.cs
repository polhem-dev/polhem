using Polhem.Business.Form;
using Polhem.Definition;
using Polhem.Definition.Attributes;
using Polhem.Definition.Security;

namespace QuickStart.Server.BusinessObjects;

/// <summary>
/// Demo business object that echoes a message. <c>Define/ProgramSettings.xml</c> binds the
/// progId "Echo" to this type, and <see cref="Polhem.Business.BusinessObjectFactory"/> constructs it with the
/// <c>(IBusinessObjectContext, Guid, string, bool)</c> constructor every business object shares.
/// </summary>
/// <remarks>
/// The binding only requires a <see cref="Polhem.Business.BusinessObject"/> subclass. Deriving from
/// <see cref="FormBusinessObject"/> also brings the form methods (<c>GetList</c>, <c>Save</c> and
/// the rest), which stay behind their own authenticated access declarations; <see cref="Echo"/> is
/// the only method this class opens to anonymous callers.
/// <para>
/// The <see cref="ApiAccessControlAttribute"/> marks <see cref="Echo"/> as public and anonymous so
/// the QuickStart console can call it without an access token or encryption hand-shake.
/// </para>
/// </remarks>
public class EchoBusinessObject : FormBusinessObject
{
    /// <summary>
    /// Initializes a new instance. The business object factory creates every business object
    /// through this constructor shape.
    /// </summary>
    /// <param name="ctx">The per-call context.</param>
    /// <param name="accessToken">The access token (ignored for anonymous calls).</param>
    /// <param name="progId">The program identifier (expected to be "Echo").</param>
    /// <param name="isLocalCall">Whether the call originates from a local source.</param>
    public EchoBusinessObject(IBusinessObjectContext ctx, Guid accessToken, string progId, bool isLocalCall = false)
        : base(ctx, accessToken, progId, isLocalCall)
    {
    }

    /// <summary>
    /// Returns the input message decorated with a server-side prefix.
    /// </summary>
    /// <param name="args">The input arguments.</param>
    [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Anonymous)]
    public virtual EchoResult Echo(EchoArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return new EchoResult
        {
            Response = $"echo: {args.Message}",
            ServerTime = DateTime.UtcNow,
        };
    }
}
