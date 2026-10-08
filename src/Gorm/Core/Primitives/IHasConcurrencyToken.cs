namespace Gorm.Core.Primitives;

/// <summary>
/// Defines i has concurrency token.
/// </summary>
public interface IHasConcurrencyToken
{
    /// <summary>
    /// Gets or sets the version.
    /// </summary>
    public int Version { get; set; }
}