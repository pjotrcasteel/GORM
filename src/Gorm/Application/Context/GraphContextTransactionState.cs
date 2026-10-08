using System.Data.Common;
using Gorm.Application.Execution;

namespace Gorm.Application.Context;

internal sealed class GraphContextTransactionState
{
    public DbConnection? CurrentTransactionConnection { get; set; }

    public DbTransaction? CurrentTransaction { get; set; }

    public GraphTransaction? CurrentGraphTransaction { get; set; }

    public bool HasActiveTransaction =>
        CurrentTransactionConnection is not null &&
        CurrentTransaction is not null &&
        CurrentGraphTransaction is not null;

    public void Clear()
    {
        CurrentTransactionConnection = null;
        CurrentTransaction = null;
        CurrentGraphTransaction = null;
    }
}