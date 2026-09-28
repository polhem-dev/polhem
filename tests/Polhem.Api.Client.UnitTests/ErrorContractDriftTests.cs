using System.ComponentModel;
using System.Globalization;
using Polhem.Api.Client.UnitTests.Connectors;
using Polhem.Api.Core.JsonRpc;
using Polhem.Base.Exceptions;

namespace Polhem.Api.Client.UnitTests
{
    /// <summary>
    /// Guards against drift between the two ends of the error contract.
    /// </summary>
    /// <remarks>
    /// On the server, <c>JsonRpcExecutor.MapException</c> maps exceptions to error codes; on the caller,
    /// <c>ApiConnector.FinalizeResponse</c> maps the codes back to exceptions. The two are inverses of each other,
    /// but **the compiler does not tie them together**: if the server returns a new code and the caller does not
    /// follow, that code silently falls into the generic branch, the <c>catch</c> promised by the exception type's
    /// doc is never entered again, and it still compiles and may well pass the tests.
    /// <para>
    /// This is not a hypothetical failure. <see cref="JsonRpcErrorCode.ReplayRejected"/> drifted exactly this way:
    /// the server threw <see cref="ReplayRejectedException"/> in several places and mapped it to -32005, while the
    /// caller was missing the branch entirely. This test is the automated check for the rule that both ends must
    /// agree.
    /// </para>
    /// <para>
    /// This test also requires **every error code to be classified**. Adding an enum member without classifying it
    /// turns this red. The purpose is to force a decision (does this code get a caller-side type) instead of
    /// letting it exist silently.
    /// </para>
    /// </remarks>
    public class ErrorContractDriftTests
    {
        /// <summary>
        /// Error codes with a dedicated exception type: the server maps that type to the code, and the caller must
        /// rebuild the code into that type.
        /// </summary>
        private static readonly (JsonRpcErrorCode Code, Type ExceptionType)[] s_reconstructedCodes =
        [
            (JsonRpcErrorCode.UserMessage, typeof(UserMessageException)),
            (JsonRpcErrorCode.PermissionDenied, typeof(ForbiddenException)),
            (JsonRpcErrorCode.CompanyAccessDenied, typeof(CompanyAccessDeniedException)),
            (JsonRpcErrorCode.CompanyNotEntered, typeof(CompanyNotEnteredException)),
            (JsonRpcErrorCode.ReplayRejected, typeof(ReplayRejectedException)),
            (JsonRpcErrorCode.Unauthorized, typeof(AuthenticationRequiredException)),
        ];

        /// <summary>
        /// Error codes deliberately not rebuilt: they arise outside the executor (transport or parsing layer), or
        /// they report a protocol mistake whose code already says everything and whose message is not meant for
        /// users (<see cref="JsonRpcErrorCode.MethodNotFound"/>, <see cref="JsonRpcErrorCode.InvalidParams"/>), so the
        /// caller always falls into the generic branch.
        /// </summary>
        private static readonly JsonRpcErrorCode[] s_transportOnlyCodes =
        [
            JsonRpcErrorCode.ParseError,
            JsonRpcErrorCode.InvalidRequest,
            JsonRpcErrorCode.MethodNotFound,
            JsonRpcErrorCode.InvalidParams,
            JsonRpcErrorCode.InternalError,
        ];

        /// <summary>
        /// Error codes that nothing in the repository currently produces. Known technical debt: it has not been
        /// decided whether to add a producer or remove the member.
        /// </summary>
        /// <remarks>
        /// Empty today. <see cref="JsonRpcErrorCode.Unauthorized"/> used to be listed here: it had never been on the
        /// wire. It now carries <see cref="AuthenticationRequiredException"/>. <see cref="JsonRpcErrorCode.MethodNotFound"/>
        /// and <see cref="JsonRpcErrorCode.InvalidParams"/> left when the executor started raising them. Putting a new
        /// code into this bucket to turn the test green is exactly what this test is meant to prevent.
        /// </remarks>
        private static readonly JsonRpcErrorCode[] s_noProducerCodes = [];

        /// <summary>
        /// The BCL exceptions that collapse, together with <see cref="UserMessageException"/>, into
        /// <see cref="JsonRpcErrorCode.UserMessage"/> — with a fixed message rather than their own.
        /// </summary>
        private static readonly Type[] s_userMessageWhitelist =
        [
            typeof(UnauthorizedAccessException),
            typeof(ArgumentException),
            typeof(ArgumentNullException),
            typeof(InvalidOperationException),
            typeof(NotSupportedException),
            typeof(FormatException),
        ];

        public static TheoryData<JsonRpcErrorCode, Type> ReconstructedCodes
        {
            get
            {
                var data = new TheoryData<JsonRpcErrorCode, Type>();
                foreach (var pair in s_reconstructedCodes) { data.Add(pair.Code, pair.ExceptionType); }
                return data;
            }
        }

        public static TheoryData<JsonRpcErrorCode> TransportOnlyCodes
        {
            get
            {
                var data = new TheoryData<JsonRpcErrorCode>();
                foreach (var code in s_transportOnlyCodes) { data.Add(code); }
                return data;
            }
        }

        public static TheoryData<Type> UserMessageWhitelist
        {
            get
            {
                var data = new TheoryData<Type>();
                foreach (var type in s_userMessageWhitelist) { data.Add(type); }
                return data;
            }
        }

        [Fact]
        [DisplayName("Every JsonRpcErrorCode member is classified exactly once (a new member cannot slip through)")]
        public void ErrorCodeClassification_CoversEveryMemberExactlyOnce()
        {
            var declared = Enum.GetValues<JsonRpcErrorCode>().ToHashSet();
            var classified = s_reconstructedCodes.Select(pair => pair.Code)
                .Concat(s_transportOnlyCodes)
                .Concat(s_noProducerCodes)
                .ToList();

            var unclassified = declared.Except(classified).ToList();
            var duplicated = classified.GroupBy(code => code)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            Assert.True(unclassified.Count == 0,
                $"These error codes are not classified; decide which category each belongs to: {string.Join(", ", unclassified)}");
            Assert.True(duplicated.Count == 0,
                $"These error codes are classified into more than one category: {string.Join(", ", duplicated)}");
            Assert.Empty(classified.Except(declared));
        }

        [Fact]
        [DisplayName("The classification buckets are not empty and each pins a representative member (so the classification check cannot become vacuous)")]
        public void ErrorCodeClassification_BucketsAreNotVacuous()
        {
            Assert.NotEmpty(s_reconstructedCodes);
            Assert.NotEmpty(s_transportOnlyCodes);
            Assert.NotEmpty(s_userMessageWhitelist);

            // Each bucket pins one representative member. If a whole bucket is emptied or moved, the `NotEmpty`
            // checks above do not catch it, but these do.
            Assert.Contains(s_reconstructedCodes, pair => pair.Code == JsonRpcErrorCode.UserMessage);
            Assert.Contains(JsonRpcErrorCode.InternalError, s_transportOnlyCodes);
            Assert.Contains(typeof(ArgumentException), s_userMessageWhitelist);
        }

        [Fact]
        [DisplayName("The registry lists derived types before their base types (otherwise the base type swallows the derived one)")]
        public void ErrorContract_DeclaresDerivedTypesBeforeTheirBaseTypes()
        {
            var rows = JsonRpcErrorContract.Rows;
            var shadowed = new List<string>();

            for (int i = 0; i < rows.Count; i++)
            {
                for (int j = i + 1; j < rows.Count; j++)
                {
                    // Matching uses `IsInstanceOfType` (assignability), so a base type listed earlier catches a
                    // derived type listed later, and that row never matches. This is not a style issue; the row is
                    // simply dead.
                    if (rows[i].ExceptionType != rows[j].ExceptionType
                        && rows[i].ExceptionType.IsAssignableFrom(rows[j].ExceptionType))
                    {
                        shadowed.Add($"{rows[j].ExceptionType.Name} (row {j}) is shadowed by {rows[i].ExceptionType.Name} (row {i})");
                    }
                }
            }

            Assert.True(shadowed.Count == 0,
                $"The registry order is wrong; these rows can never match: {string.Join("; ", shadowed)}");
        }

        [Fact]
        [DisplayName("Each rebuildable error code has exactly one rebuild type in the registry")]
        public void ErrorContract_DeclaresExactlyOneRebuildPerCode()
        {
            var duplicated = JsonRpcErrorContract.Rows
                .Where(row => row.CanRebuild)
                .GroupBy(row => row.Code)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            Assert.True(duplicated.Count == 0,
                $"These error codes declare more than one rebuild type, and the caller gets the one declared first: {string.Join(", ", duplicated)}");
        }

        [Fact]
        [DisplayName("The rebuildable codes in the registry match the specification declared in this test exactly")]
        public void ErrorContract_RebuildableCodes_MatchDeclaredSpecification()
        {
            // `s_reconstructedCodes` in this test is **the specification**, deliberately written by hand independently
            // of the implementation. If the test read the list of the code under test, it would only verify the
            // implementation against itself and prove nothing.
            var expected = s_reconstructedCodes
                .Select(pair => (pair.Code, pair.ExceptionType))
                .OrderBy(pair => (int)pair.Code)
                .ToList();
            var actual = JsonRpcErrorContract.Rows
                .Where(row => row.CanRebuild)
                .Select(row => (row.Code, row.ExceptionType))
                .OrderBy(pair => (int)pair.Code)
                .ToList();

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(ReconstructedCodes))]
        [DisplayName("The server maps each declared exception type to its declared error code and keeps the message as is")]
        public void MapException_DeclaredExceptionType_ReturnsDeclaredCode(
            JsonRpcErrorCode expectedCode, Type exceptionType)
        {
            const string message = "contract probe message";
            var exception = (Exception)Activator.CreateInstance(exceptionType, message)!;

            var (code, mappedMessage) = JsonRpcExecutor.MapException(exception);

            Assert.Equal(expectedCode, code);
            Assert.Equal(message, mappedMessage);
        }

        [Theory]
        [MemberData(nameof(ReconstructedCodes))]
        [DisplayName("The caller rebuilds each declared error code into its declared exception type without prefixing the message")]
        public async Task FinalizeResponse_DeclaredCode_RebuildsDeclaredExceptionType(
            JsonRpcErrorCode code, Type expectedExceptionType)
        {
            const string message = "contract probe message";

            var exception = await Record.ExceptionAsync(() =>
                ApiConnectorTestHost.ExecuteWithErrorAsync(code, message));

            Assert.NotNull(exception);
            Assert.IsType(expectedExceptionType, exception);
            Assert.Equal(message, exception.Message);
        }

        [Theory]
        [MemberData(nameof(TransportOnlyCodes))]
        [DisplayName("The caller sends deliberately unrebuilt error codes to the generic branch and keeps the code and the original message")]
        public async Task FinalizeResponse_TransportOnlyCode_FallsBackToGenericBranch(JsonRpcErrorCode code)
        {
            const string message = "transport level failure";

            var exception = await Record.ExceptionAsync(() =>
                ApiConnectorTestHost.ExecuteWithErrorAsync(code, message));

            var invalidOperation = Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains("API error", invalidOperation.Message);
            Assert.Contains(((int)code).ToString(CultureInfo.InvariantCulture), invalidOperation.Message);
            Assert.Contains(message, invalidOperation.Message);
        }

        [Theory]
        [MemberData(nameof(UserMessageWhitelist))]
        [DisplayName("Whitelisted BCL exceptions collapse into the UserMessage code together with UserMessageException (many-to-one is intended)")]
        public void MapException_WhitelistedBclException_CollapsesToUserMessage(Type exceptionType)
        {
            const string message = "whitelisted bcl message";
            var exception = (Exception)Activator.CreateInstance(exceptionType, message)!;

            var (code, _) = JsonRpcExecutor.MapException(exception);

            Assert.Equal(JsonRpcErrorCode.UserMessage, code);
        }

        [Fact]
        [DisplayName("Many-to-one is irreversible: a whitelisted BCL exception crosses the wire and is rebuilt as UserMessageException")]
        public async Task FinalizeResponse_UserMessageCode_AlwaysRebuildsUserMessageException()
        {
            // The server throws `InvalidOperationException`, but only an integer reaches the caller, and all it can
            // restore is the type that integer identifies. This is a deliberate trade-off, not a defect. The text
            // is whatever the server sent: outside debug mode that is the contract's fixed message, not the
            // exception's own (pinned in JsonRpcExecutorUserMessageExceptionTests), so this compares against the
            // mapped message rather than against either literal.
            var (code, message) = JsonRpcExecutor.MapException(new InvalidOperationException("state is wrong"));

            var exception = await Record.ExceptionAsync(() =>
                ApiConnectorTestHost.ExecuteWithErrorAsync(code, message));

            Assert.IsType<UserMessageException>(exception);
            Assert.Equal(message, exception.Message);
        }

        [Fact]
        [DisplayName("An authentication failure crosses the wire as Unauthorized (-32001) and is rebuilt as an UnauthorizedAccessException carrying its message")]
        public async Task AuthenticationFailure_RoundTripsAsUnauthorized()
        {
            var (code, message) = JsonRpcExecutor.MapException(
                new AuthenticationRequiredException("AccessToken is required or invalid."));

            Assert.Equal(JsonRpcErrorCode.Unauthorized, code);
            Assert.Equal(-32001, (int)code);
            Assert.Equal("AccessToken is required or invalid.", message);

            var exception = await Record.ExceptionAsync(() =>
                ApiConnectorTestHost.ExecuteWithErrorAsync(code, message));

            var unauthorized = Assert.IsType<UnauthorizedAccessException>(exception, exactMatch: false);
            Assert.IsType<AuthenticationRequiredException>(unauthorized);
            Assert.Equal("AccessToken is required or invalid.", unauthorized.Message);
        }

        [Fact]
        [DisplayName("A permission failure keeps its own code and is not reported as an authentication failure")]
        public void PermissionFailure_IsNotUnauthorized()
        {
            var (code, _) = JsonRpcExecutor.MapException(new ForbiddenException("Permission denied."));

            Assert.Equal(JsonRpcErrorCode.PermissionDenied, code);
        }
    }
}
