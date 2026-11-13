using System.ComponentModel;
using Polhem.Core.Exceptions;

namespace Polhem.Core.UnitTests.Exceptions
{
    /// <summary>
    /// Tests for the constructors of the access exceptions that carry a localizable message:
    /// <see cref="AuthenticationRequiredException"/>, <see cref="CompanyAccessDeniedException"/>,
    /// <see cref="CompanyNotEnteredException"/> and <see cref="ForbiddenException"/>.
    /// </summary>
    public class LocalizableExceptionConstructorTests
    {
        public static TheoryData<string> ExceptionNames =>
        [
            nameof(AuthenticationRequiredException),
            nameof(CompanyAccessDeniedException),
            nameof(CompanyNotEnteredException),
            nameof(ForbiddenException),
        ];

        [Theory]
        [MemberData(nameof(ExceptionNames))]
        [DisplayName("The message-only constructor sets the message and leaves the localization key empty")]
        public void Constructor_MessageOnly_HasNoKey(string name)
        {
            var ex = Create(name, "plain message");

            Assert.Equal("plain message", ex.Message);
            Assert.Equal(string.Empty, ((ILocalizableMessage)ex).MessageKey);
            Assert.Empty(((ILocalizableMessage)ex).MessageArguments);
            Assert.Null(ex.InnerException);
        }

        [Theory]
        [MemberData(nameof(ExceptionNames))]
        [DisplayName("The constructor with an inner exception keeps it")]
        public void Constructor_WithInner_KeepsInnerException(string name)
        {
            var inner = new InvalidOperationException("root cause");

            var ex = CreateWithInner(name, "outer", inner);

            Assert.Equal("outer", ex.Message);
            Assert.Same(inner, ex.InnerException);
        }

        [Theory]
        [MemberData(nameof(ExceptionNames))]
        [DisplayName("The keyed constructor formats the default message with its arguments and keeps both for translation")]
        public void Constructor_Keyed_FormatsMessageAndKeepsArguments(string name)
        {
            var ex = CreateKeyed(name, "Access.Denied", "Denied for {0}", ["acme"]);

            Assert.Equal("Denied for acme", ex.Message);
            Assert.Equal("Access.Denied", ((ILocalizableMessage)ex).MessageKey);
            Assert.Equal(["acme"], ((ILocalizableMessage)ex).MessageArguments);
        }

        [Theory]
        [MemberData(nameof(ExceptionNames))]
        [DisplayName("The keyed constructor given a null argument array keeps the default message and an empty argument list")]
        public void Constructor_KeyedNullArguments_UsesEmptyArguments(string name)
        {
            var ex = CreateKeyed(name, "Access.Denied", "Denied {0}", null!);

            Assert.Equal("Denied {0}", ex.Message);
            Assert.Empty(((ILocalizableMessage)ex).MessageArguments);
        }

        [Theory]
        [MemberData(nameof(ExceptionNames))]
        [DisplayName("The keyed constructor rejects a blank key")]
        public void Constructor_KeyedBlankKey_Throws(string name)
        {
            Assert.ThrowsAny<ArgumentException>(() => CreateKeyed(name, " ", "message", []));
        }

        private static Exception Create(string name, string message) => name switch
        {
            nameof(AuthenticationRequiredException) => new AuthenticationRequiredException(message),
            nameof(CompanyAccessDeniedException) => new CompanyAccessDeniedException(message),
            nameof(CompanyNotEnteredException) => new CompanyNotEnteredException(message),
            _ => new ForbiddenException(message),
        };

        private static Exception CreateWithInner(string name, string message, Exception inner) => name switch
        {
            nameof(AuthenticationRequiredException) => new AuthenticationRequiredException(message, inner),
            nameof(CompanyAccessDeniedException) => new CompanyAccessDeniedException(message, inner),
            nameof(CompanyNotEnteredException) => new CompanyNotEnteredException(message, inner),
            _ => new ForbiddenException(message, inner),
        };

        private static Exception CreateKeyed(string name, string key, string defaultMessage, object?[] arguments) => name switch
        {
            nameof(AuthenticationRequiredException) => new AuthenticationRequiredException(key, defaultMessage, arguments),
            nameof(CompanyAccessDeniedException) => new CompanyAccessDeniedException(key, defaultMessage, arguments),
            nameof(CompanyNotEnteredException) => new CompanyNotEnteredException(key, defaultMessage, arguments),
            _ => new ForbiddenException(key, defaultMessage, arguments),
        };
    }
}
