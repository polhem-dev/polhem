using System.Data.Common;
using System.Globalization;

namespace Polhem.Db.Manager
{
    /// <summary>
    /// Resolves the <c>{@DbName}</c>, <c>{@UserId}</c> and <c>{@Password}</c> placeholders of a
    /// connection string template.
    /// </summary>
    /// <remarks>
    /// The template is parsed into key/value pairs and written back with
    /// <see cref="DbConnectionStringBuilder"/>, so a substituted value is quoted as the connection
    /// string grammar requires. Plain text substitution would let a value containing <c>;</c> or
    /// <c>=</c> end its pair and start a new keyword, which breaks a legitimate password with those
    /// characters and lets a crafted one add connection options. A template with no placeholder is
    /// returned unchanged.
    /// </remarks>
    public static class ConnectionStringTemplate
    {
        private const string DbNamePlaceholder = "{@DbName}";
        private const string UserIdPlaceholder = "{@UserId}";
        private const string PasswordPlaceholder = "{@Password}";

        /// <summary>
        /// Returns the connection string with each placeholder replaced by its value.
        /// </summary>
        /// <param name="template">The connection string template.</param>
        /// <param name="dbName">The database name; an empty value leaves <c>{@DbName}</c> in place.</param>
        /// <param name="userId">The login user id; an empty value leaves <c>{@UserId}</c> in place.</param>
        /// <param name="password">The login password; an empty value leaves <c>{@Password}</c> in place.</param>
        /// <returns>The resolved connection string.</returns>
        /// <remarks>
        /// Placeholders match case-insensitively and may sit anywhere inside a value, for example
        /// <c>Data Source=file:app_{@DbName}.db</c>. When a placeholder is present, the whole string
        /// is rewritten: keywords may come back in lower case and values quoted, both of which are
        /// ordinary connection string syntax.
        /// </remarks>
        public static string Resolve(string template, string dbName, string userId, string password)
        {
            if (string.IsNullOrEmpty(template) || template.IndexOf("{@", StringComparison.Ordinal) < 0)
                return template;

            var builder = new DbConnectionStringBuilder { ConnectionString = template };
            foreach (var key in builder.Keys.Cast<string>().ToList())
            {
                string value = Convert.ToString(builder[key], CultureInfo.InvariantCulture) ?? string.Empty;
                string resolved = Substitute(value, DbNamePlaceholder, dbName);
                resolved = Substitute(resolved, UserIdPlaceholder, userId);
                resolved = Substitute(resolved, PasswordPlaceholder, password);
                if (!string.Equals(resolved, value, StringComparison.Ordinal))
                    builder[key] = resolved;
            }
            return builder.ConnectionString;
        }

        private static string Substitute(string value, string placeholder, string replacement)
            => string.IsNullOrEmpty(replacement)
                ? value
                : value.Replace(placeholder, replacement, StringComparison.OrdinalIgnoreCase);
    }
}
