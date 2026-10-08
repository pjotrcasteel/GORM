using System.Reflection;

namespace Gorm.Infrastructure.Providers.SqlServer.Writers;

/// <summary>
/// Caches predicate method metadata used by SQL predicate writers.
/// </summary>
internal static class PredicateMethodCache
{
    public static readonly MethodInfo StringStartsWithMethod =
        typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;

    public static readonly MethodInfo StringEndsWithMethod =
        typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;

    public static readonly MethodInfo StringContainsMethod =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    public static readonly MethodInfo StringIsNullOrWhiteSpaceMethod =
        typeof(string).GetMethod(nameof(string.IsNullOrWhiteSpace), [typeof(string)])!;

    public static readonly MethodInfo StringIsNullOrEmptyMethod =
        typeof(string).GetMethod(nameof(string.IsNullOrEmpty), [typeof(string)])!;

    public static bool IsSupportedStaticContains(Type? declaringType) =>
        declaringType == typeof(Enumerable) ||
        declaringType == typeof(MemoryExtensions);
}