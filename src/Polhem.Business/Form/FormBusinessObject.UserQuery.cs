using Polhem.Core.Exceptions;
using Polhem.Definition.Attributes;
using Polhem.Definition.Settings;
using Polhem.Definition.Language;
using Polhem.Definition.Security;
using Polhem.Repository.Abstractions.Factories;
using Polhem.Repository.Abstractions.Form;

namespace Polhem.Business.Form
{
    /// <summary>
    /// User-query half of FormBusinessObject: the query criteria a user saves under a name for this form, personal or
    /// shared with the company, and the one each user applies when the list opens.
    /// </summary>
    /// <remarks>
    /// Every action requires the form's <c>Read</c> permission, the same gate as <see cref="GetList"/>. Beyond that,
    /// only the user who created a query may overwrite, rename or delete it, while any user may make a shared one
    /// their default. The methods are not virtual, so a subclass cannot loosen the owner check. The stored criteria
    /// text is the front end's; it is kept as given and never read here.
    /// <para>
    /// NOTE: the writes declare no replay protection. Replaying one rewrites the same query or default, while
    /// declaring it would refuse the call from a client that does not write the wire frame whenever the host turns the
    /// frame on.
    /// </para>
    /// </remarks>
    public partial class FormBusinessObject
    {
        /// <summary>The longest name a user query may have, the length of its column.</summary>
        public const int UserQueryNameMaxLength = 100;

        /// <summary>The longest criteria text a user query may store.</summary>
        public const int UserQueryCriteriaMaxLength = 65536;

        /// <summary>
        /// Lists the user queries the caller can apply on this form: their own and the ones other users share, with
        /// the caller's default.
        /// </summary>
        /// <param name="args">The input arguments.</param>
        /// <remarks>
        /// A default that names a query the caller can no longer apply, because its owner stopped sharing it, is
        /// reported as none.
        /// </remarks>
        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
        public GetUserQueriesResult GetUserQueries(GetUserQueriesArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            Authorize(PermissionActions.Read);

            var list = CreateUserQueryRepository().GetQueries(ProgId, ResolveUserQueryUserId());
            bool defaultVisible = list.Table.Rows.Cast<global::System.Data.DataRow>()
                .Any(row => row["sys_rowid"] is Guid rowId && rowId == list.DefaultRowId);
            return new GetUserQueriesResult
            {
                Table = list.Table,
                DefaultRowId = defaultVisible ? list.DefaultRowId : Guid.Empty,
            };
        }

        /// <summary>
        /// Saves a new user query, or overwrites one the caller created.
        /// </summary>
        /// <param name="args">The input arguments.</param>
        /// <exception cref="UserMessageException">The name is missing or too long, the criteria are too long, or the
        /// query to overwrite does not exist on this form.</exception>
        /// <exception cref="ForbiddenException">The caller may not read the form, or did not create the query.</exception>
        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
        public SaveUserQueryResult SaveUserQuery(SaveUserQueryArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            Authorize(PermissionActions.Read);

            string name = (args.Name ?? string.Empty).Trim();
            if (name.Length == 0)
                throw new UserMessageException(PolhemMessages.UserQueryNameRequired, "A user query needs a name.");
            if (name.Length > UserQueryNameMaxLength)
                throw new UserMessageException(PolhemMessages.UserQueryNameTooLong,
                    "A user query name can have at most {0} characters.", UserQueryNameMaxLength);
            string criteria = args.Criteria ?? string.Empty;
            if (criteria.Length > UserQueryCriteriaMaxLength)
                throw new UserMessageException(PolhemMessages.UserQueryTooLarge, "The user query holds too many criteria to store.");

            string userId = ResolveUserQueryUserId();
            var repository = CreateUserQueryRepository();
            if (args.RowId == Guid.Empty)
            {
                var rowId = Guid.NewGuid();
                repository.Insert(rowId, ProgId, userId, name, args.IsShared, criteria);
                return new SaveUserQueryResult { RowId = rowId };
            }

            RequireOwnUserQuery(repository, args.RowId, userId);
            repository.Update(args.RowId, name, args.IsShared, criteria);
            return new SaveUserQueryResult { RowId = args.RowId };
        }

        /// <summary>
        /// Deletes a user query the caller created, and clears it as any user's default.
        /// </summary>
        /// <param name="args">The input arguments.</param>
        /// <exception cref="ForbiddenException">The caller may not read the form, or did not create the query.</exception>
        /// <remarks>A query that no longer exists is not an error: <see cref="DeleteUserQueryResult.RowsAffected"/> is zero.</remarks>
        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
        public DeleteUserQueryResult DeleteUserQuery(DeleteUserQueryArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            Authorize(PermissionActions.Read);

            var repository = CreateUserQueryRepository();
            var owner = repository.GetOwner(args.RowId);
            string userId = ResolveUserQueryUserId();
            if (owner is null || !IsVisible(owner, userId))
                return new DeleteUserQueryResult { RowsAffected = 0 };
            if (!IsOwnedBy(owner, userId))
                throw NotOwner();
            return new DeleteUserQueryResult { RowsAffected = repository.Delete(args.RowId) };
        }

        /// <summary>
        /// Sets the user query the caller applies when this form's list opens, or clears it.
        /// </summary>
        /// <param name="args">The input arguments.</param>
        /// <exception cref="UserMessageException">The query does not exist on this form, or the caller can not apply it.</exception>
        /// <exception cref="ForbiddenException">The caller may not read the form.</exception>
        [ApiAccessControl(ApiProtectionLevel.Public, ApiAccessRequirement.Authenticated)]
        public SetDefaultUserQueryResult SetDefaultUserQuery(SetDefaultUserQueryArgs args)
        {
            ArgumentNullException.ThrowIfNull(args);
            Authorize(PermissionActions.Read);

            string userId = ResolveUserQueryUserId();
            var repository = CreateUserQueryRepository();
            if (args.RowId != Guid.Empty)
            {
                var owner = repository.GetOwner(args.RowId);
                if (owner is null || !IsVisible(owner, userId))
                    throw NotFound();
            }
            repository.SetDefault(ProgId, userId, args.RowId);
            return new SetDefaultUserQueryResult { DefaultRowId = args.RowId };
        }

        private IUserQueryRepository CreateUserQueryRepository()
            => Services.GetRequiredService<IRepositoryFactory>().Create<IUserQueryRepository>(AccessToken);

        private string ResolveUserQueryUserId()
        {
            string? userId = SessionInfoService.Get(AccessToken)?.UserId;
            if (string.IsNullOrEmpty(userId))
                throw new AuthenticationRequiredException(PolhemMessages.SessionNotFound, "Session not found or has expired.");
            return userId;
        }

        private void RequireOwnUserQuery(IUserQueryRepository repository, Guid rowId, string userId)
        {
            var owner = repository.GetOwner(rowId);
            if (owner is null || !IsVisible(owner, userId))
                throw NotFound();
            if (!IsOwnedBy(owner, userId))
                throw NotOwner();
        }

        // A query on another form, or another user's personal query, is reported as missing rather than forbidden,
        // so the answer does not confirm that it exists.
        private bool IsVisible(UserQueryOwner owner, string userId)
            => string.Equals(owner.ProgId, ProgId, StringComparison.OrdinalIgnoreCase)
                && (owner.IsShared || IsOwnedBy(owner, userId));

        private static bool IsOwnedBy(UserQueryOwner owner, string userId)
            => string.Equals(owner.UserId, userId, StringComparison.Ordinal);

        private static UserMessageException NotFound()
            => new(PolhemMessages.UserQueryNotFound, "The user query does not exist.");

        private static ForbiddenException NotOwner()
            => new(PolhemMessages.UserQueryNotOwner, "Only the user who created a user query may change or delete it.");
    }
}
