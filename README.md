# Using the DataAccess Library

The DataAccess library is database-agnostic.

The solution is divided into independent layers:

```text
DataAccess.Abstractions
    ├── Attributes
    ├── Interfaces
    └── Models

DataAccess.Core
    ├── Repository
    └── EntityMetadata

DataAccess.Firestore
DataAccess.MongoDB
DataAccess.MySQL
DataAccess.PostgreSQL
DataAccess.SQLite
DataAccess.SQLServer
```

`DataAccess.Abstractions` contains the contracts and shared models used by consumers and providers.

`DataAccess.Core` contains database-independent repository functionality and entity metadata resolution.

Each provider project contains only the implementation and configuration required by its respective database.

---

## Installation

### Requirements

The application using DataAccess must have:

* .NET SDK compatible with the library target framework.
* The database engine required by the selected provider.
* The corresponding DataAccess provider package.

The application only needs to install the provider it intends to use.

### Install the Core Packages

```bash
dotnet add package DataAccess.Abstractions
dotnet add package DataAccess.Core
```

### Install a Database Provider

#### MongoDB

```bash
dotnet add package DataAccess.MongoDB
```

#### SQLite

```bash
dotnet add package DataAccess.SQLite
```

#### Firestore

```bash
dotnet add package DataAccess.Firestore
```

#### MySQL

```bash
dotnet add package DataAccess.MySQL
```

#### PostgreSQL

```bash
dotnet add package DataAccess.PostgreSQL
```

#### SQL Server

```bash
dotnet add package DataAccess.SQLServer
```

For example, an application using PostgreSQL requires:

```bash
dotnet add package DataAccess.Abstractions
dotnet add package DataAccess.Core
dotnet add package DataAccess.PostgreSQL
```

---

## Quick Start

The following example uses PostgreSQL and demonstrates the minimum setup required to start using the library.

### 1. Create a project

Create a new ASP.NET Core application:

```bash
dotnet new webapi -n MyApplication
cd MyApplication
```

Install the required packages:

```bash
dotnet add package DataAccess.Abstractions
dotnet add package DataAccess.Core
dotnet add package DataAccess.PostgreSQL
```

---

### 2. Create an Entity

Create an entity with a property marked with `[PrimaryKey]`:

```csharp
using DataAccess.Abstractions.Attributes;

public class Project
{
    [PrimaryKey]
    public Guid ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
```

No base class or `IBaseEntity` implementation is required.

---

### 3. Configure the Database

Add the selected provider to `appsettings.json`:

```json
{
  "DBOfChoice": "PostgreSQL",

  "PostgreSQLDatabase": {
    "ConnectionString": "Host=localhost;Port=5432;Database=YourDatabase;Username=postgres;Password=your_password;"
  }
}
```

Only the configuration for the selected provider is required.

---

### 4. Register DataAccess

In `Program.cs`:

```csharp
builder.Services.AddDbProvider(
    builder.Configuration);
```

The provider registration reads `DBOfChoice`, loads the corresponding configuration section, and registers the required DataAccess services.

No manual registration of `IRepository<TEntity>` or `IDataAccessContext<TEntity>` is required.

---

### 5. Inject the Repository

A service can now consume the database-independent repository:

```csharp
using DataAccess.Abstractions.Interfaces;

public class ProjectService
{
    private readonly IRepository<Project> repository;

    public ProjectService(
        IRepository<Project> repository)
    {
        this.repository = repository;
    }

    public async Task CreateAsync(Project project)
    {
        await repository.InsertAsync(project);
    }
}
```

---

### 6. Insert an Entity

```csharp
var project = new Project
{
    ProjectId = Guid.NewGuid(),
    Name = "My Project",
    CreatedAt = DateTime.UtcNow
};

await repository.InsertAsync(project);
```

The PostgreSQL provider translates this operation into the corresponding SQL statement.

The application does not need to know how the entity is persisted.

---

### 7. Query an Entity

Queries are created using the database-independent `Query<TEntity>` model:

```csharp
var project = await repository.FirstOrDefaultAsync(
    new Query<Project>
    {
        Filters =
        [
            new QueryFilter(
                nameof(Project.ProjectId),
                QueryOperator.Equal,
                projectId)
        ]
    });
```

The provider translates the query into the appropriate database-specific operation.

---

### 8. Update an Entity

```csharp
project.Name = "Updated Project";

await repository.UpdateAsync(project);
```

---

### 9. Delete an Entity

```csharp
await repository.DeleteAsync(project);
```

---

### 10. Use Filtering and Pagination

```csharp
var result = await repository.SelectPagedAsync(
    new Query<Project>
    {
        Filters =
        [
            new QueryFilter(
                nameof(Project.Name),
                QueryOperator.Equal,
                "My Project")
        ],

        Order = new QueryOrder(
            nameof(Project.CreatedAt),
            Descending: true),

        Page = 1,
        PageSize = 20
    });
```

The result contains the selected entities together with pagination metadata:

```csharp
result.Items
result.Page
result.PageSize
result.TotalCount
```

At this point the application is using the same repository API regardless of the selected database provider.

---

## Entity Primary Key

Entities do not need to implement a shared base interface such as `IBaseEntity`.

When an entity requires a primary key, it can identify the property using the `PrimaryKeyAttribute`:

```csharp
using DataAccess.Abstractions.Attributes;

public class Project
{
    [PrimaryKey]
    public Guid ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;
}
```

The primary key is resolved by `EntityMetadata` in `DataAccess.Core`:

```csharp
var primaryKey = EntityMetadata.GetPrimaryKey<TEntity>();
```

The metadata is provider-independent. Each database provider can use the resolved primary key according to its own requirements.

For example:

* MongoDB uses the property as the BSON document ID.
* Firestore uses the property value as the document ID.
* SQLite uses the property as the database primary key.
* Relational providers use the property as the table primary key.

This keeps the repository independent from a specific entity base class or fixed property name such as `Id`.

---

## Configuration

The consuming application is responsible for selecting the database provider and supplying its configuration.

### appsettings.json

```json
{
  "DBOfChoice": "PostgreSQL",

  "MongoDBDatabase": {
    "ConnectionString": "...",
    "DatabaseName": "YourDatabase"
  },

  "SQLiteDBDatabase": {
    "ConnectionString": "YourDatabase.db",
    "ConnectionKey": "",
    "DatabaseName": "YourDatabase"
  },

  "FirestoreDatabase": {
    "ProjectId": "...",
    "CredentialPath": "..."
  },

  "MySQLDatabase": {
    "ConnectionString": "...",
    "DatabaseName": "YourDatabase"
  },

  "PostgreSQLDatabase": {
    "ConnectionString": "...",
    "DatabaseName": "YourDatabase"
  },

  "SQLServerDatabase": {
    "ConnectionString": "..."
  }
}
```

`DBOfChoice` currently supports:

* `Mongo`
* `Sqlite`
* `Firestore`
* `MySQL`
* `PostgreSQL`
* `SQLServer`

Only the configuration required by the selected provider needs to be supplied.

---

## Registering the Provider

Register the provider during application startup:

```csharp
builder.Services.AddDbProvider(
    builder.Configuration);
```

Example implementation:

```csharp
public static IServiceCollection AddDbProvider(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var dbChoice = configuration.GetValue<string>(
        Definitions.DBOfChoice);

    if (string.IsNullOrWhiteSpace(dbChoice))
        throw new InvalidOperationException(
            $"Configuration '{Definitions.DBOfChoice}' was not found.");

    if (!Enum.TryParse<Definitions.DataBases>(
            dbChoice,
            true,
            out var selectedDb))
    {
        throw new InvalidOperationException(
            $"Unsupported database provider: {dbChoice}");
    }

    var sectionName = selectedDb switch
    {
        Definitions.DataBases.Mongo =>
            Definitions.MongoDbDatabaseSection,

        Definitions.DataBases.Sqlite =>
            Definitions.SQLiteDbDatabaseSection,

        Definitions.DataBases.Firestore =>
            Definitions.FirestoreDatabaseSection,

        Definitions.DataBases.MySQL =>
            Definitions.MySQLDatabaseSection,

        Definitions.DataBases.PostgreSQL =>
            Definitions.PostgreSQLDatabaseSection,

        Definitions.DataBases.SQLServer =>
            Definitions.SQLServerDatabaseSection,

        _ => throw new InvalidOperationException(
            $"Unsupported database provider: {selectedDb}")
    };

    var section = configuration.GetSection(sectionName);

    if (!section.Exists())
        throw new InvalidOperationException(
            $"Configuration section '{sectionName}' was not found.");

    Action<Database> configure = options =>
        section.Bind(options);

    switch (selectedDb)
    {
        case Definitions.DataBases.Mongo:
            services.AddMongo(configure);
            break;

        case Definitions.DataBases.Sqlite:
            services.AddSqlite(configure);
            break;

        case Definitions.DataBases.Firestore:
            services.AddFirestore(configure);
            break;

        case Definitions.DataBases.MySQL:
            services.AddMySQL(configure);
            break;

        case Definitions.DataBases.PostgreSQL:
            services.AddPostgreSQL(configure);
            break;

        case Definitions.DataBases.SQLServer:
            services.AddSQLServer(configure);
            break;
    }

    return services;
}
```

`AddDbProvider()` is responsible for:

* Reading the selected provider from `DBOfChoice`.
* Validating that the provider is supported.
* Validating that the corresponding configuration section exists.
* Binding the provider configuration.
* Initializing the selected provider.

Each provider registers its required dependencies internally, including:

* `IDataAccessContext<TEntity>`
* `IRepository<TEntity>`
* Provider-specific services

The consuming application does not need to register repositories, contexts, or provider-specific services manually.

---

## Using the Repository

After registering the provider, repositories can be injected normally:

```csharp
public class ProjectService : IProjectService
{
    private readonly IRepository<Project> repository;

    public ProjectService(IRepository<Project> repository)
    {
        this.repository = repository;
    }
}
```

The application only depends on the abstractions:

```text
Application
    ↓
DataAccess.Abstractions
    ↓
DataAccess.Core
    ↓
Selected Provider
```

The application does not need to directly reference provider-specific repository implementations.

---

## Repository Operations

Repositories expose database-independent operations:

```csharp
await repository.InsertAsync(entity);

await repository.UpdateAsync(entity);

await repository.DeleteAsync(entity);
```

Query operations are also database-independent:

```csharp
var entity = await repository.FirstOrDefaultAsync(
    new Query<Project>
    {
        Filters =
        [
            new QueryFilter(
                nameof(Project.ProjectId),
                QueryOperator.Equal,
                projectId)
        ]
    });
```

Provider-specific details remain inside each implementation.

For example, Firestore can construct a `DocumentReference` directly from the entity's primary key:

```csharp
var id = EntityMetadata
    .GetPrimaryKeyValue(entity)?
    .ToString();

var document = collection.Document(id);

await document.SetAsync(entity);
```

`Document(id)` creates a reference to the document. It does not perform a read before the update or delete operation.

MongoDB can use the same primary-key metadata to configure its BSON `_id` mapping and build filters for operations that require the entity key.

Relational providers translate the database-independent repository operations into SQL appropriate for their respective database engines.

---

## Switching Providers

Changing the database provider only requires updating the application configuration.

### MongoDB

```json
{
  "DBOfChoice": "Mongo"
}
```

### SQLite

```json
{
  "DBOfChoice": "Sqlite"
}
```

### Firestore

```json
{
  "DBOfChoice": "Firestore"
}
```

### MySQL

```json
{
  "DBOfChoice": "MySQL"
}
```

### PostgreSQL

```json
{
  "DBOfChoice": "PostgreSQL"
}
```

### SQL Server

```json
{
  "DBOfChoice": "SQLServer"
}
```

No application repository or service code changes are required.

---

## Supported Providers

Currently supported providers:

* MongoDB
* SQLite
* Firestore
* MySQL
* PostgreSQL
* SQL Server

Additional providers can be added as independent projects by implementing the required DataAccess abstractions and provider registration following the existing pattern:

```text
DataAccess.Abstractions
        ↑
DataAccess.Core
        ↑
┌────────┼──────────┬─────────┬────────────┬────────────┐
│        │          │         │            │            │
Mongo  SQLite  Firestore    MySQL     PostgreSQL   SQL Server
```

The provider implementation is responsible for translating the database-independent repository operations into the API of the underlying database.

---

## Integration Tests

Each database provider has its own test project.

```text
DataAccess.Core.Tests
DataAccess.Firestore.Tests
DataAccess.MongoDB.Tests
DataAccess.MySQL.Tests
DataAccess.PostgreSQL.Tests
DataAccess.SQLite.Tests
DataAccess.SQLServer.Tests
```

Provider tests validate the actual database implementation, including:

* Schema creation
* Primary key configuration
* Nullable and non-nullable columns
* Data type mapping
* Insert
* Select
* Update
* Delete
* Filtering
* Pagination
* Existence checks

PostgreSQL integration tests use Testcontainers to run an isolated PostgreSQL instance during test execution.

Docker is therefore required for the PostgreSQL integration tests, but it is not a dependency of the `DataAccess.PostgreSQL` library itself.

---

## Creating the NuGet Package

The library can be packaged as a NuGet package using:

```bash
dotnet pack -c Release -o ../nupkgs
```

The generated `.nupkg` files can be stored in a local folder and configured as a local NuGet package source.

For example:

```text
nupkgs/
├── DataAccess.Abstractions.x.x.x.nupkg
├── DataAccess.Core.x.x.x.nupkg
├── DataAccess.MongoDB.x.x.x.nupkg
├── DataAccess.Firestore.x.x.x.nupkg
├── DataAccess.SQLite.x.x.x.nupkg
├── DataAccess.MySQL.x.x.x.nupkg
├── DataAccess.PostgreSQL.x.x.x.nupkg
└── DataAccess.SQLServer.x.x.x.nupkg
```

The folder can then be added as a local NuGet source in the consuming project or development environment.

This allows the packages to be installed and tested as regular NuGet dependencies without publishing them to a public NuGet feed.

The project is currently being developed to provide a stable and database-agnostic version.

### Package Version

The package version can be updated by changing the `VersionPrefix` property in `Directory.Build.props`:

```xml
<VersionPrefix>x.x.x</VersionPrefix>
```
