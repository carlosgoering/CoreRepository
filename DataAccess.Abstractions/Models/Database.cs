namespace DataAccess.Abstractions.Models;

public class Database
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string ConnectionKey { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;

}
