using DataAccess.Abstractions.Interfaces;
using DataAccess.Abstractions.Models;
using DataAccess.Core.Metadata;
using Google.Cloud.Firestore;

namespace DataAcess.Firestore.Context;

/// <summary>
/// If you want to know more about Firestore, please visit: https://docs.cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest
/// </summary>
/// <typeparam name="TEntity"></typeparam>
internal sealed class DataAccessContext<TEntity> : IDataAccessContext<TEntity>
    where TEntity : class, new()
{
    private readonly FirestoreDb database;
    private readonly CollectionReference collection;

    public DataAccessContext(
        FirestoreDb database)
    {
        this.database = database;

        collection = database.Collection(
            typeof(TEntity).Name);
    }

    public async Task InsertAsync(TEntity entity)
    {
        var pk = EntityMetadata.GetPrimaryKeyValue(entity)?.ToString();

        var document = collection.Document(pk);

        await document.CreateAsync(entity);
    }

    public async Task UpdateAsync(TEntity entity)
    {
        var pk = EntityMetadata.GetPrimaryKeyValue(entity)?.ToString();

        var document = collection.Document(pk);

        await document.SetAsync(entity);
    }

    public async Task DeleteAsync(TEntity entity)
    {
        var pk = EntityMetadata.GetPrimaryKeyValue(entity)?.ToString();

        var document = collection.Document(pk);

        await document.DeleteAsync();
    }

    public async Task<TEntity?> FirstOrDefaultAsync(
        Query<TEntity> query)
    {
        var firestoreQuery = BuildQuery(query);

        firestoreQuery = firestoreQuery.Limit(1);

        var snapshot = await firestoreQuery.GetSnapshotAsync();

        return snapshot.Documents.Count == 0
            ? null
            : snapshot.Documents[0].ConvertTo<TEntity>();
    }

    public async Task<IReadOnlyCollection<TEntity>> SelectAsync(
        Query<TEntity> query)
    {
        var firestoreQuery = BuildQuery(query);

        firestoreQuery = firestoreQuery
            .Offset(query.Skip)
            .Limit(query.PageSize);

        var snapshot = await firestoreQuery.GetSnapshotAsync();

        return snapshot.Documents
            .Select(x => x.ConvertTo<TEntity>())
            .ToList();
    }

    public async Task<PagedResult<TEntity>> SelectPagedAsync(
        Query<TEntity> query)
    {
        var firestoreQuery = BuildQuery(query);

        var countSnapshot = await firestoreQuery
            .Count()
            .GetSnapshotAsync();

        var total = countSnapshot.Count ?? 0;

        var snapshot = await firestoreQuery
            .Offset(query.Skip)
            .Limit(query.PageSize)
            .GetSnapshotAsync();

        var items = snapshot.Documents
            .Select(x => x.ConvertTo<TEntity>())
            .ToList();

        return new PagedResult<TEntity>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = checked((int)total)
        };
    }

    public async Task<long> CountAsync(
        Query<TEntity>? query = null)
    {
        var firestoreQuery = query is null
            ? collection
            : BuildQuery(query);

        var snapshot = await firestoreQuery
            .Count()
            .GetSnapshotAsync();

        return snapshot.Count ?? 0;
    }

    public async Task<bool> ExistsAsync(
        Query<TEntity> query)
    {
        var firestoreQuery = BuildQuery(query)
            .Limit(1);

        var snapshot = await firestoreQuery.GetSnapshotAsync();

        return snapshot.Documents.Count > 0;
    }

    private Google.Cloud.Firestore.Query BuildQuery(
        Query<TEntity> query)
    {
        Google.Cloud.Firestore.Query firestoreQuery =
            collection;

        foreach (var filter in query.Filters)
        {
            firestoreQuery = ApplyFilter(
                firestoreQuery,
                filter);
        }

        if (query.Order is not null)
        {
            firestoreQuery = query.Order.Descending
                ? firestoreQuery.OrderByDescending(
                    query.Order.Field)
                : firestoreQuery.OrderBy(
                    query.Order.Field);
        }

        return firestoreQuery;
    }

    private static Google.Cloud.Firestore.Query ApplyFilter(
        Google.Cloud.Firestore.Query query,
        QueryFilter filter)
    {
        return filter.Operator switch
        {
            QueryOperator.Equal =>
                query.WhereEqualTo(
                    filter.Field,
                    filter.Value),

            QueryOperator.NotEqual =>
                query.WhereNotEqualTo(
                    filter.Field,
                    filter.Value),

            QueryOperator.GreaterThan =>
                query.WhereGreaterThan(
                    filter.Field,
                    filter.Value),

            QueryOperator.GreaterThanOrEqual =>
                query.WhereGreaterThanOrEqualTo(
                    filter.Field,
                    filter.Value),

            QueryOperator.LessThan =>
                query.WhereLessThan(
                    filter.Field,
                    filter.Value),

            QueryOperator.LessThanOrEqual =>
                query.WhereLessThanOrEqualTo(
                    filter.Field,
                    filter.Value),

            QueryOperator.In =>
               query.WhereIn(
                   filter.Field,
                   filter.Value as IEnumerable<object>
                       ?? throw new ArgumentException(
                       "The value of an 'In' filter must be a collection.")),

            _ => throw new NotSupportedException(
                $"Operator '{filter.Operator}' is not supported by Firestore.")
        };

    }
}