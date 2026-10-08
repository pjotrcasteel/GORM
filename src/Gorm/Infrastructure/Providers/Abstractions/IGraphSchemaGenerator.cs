using Gorm.Core.Models;

namespace Gorm.Infrastructure.Providers.Abstractions;

/// <summary>
/// Defines i graph schema generator.
/// </summary>
public interface IGraphSchemaGenerator
{
    /// <summary>
    /// Executes generate create script.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <returns>The value.</returns>
    public string GenerateCreateScript(GraphModel model);
}