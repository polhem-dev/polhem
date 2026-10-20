namespace Polhem.Db.Manager
{
    /// <summary>
    /// The outcome of <see cref="DatabaseCreator.EnsureExists"/>.
    /// </summary>
    public enum DatabaseCreateResult
    {
        /// <summary>The database did not exist and was created.</summary>
        Created,

        /// <summary>The database already existed; nothing was changed.</summary>
        AlreadyExists,

        /// <summary>
        /// The engine has nothing to create: an Oracle schema is created by a DBA, and an in-memory
        /// SQLite database exists only while a connection holds it open.
        /// </summary>
        NotApplicable,
    }
}
