namespace Gorm.Application.Querying;
/// <summary>
/// Defines i graph includable queryable.
/// </summary>
#pragma warning disable S2326, S1694
public interface IGraphIncludableQueryable<out TEntity, out TProperty> : IQueryable<TEntity>
{
}
#pragma warning restore S2326, S1694