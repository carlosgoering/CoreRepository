namespace DataAccess.Abstractions.Models;

public class Database
{
    public string ConnectionString { get; set; }
    public string DatabaseName { get; set; }
    public string ConnectionKey { get; set; }
    public string ProjectId { get; set; }

    public Database()
    {
        ConnectionString = string.Empty;
        DatabaseName = string.Empty;
        ConnectionKey = string.Empty;
        ProjectId = string.Empty;
    }
}
