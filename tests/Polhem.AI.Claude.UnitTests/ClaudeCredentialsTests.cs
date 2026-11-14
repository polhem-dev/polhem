using System.ComponentModel;

namespace Polhem.AI.Claude.UnitTests
{
    public sealed class ClaudeCredentialsTests : IDisposable
    {
        private const string SecretKey = "sk-ant-secret-value";
        private const string SecretToken = "oauth-secret-token";

        private readonly string _home = Path.Combine(Path.GetTempPath(), "polhem-claude-" + Guid.NewGuid().ToString("N"));
        private readonly Dictionary<string, string?> _variables = new(StringComparer.Ordinal);

        public ClaudeCredentialsTests() => Directory.CreateDirectory(_home);

        public void Dispose() => Directory.Delete(_home, recursive: true);

        private string ConfigDirectory => Path.Combine(_home, ".config", "anthropic");

        private ClaudeCredentialSource Describe()
            => ClaudeCredentials.Describe(name => _variables.GetValueOrDefault(name), _home, windowsAppData: null);

        private void SaveProfile(string name, bool signedIn = true, string? workspaceId = "wrkspc_1",
            string authenticationType = "user_oauth")
        {
            Directory.CreateDirectory(Path.Combine(ConfigDirectory, "configs"));
            var workspace = workspaceId is null ? "" : $"\"workspace_id\": \"{workspaceId}\",";
            File.WriteAllText(Path.Combine(ConfigDirectory, "configs", name + ".json"),
                $"{{ {workspace} \"organization_id\": \"org_1\", \"authentication\": {{ \"type\": \"{authenticationType}\" }} }}");
            if (signedIn)
            {
                Directory.CreateDirectory(Path.Combine(ConfigDirectory, "credentials"));
                File.WriteAllText(Path.Combine(ConfigDirectory, "credentials", name + ".json"),
                    $"{{ \"type\": \"oauth_token\", \"access_token\": \"{SecretToken}\" }}");
            }
        }

        private static void AssertNoSecret(ClaudeCredentialSource source)
        {
            var text = string.Join("\n", [source.Description, .. source.Warnings]);
            Assert.DoesNotContain(SecretKey, text, StringComparison.Ordinal);
            Assert.DoesNotContain(SecretToken, text, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("With nothing configured the source is None and says how to sign in")]
        public void Describe_NothingConfigured_ReturnsNone()
        {
            var source = Describe();

            Assert.Equal(ClaudeCredentialKind.None, source.Kind);
            Assert.Contains("ant auth login", source.Description, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("The default profile saved by ant auth login is reported with its workspace and organization")]
        public void Describe_DefaultProfile_ReturnsProfile()
        {
            SaveProfile("default");

            var source = Describe();

            Assert.Equal(ClaudeCredentialKind.Profile, source.Kind);
            Assert.Equal("default", source.ProfileName);
            Assert.Equal("wrkspc_1", source.WorkspaceId);
            Assert.Equal("org_1", source.OrganizationId);
            Assert.Empty(source.Warnings);
            AssertNoSecret(source);
        }

        [Fact]
        [DisplayName("The profile active_config names is used instead of default")]
        public void Describe_ActiveConfig_SelectsProfile()
        {
            SaveProfile("default");
            SaveProfile("polhem", workspaceId: "wrkspc_polhem");
            File.WriteAllText(Path.Combine(ConfigDirectory, "active_config"), "polhem\n");

            var source = Describe();

            Assert.Equal("polhem", source.ProfileName);
            Assert.Equal("wrkspc_polhem", source.WorkspaceId);
        }

        [Fact]
        [DisplayName("A profile without saved credentials is reported with a warning to sign in again")]
        public void Describe_ProfileWithoutCredentials_Warns()
        {
            SaveProfile("default", signedIn: false);

            var source = Describe();

            Assert.Equal(ClaudeCredentialKind.Profile, source.Kind);
            Assert.Contains(source.Warnings, warning => warning.Contains("ant auth login", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("An API key wins over a saved profile and the description warns that it hides the profile")]
        public void Describe_ApiKeyAndProfile_ApiKeyWinsWithWarning()
        {
            SaveProfile("default");
            _variables[ClaudeCredentials.ApiKeyVariable] = SecretKey;

            var source = Describe();

            Assert.Equal(ClaudeCredentialKind.ApiKey, source.Kind);
            Assert.Contains(source.Warnings, warning => warning.Contains("hides the profile 'default'", StringComparison.Ordinal));
            AssertNoSecret(source);
        }

        [Fact]
        [DisplayName("An API key also wins over a profile named by ANTHROPIC_PROFILE")]
        public void Describe_ApiKeyAndExplicitProfile_ApiKeyWins()
        {
            SaveProfile("polhem");
            _variables[ClaudeCredentials.ApiKeyVariable] = SecretKey;
            _variables[ClaudeCredentials.ProfileVariable] = "polhem";

            var source = Describe();

            Assert.Equal(ClaudeCredentialKind.ApiKey, source.Kind);
            Assert.Contains(source.Warnings, warning => warning.Contains("'polhem'", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("An empty ANTHROPIC_API_KEY is ignored, the profile is used, and a warning names the variable")]
        public void Describe_EmptyApiKey_FallsThroughWithWarning()
        {
            SaveProfile("default");
            _variables[ClaudeCredentials.ApiKeyVariable] = "";

            var source = Describe();

            Assert.Equal(ClaudeCredentialKind.Profile, source.Kind);
            Assert.Contains(source.Warnings, warning => warning.Contains(ClaudeCredentials.ApiKeyVariable, StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("ANTHROPIC_AUTH_TOKEN is reported as an access token and its value is not shown")]
        public void Describe_AuthToken_ReturnsAuthToken()
        {
            _variables[ClaudeCredentials.AuthTokenVariable] = SecretToken;

            var source = Describe();

            Assert.Equal(ClaudeCredentialKind.AuthToken, source.Kind);
            AssertNoSecret(source);
        }

        [Fact]
        [DisplayName("A profile named by ANTHROPIC_PROFILE that does not exist gives None with a warning")]
        public void Describe_MissingExplicitProfile_ReturnsNoneWithWarning()
        {
            SaveProfile("default");
            _variables[ClaudeCredentials.ProfileVariable] = "missing";

            var source = Describe();

            Assert.Equal(ClaudeCredentialKind.None, source.Kind);
            Assert.Contains(source.Warnings, warning => warning.Contains("'missing'", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("Workload identity federation variables are used before a profile on disk")]
        public void Describe_WorkloadIdentity_BeatsDiskProfile()
        {
            SaveProfile("default");
            _variables[ClaudeCredentials.FederationRuleVariable] = "fdrl_1";
            _variables[ClaudeCredentials.IdentityTokenFileVariable] = "/var/run/token";

            Assert.Equal(ClaudeCredentialKind.WorkloadIdentity, Describe().Kind);
        }

        [Fact]
        [DisplayName("ANTHROPIC_BASE_URL is reported as the address requests go to")]
        public void Describe_BaseUrlVariable_IsReported()
        {
            SaveProfile("default");
            _variables[ClaudeCredentials.BaseUrlVariable] = "https://proxy.example.test";

            Assert.Equal("https://proxy.example.test", Describe().BaseUrl);
        }

        [Fact]
        [DisplayName("ANTHROPIC_CONFIG_DIR moves where profiles are looked for")]
        public void Describe_ConfigDirVariable_IsUsed()
        {
            var elsewhere = Path.Combine(_home, "elsewhere");
            Directory.CreateDirectory(Path.Combine(elsewhere, "configs"));
            File.WriteAllText(Path.Combine(elsewhere, "configs", "default.json"),
                "{ \"authentication\": { \"type\": \"user_oauth\", \"credentials_path\": \"/nowhere.json\" } }");
            _variables[ClaudeCredentials.ConfigDirVariable] = elsewhere;

            var source = Describe();

            Assert.Equal(ClaudeCredentialKind.Profile, source.Kind);
            Assert.NotEmpty(source.Warnings);
        }

        [Fact]
        [DisplayName("A profile name that could leave the configuration folder is refused")]
        public void Describe_TraversalProfileName_IsRefused()
        {
            _variables[ClaudeCredentials.ProfileVariable] = "../outside";

            var source = Describe();

            Assert.Equal(ClaudeCredentialKind.None, source.Kind);
            Assert.Contains(source.Warnings, warning => warning.Contains("not a valid profile name", StringComparison.Ordinal));
        }

        [Fact]
        [DisplayName("A profile with an unsupported sign-in type gives None with a warning")]
        public void Describe_UnsupportedAuthenticationType_ReturnsNone()
        {
            SaveProfile("default", authenticationType: "something_else");

            var source = Describe();

            Assert.Equal(ClaudeCredentialKind.None, source.Kind);
            Assert.Contains(source.Warnings, warning => warning.Contains("something_else", StringComparison.Ordinal));
        }
    }
}
