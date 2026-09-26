using System.ComponentModel;
using Polhem.Definition.Database;
using Polhem.Definition.Security;
using Polhem.LoadTests.Configuration;

namespace Polhem.LoadTests.UnitTests
{
    /// <summary>
    /// Tests for <see cref="LoadTestOptions"/>.
    /// </summary>
    public class LoadTestOptionsTests
    {
        private static LoadTestOptions CreateValid() => new()
        {
            Database = new DatabaseOptions { Provider = DatabaseType.SQLServer },
            Scenarios = [new ScenarioOptions { Name = "GetList", Enabled = true, Weight = 1 }]
        };

        [Fact]
        [DisplayName("Parse reads enums as strings and matches property names case-insensitively")]
        public void Parse_ReadsStringEnumsCaseInsensitively()
        {
            const string json = """
                {
                  "Target": { "mode": "Remote", "endpoint": "http://localhost:5000/api",
                              "protectionLevel": "Public" },
                  "load": { "virtualUsers": 25, "model": "Open" },
                  "auth": { "tokenStrategy": "Shared" },
                  "scenarios": [ { "name": "GetList", "enabled": true, "weight": 3 } ]
                }
                """;

            var options = LoadTestOptions.Parse(json);

            Assert.Equal(TargetMode.Remote, options.Target.Mode);
            Assert.Equal(ApiProtectionLevel.Public, options.Target.ProtectionLevel);
            Assert.Equal(LoadModel.Open, options.Load.Model);
            Assert.Equal(TokenStrategy.Shared, options.Auth.TokenStrategy);
            Assert.Equal(25, options.Load.VirtualUsers);
            Assert.Equal(3, options.Scenarios[0].Weight);
        }

        [Fact]
        [DisplayName("Parse allows comments and trailing commas")]
        public void Parse_AllowsCommentsAndTrailingCommas()
        {
            const string json = """
                {
                  // the sample file ships with comments
                  "scenarios": [ { "name": "GetList" }, ],
                }
                """;

            var options = LoadTestOptions.Parse(json);

            Assert.Single(options.Scenarios);
        }

        [Fact]
        [DisplayName("Parse uses default values for missing sections")]
        public void Parse_MissingSections_UseDefaults()
        {
            var options = LoadTestOptions.Parse("{}");

            Assert.Equal(TargetMode.Local, options.Target.Mode);
            Assert.Equal(DatabaseType.SQLServer, options.Database.Provider);
            Assert.Equal(LoadModel.Closed, options.Load.Model);
            Assert.Equal([50d, 95d, 99d], options.Report.Percentiles);
        }

        [Fact]
        [DisplayName("Validate always rejects SQLite, with a message that names it")]
        public void Validate_SqliteProvider_Throws()
        {
            var options = CreateValid();
            options.Database.Provider = DatabaseType.SQLite;

            var ex = Assert.Throws<InvalidOperationException>(options.Validate);
            Assert.Contains("SQLite", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Validate rejects Remote mode without an endpoint")]
        public void Validate_RemoteWithoutEndpoint_Throws()
        {
            var options = CreateValid();
            options.Target.Mode = TargetMode.Remote;

            Assert.Throws<InvalidOperationException>(options.Validate);
        }

        [Fact]
        [DisplayName("Validate accepts Local mode without an endpoint")]
        public void Validate_LocalWithoutEndpoint_Passes()
        {
            var options = CreateValid();
            options.Target.Mode = TargetMode.Local;

            options.Validate();
        }

        [Fact]
        [DisplayName("Validate rejects a configuration with no enabled scenario")]
        public void Validate_NoEnabledScenario_Throws()
        {
            var options = CreateValid();
            options.Scenarios[0].Enabled = false;

            Assert.Throws<InvalidOperationException>(options.Validate);
        }

        [Fact]
        [DisplayName("Validate requires a positive weight for an enabled scenario")]
        public void Validate_EnabledScenarioWithZeroWeight_Throws()
        {
            var options = CreateValid();
            options.Scenarios[0].Weight = 0;

            var ex = Assert.Throws<InvalidOperationException>(options.Validate);
            Assert.Contains("GetList", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        [DisplayName("Validate does not check the weight of a disabled scenario")]
        public void Validate_DisabledScenarioWithZeroWeight_Ignored()
        {
            var options = CreateValid();
            options.Scenarios.Add(new ScenarioOptions { Name = "Save", Enabled = false, Weight = 0 });

            options.Validate();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [DisplayName("Validate requires a positive number of virtual users")]
        public void Validate_NonPositiveVirtualUsers_Throws(int virtualUsers)
        {
            var options = CreateValid();
            options.Load.VirtualUsers = virtualUsers;

            Assert.Throws<InvalidOperationException>(options.Validate);
        }

        [Fact]
        [DisplayName("Validate allows a warm-up of 0 but rejects a negative one")]
        public void Validate_WarmupSeconds_ZeroAllowedNegativeRejected()
        {
            var options = CreateValid();

            options.Load.WarmupSeconds = 0;
            options.Validate();

            options.Load.WarmupSeconds = -1;
            Assert.Throws<InvalidOperationException>(options.Validate);
        }

        [Fact]
        [DisplayName("Validate rejects an empty database name prefix")]
        public void Validate_EmptyDatabaseNamePrefix_Throws()
        {
            var options = CreateValid();
            options.Database.DatabaseNamePrefix = "";

            var ex = Assert.Throws<InvalidOperationException>(options.Validate);
            Assert.Contains("unit tests", ex.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("load test_")]
        [InlineData("load-test_")]
        [InlineData("load];DROP DATABASE x--")]
        [DisplayName("Validate accepts only letters, digits and underscores in the prefix (the name goes into DDL)")]
        public void Validate_UnsafeDatabaseNamePrefix_Throws(string prefix)
        {
            var options = CreateValid();
            options.Database.DatabaseNamePrefix = prefix;

            Assert.Throws<InvalidOperationException>(options.Validate);
        }

        [Fact]
        [DisplayName("ResolveDatabaseName prepends the prefix to the CategoryId")]
        public void ResolveDatabaseName_PrependsPrefix()
        {
            var database = new DatabaseOptions { DatabaseNamePrefix = "loadtest_" };

            Assert.Equal("loadtest_company", database.ResolveDatabaseName("company"));
            Assert.Equal("loadtest_common", database.ResolveDatabaseName("common"));
        }

        [Fact]
        [DisplayName("ResolveServeUrl uses the loopback default port when the serve URL is not set")]
        public void ResolveServeUrl_NotConfigured_UsesLoopbackDefaultPort()
        {
            var target = new TargetOptions();

            Assert.Equal(
                $"http://localhost:{TargetOptions.DefaultServePort}", target.ResolveServeUrl());
        }

        [Fact]
        [DisplayName("ResolveServeUrl uses the configured serve URL as is")]
        public void ResolveServeUrl_Configured_UsesConfiguredValue()
        {
            var target = new TargetOptions { ServeUrl = "http://0.0.0.0:8080" };

            Assert.Equal("http://0.0.0.0:8080", target.ResolveServeUrl());
        }

        [Fact]
        [DisplayName("The sample configuration file parses and passes validation")]
        public void SampleConfiguration_ParsesAndValidates()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "loadtest.sample.json");
            Assert.True(File.Exists(path), $"Sample configuration not found at '{path}'.");

            var options = LoadTestOptions.FromFile(path);

            options.Validate();
            Assert.NotEmpty(options.Scenarios);
        }
    }
}
