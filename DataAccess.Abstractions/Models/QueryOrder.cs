namespace DataAccess.Abstractions.Models;

public sealed record QueryOrder(
    string Field,
    bool Descending = false);
