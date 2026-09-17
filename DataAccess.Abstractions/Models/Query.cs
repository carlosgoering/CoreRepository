namespace DataAccess.Abstractions.Models;

public sealed record Query<TEntity>
{
    public IReadOnlyCollection<QueryFilter> Filters { get; init; } = [];

    public QueryOrder? Order { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int Skip => (Page - 1) * PageSize;

}