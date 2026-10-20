namespace DataAccess.Abstractions.Models;

public sealed record QueryFilter(
  string Field,
  QueryOperator Operator,
  object? Value);
