namespace Polhem.Core.UnitTests
{
    /// <summary>
    /// A plain enum with non-contiguous values, used as a fixture for the enum conversions in
    /// <see cref="ValueUtilities"/>.
    /// </summary>
    public enum TestInterval
    {
        Year = 0,
        Quarter = 1,
        Month = 2,
        Day = 4,
        Weekday = 6,
        Hour = 7,
        Minute = 8,
        Second = 9
    }
}
