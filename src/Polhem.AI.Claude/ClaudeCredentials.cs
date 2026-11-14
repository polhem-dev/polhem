using System.Text.Json;

namespace Polhem.AI.Claude
{
    /// <summary>
    /// Reports which credential the Anthropic SDK will use, without reading or keeping any secret.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The SDK looks for a credential in this order and uses the first it finds: the <c>ANTHROPIC_API_KEY</c>
    /// environment variable, the <c>ANTHROPIC_AUTH_TOKEN</c> environment variable, the profile named by
    /// <c>ANTHROPIC_PROFILE</c>, workload identity federation environment variables, and finally the active profile that
    /// <c>ant auth login</c> saved (the one <c>active_config</c> names, otherwise <c>default</c>). An empty environment
    /// variable counts as not set.
    /// </para>
    /// <para>
    /// IMPORTANT: this order mirrors the Anthropic SDK version this package references
    /// (<c>Anthropic.Credentials.AnthropicCredentials.Resolve</c>), which the SDK keeps internal. Check it again when the
    /// SDK is upgraded.
    /// </para>
    /// </remarks>
    public static class ClaudeCredentials
    {
        internal const string ApiKeyVariable = "ANTHROPIC_API_KEY";
        internal const string AuthTokenVariable = "ANTHROPIC_AUTH_TOKEN";
        internal const string ProfileVariable = "ANTHROPIC_PROFILE";
        internal const string ConfigDirVariable = "ANTHROPIC_CONFIG_DIR";
        internal const string BaseUrlVariable = "ANTHROPIC_BASE_URL";
        internal const string FederationRuleVariable = "ANTHROPIC_FEDERATION_RULE_ID";
        internal const string IdentityTokenFileVariable = "ANTHROPIC_IDENTITY_TOKEN_FILE";
        internal const string IdentityTokenVariable = "ANTHROPIC_IDENTITY_TOKEN";

        private const string SignInHint = "Run `ant auth login` to sign in.";

        /// <summary>
        /// Describes the credential the Anthropic SDK will use on this machine.
        /// </summary>
        /// <returns>The description; it never contains a key or a token.</returns>
        public static ClaudeCredentialSource Describe()
            => Describe(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                OperatingSystem.IsWindows() ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) : null);

        internal static ClaudeCredentialSource Describe(Func<string, string?> getVariable, string homeDirectory,
            string? windowsAppData)
        {
            string? Variable(string name) => getVariable(name) is { Length: > 0 } value ? value : null;

            var warnings = new List<string>();
            var configDirectory = ResolveConfigDirectory(Variable, homeDirectory, windowsAppData);
            var baseUrlOverride = Variable(BaseUrlVariable);
            var explicitProfile = Variable(ProfileVariable);

            if (getVariable(ApiKeyVariable) is { Length: 0 })
            {
                warnings.Add($"{ApiKeyVariable} is set but empty. This application ignores it, but the ant CLI and the "
                    + "Anthropic SDKs of other languages use the empty key and fail; unset it.");
            }

            if (Variable(ApiKeyVariable) is not null)
            {
                if (Variable(AuthTokenVariable) is not null)
                    warnings.Add($"{AuthTokenVariable} is ignored because {ApiKeyVariable} is set.");
                if (ActiveProfile(configDirectory, Variable) is { } hidden && File.Exists(ConfigPath(configDirectory, hidden)))
                {
                    warnings.Add($"{ApiKeyVariable} hides the profile '{hidden}' saved by `ant auth login`. "
                        + "Unset it to use the profile.");
                }
                return new ClaudeCredentialSource(ClaudeCredentialKind.ApiKey,
                    $"API key from the {ApiKeyVariable} environment variable", warnings) { BaseUrl = baseUrlOverride };
            }

            if (Variable(AuthTokenVariable) is not null)
            {
                return new ClaudeCredentialSource(ClaudeCredentialKind.AuthToken,
                    $"Access token from the {AuthTokenVariable} environment variable", warnings) { BaseUrl = baseUrlOverride };
            }

            if (explicitProfile is not null)
            {
                return DescribeProfile(configDirectory, explicitProfile, baseUrlOverride, warnings, explicitSelection: true)
                    ?? None(warnings, baseUrlOverride);
            }

            if (Variable(FederationRuleVariable) is not null
                && (Variable(IdentityTokenFileVariable) is not null || Variable(IdentityTokenVariable) is not null))
            {
                return new ClaudeCredentialSource(ClaudeCredentialKind.WorkloadIdentity,
                    "Workload identity federation from environment variables", warnings) { BaseUrl = baseUrlOverride };
            }

            var active = ActiveProfile(configDirectory, Variable);
            if (active is null)
            {
                warnings.Add("The active profile named in active_config is not a valid profile name.");
                return None(warnings, baseUrlOverride);
            }
            return DescribeProfile(configDirectory, active, baseUrlOverride, warnings, explicitSelection: false)
                ?? None(warnings, baseUrlOverride);
        }

        private static ClaudeCredentialSource None(List<string> warnings, string? baseUrl)
            => new(ClaudeCredentialKind.None, "No credential found. " + SignInHint, warnings) { BaseUrl = baseUrl };

        private static ClaudeCredentialSource? DescribeProfile(string configDirectory, string profile, string? baseUrlOverride,
            List<string> warnings, bool explicitSelection)
        {
            if (!IsValidProfileName(profile))
            {
                warnings.Add($"'{profile}' is not a valid profile name.");
                return null;
            }

            var configPath = ConfigPath(configDirectory, profile);
            if (!File.Exists(configPath))
            {
                if (explicitSelection)
                    warnings.Add($"{ProfileVariable} names the profile '{profile}', which does not exist. {SignInHint}");
                return null;
            }

            ProfileConfig config;
            try
            {
                config = ReadConfig(configPath);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                warnings.Add($"The configuration of the profile '{profile}' cannot be read. {SignInHint}");
                return null;
            }

            if (config.AuthenticationType is null)
            {
                warnings.Add($"The profile '{profile}' has no sign-in. {SignInHint}");
                return null;
            }

            if (config.AuthenticationType == "user_oauth")
            {
                var credentialsPath = config.CredentialsPath
                    ?? Path.Combine(configDirectory, "credentials", profile + ".json");
                if (!File.Exists(credentialsPath))
                    warnings.Add($"The profile '{profile}' has no saved sign-in. {SignInHint}");
            }
            else if (config.AuthenticationType != "oidc_federation")
            {
                warnings.Add($"The profile '{profile}' uses the sign-in type '{config.AuthenticationType}', "
                    + "which the Anthropic SDK does not support.");
                return null;
            }

            var description = config.AuthenticationType == "user_oauth"
                ? $"Signed in with ant auth login (profile: {profile})"
                : $"Workload identity federation (profile: {profile})";
            return new ClaudeCredentialSource(ClaudeCredentialKind.Profile, description, warnings)
            {
                ProfileName = profile,
                WorkspaceId = config.WorkspaceId,
                OrganizationId = config.OrganizationId,
                BaseUrl = baseUrlOverride ?? config.BaseUrl,
            };
        }

        private static string ResolveConfigDirectory(Func<string, string?> variable, string homeDirectory,
            string? windowsAppData)
        {
            if (variable(ConfigDirVariable) is { } configured)
                return configured;
            if (windowsAppData is not null)
                return Path.Combine(windowsAppData, "Anthropic");
            if (variable("XDG_CONFIG_HOME") is { } xdg)
                return Path.Combine(xdg, "anthropic");
            return Path.Combine(homeDirectory, ".config", "anthropic");
        }

        private static string? ActiveProfile(string configDirectory, Func<string, string?> variable)
        {
            if (variable(ProfileVariable) is { } named)
                return IsValidProfileName(named) ? named : null;

            var pointer = Path.Combine(configDirectory, "active_config");
            if (File.Exists(pointer))
            {
                var name = File.ReadAllText(pointer).Trim();
                if (name.Length > 0)
                    return IsValidProfileName(name) ? name : null;
            }
            return "default";
        }

        private static string ConfigPath(string configDirectory, string profile)
            => Path.Combine(configDirectory, "configs", profile + ".json");

        // The profile name becomes part of a file path, so anything that could leave the configuration folder is refused.
        private static bool IsValidProfileName(string name)
            => name.Length is > 0 and <= 100
               && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
               && name is not ("." or "..");

        private static ProfileConfig ReadConfig(string path)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            string? Text(JsonElement element, string name)
                => element.ValueKind == JsonValueKind.Object
                   && element.TryGetProperty(name, out var value)
                   && value.ValueKind == JsonValueKind.String
                   && value.GetString() is { Length: > 0 } text
                    ? text
                    : null;

            var authentication = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("authentication", out var auth) ? auth : default;
            return new ProfileConfig(
                Text(authentication, "type"),
                Text(authentication, "credentials_path"),
                Text(root, "workspace_id"),
                Text(root, "organization_id"),
                Text(root, "base_url"));
        }

        private sealed record ProfileConfig(string? AuthenticationType, string? CredentialsPath, string? WorkspaceId,
            string? OrganizationId, string? BaseUrl);
    }
}
