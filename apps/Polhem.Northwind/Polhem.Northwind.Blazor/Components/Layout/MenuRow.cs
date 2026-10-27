namespace Polhem.Northwind.Blazor.Components.Layout;

/// <summary>
/// One row of the navigation menu: a folder header when <see cref="ProgId"/> is <c>null</c>, otherwise a form to open.
/// </summary>
/// <param name="Caption">The text shown.</param>
/// <param name="ProgId">The program the row opens, or <c>null</c> for a header.</param>
public sealed record MenuRow(string Caption, string? ProgId);
