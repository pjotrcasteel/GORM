namespace Gorm;

internal static class NonNullExtensions
{
    internal static T EnsureNotNull<T>(this T? value) where T : class
    {
        return value ?? throw new InvalidOperationException("A required value was null.");
    }
}