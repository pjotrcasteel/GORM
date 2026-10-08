using System.Collections;
using System.Reflection;
using Gorm.Core.Metadata;
using Gorm.Core.Primitives;

namespace Gorm.Application.Context;

internal sealed class NavigationFixupPlan
{
    private readonly GraphNavigationKind _kind;
    private readonly Type _elementType;
    private readonly Type _propertyType;
    private readonly Func<object, object?> _get;
    private readonly Action<object, object?>? _set;

    public NavigationFixupPlan(GraphNavigationKind kind, Type elementType, Type propertyType, Func<object, object?> get, Action<object, object?>? set)
    {
        _kind = kind;
        _elementType = elementType;
        _propertyType = propertyType;
        _get = get;
        _set = set;
    }

    public void Apply(Node owner, IReadOnlyList<object> items)
    {
        if (_kind == GraphNavigationKind.Reference)
        {
            _set!(owner, FindFirstTypedNode(items, _elementType));
            return;
        }

        var typedItems = CollectTypedNodes(items, _elementType);
        var currentValue = _get(owner);

        if (currentValue is not null && TryFillExistingCollection(currentValue, _elementType, typedItems))
        {
            return;
        }

        if (_set is null)
        {
            throw new InvalidOperationException(
                $"Collection navigation on type '{owner.GetType().FullName}' must either expose a mutable collection instance or be writable.");
        }

        _set(owner, CreateCollectionInstance(_propertyType, _elementType, typedItems));
    }

    private static Node? FindFirstTypedNode(IReadOnlyList<object> items, Type elementType)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is Node node && elementType.IsInstanceOfType(node))
            {
                return node;
            }
        }

        return null;
    }

    private static bool TryFillExistingCollection(object collection, Type elementType, List<Node> items)
    {
        if (collection is IList { IsFixedSize: false, IsReadOnly: false } list)
        {
            list.Clear();

            for (var i = 0; i < items.Count; i++)
            {
                list.Add(items[i]);
            }

            return true;
        }

        var clearMethod = collection.GetType().GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance);
        var addMethod = collection.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance, null, [elementType], null);

        if (addMethod is null)
        {
            return false;
        }

        clearMethod?.Invoke(collection, null);

        for (var i = 0; i < items.Count; i++)
        {
            addMethod.Invoke(collection, [items[i]]);
        }

        return true;
    }

    private static List<Node> CollectTypedNodes(IReadOnlyList<object> items, Type elementType)
    {
        var result = new List<Node>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is Node node && elementType.IsInstanceOfType(node))
            {
                result.Add(node);
            }
        }

        return result;
    }

    private static object CreateCollectionInstance(Type propertyType, Type elementType, List<Node> items)
    {
        if (propertyType.IsArray)
        {
            var array = Array.CreateInstance(elementType, items.Count);

            for (var i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }

            return array;
        }

        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;

        for (var i = 0; i < items.Count; i++)
        {
            list.Add(items[i]);
        }

        return list;
    }
}