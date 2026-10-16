namespace Polhem.Api.Contracts.Form
{
    /// <summary>
    /// Creates the <see cref="IFormApiConnector"/> for a program.
    /// </summary>
    public interface IFormApiConnectorFactory
    {
        /// <summary>
        /// Creates the connector for <paramref name="progId"/>.
        /// </summary>
        /// <param name="progId">The program identifier.</param>
        IFormApiConnector Create(string progId);
    }
}
