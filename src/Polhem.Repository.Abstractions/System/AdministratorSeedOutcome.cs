namespace Polhem.Repository.Abstractions.System
{
    /// <summary>
    /// What a seed wrote: each of the user, the company and the access between them is written only when it was
    /// missing.
    /// </summary>
    /// <param name="UserCreated">Whether the user was created.</param>
    /// <param name="CompanyCreated">Whether the company was created.</param>
    /// <param name="AccessGranted">Whether the user was given access to the company.</param>
    public readonly record struct AdministratorSeedOutcome(bool UserCreated, bool CompanyCreated, bool AccessGranted);
}
