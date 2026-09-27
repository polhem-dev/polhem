using Polhem.Base.Collections;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Polhem.Definition.Settings
{
    /// <summary>
    /// A service endpoint list item.
    /// </summary>
    [Description("Service endpoint list item.")]
    public sealed class EndpointItem : CollectionItem
    {
        /// <summary>
        /// Initializes a new instance of <see cref="EndpointItem"/>.
        /// </summary>
        public EndpointItem()
        { }

        /// <summary>
        /// Initializes a new instance of <see cref="EndpointItem"/>.
        /// </summary>
        /// <param name="name">The service endpoint name.</param>
        /// <param name="endpoint">The service endpoint location. Use a URL for remote connections or a local path for local connections.</param>
        public EndpointItem(string name, string endpoint)
        {
            Name = name;
            Endpoint = endpoint;
        }

        /// <summary>
        /// Gets or sets the service endpoint name.
        /// </summary>
        [XmlAttribute]
        [Description("Service endpoint name.")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the service endpoint location. Use a URL for remote connections or a local path for local connections.
        /// </summary>
        [XmlAttribute]
        [Description("Service endpoint location. Use a URL for remote connections or a local path for local connections.")]
        public string Endpoint { get; set; } = string.Empty;
    }
}
