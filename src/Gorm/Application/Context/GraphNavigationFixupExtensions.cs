using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;

namespace Gorm.Application.Context;

/// <summary>
/// Represents graph navigation fixup extensions.
/// </summary>
internal static class GraphNavigationFixupExtensions
{
    private static readonly ConcurrentDictionary<NavigationFixupPlanKey, NavigationFixupPlan> Plans = [];

    /// <summary>
    /// Executes apply navigation fixup.
    /// </summary>
    /// <param name="context">The graph context.</param>
    /// <param name="owner">The owner node.</param>
    /// <param name="relationshipName">The relationship name.</param>
    /// <param name="items">The items.</param>
    public static void ApplyNavigationFixup(this GraphContext context, Node owner, string relationshipName, IReadOnlyList<object> items)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(items);

        var navigations = context.Model.GetNavigationsForRelationship(owner.GetType(), relationshipName);

        for (var i = 0; i < navigations.Length; i++)
        {
            var navigation = navigations[i];

            var plan = Plans.GetOrAdd(
                new NavigationFixupPlanKey(owner.GetType(), navigation.PropertyName),
                static (_, state) => CreatePlan(state.OwnerType, state.Navigation),
                new NavigationFixupPlanState(owner.GetType(), navigation));

            plan.Apply(owner, items);
        }
    }

    private static NavigationFixupPlan CreatePlan(Type ownerType, GraphNavigationMapping navigation)
    {
        var property = ownerType.GetProperty(navigation.PropertyName, BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidOperationException(
                $"Navigation property '{navigation.PropertyName}' was not found on type '{ownerType.FullName}'.");

        var getter = CreateGetter(property);
        var setter = property.CanWrite ? CreateSetter(property) : null;

        if (navigation.Kind is GraphNavigationKind.Reference && setter is null)
        {
            throw new InvalidOperationException(
                $"Reference {nameof(navigation)} property '{navigation.PropertyName}' on type '{ownerType.FullName}' must be writable.");
        }

        return new NavigationFixupPlan(navigation.Kind, navigation.ElementType, property.PropertyType, getter, setter);
    }

    private static Func<object, object?> CreateGetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var access = Expression.Property(Expression.Convert(target, property.DeclaringType!), property);
        var box = Expression.Convert(access, typeof(object));

        return Expression.Lambda<Func<object, object?>>(box, target).Compile();
    }

    private static Action<object, object?> CreateSetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(object), "target");
        var value = Expression.Parameter(typeof(object), "value");
        var body = Expression.Assign(Expression.Property(Expression.Convert(target, property.DeclaringType!), property), Expression.Convert(value, property.PropertyType));

        return Expression.Lambda<Action<object, object?>>(body, target, value).Compile();
    }

    private readonly record struct NavigationFixupPlanKey(Type OwnerType, string PropertyName);

    private readonly record struct NavigationFixupPlanState(Type OwnerType, GraphNavigationMapping Navigation);
}