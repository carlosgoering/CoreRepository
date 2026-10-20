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

## Tests

Each provider has its own test project. Core tests run without a database.

| Project | Coverage and requirements |
| --- | --- |
| `DataAccess.Core.Tests` | Entity metadata, repository delegation, write completion and error propagation; no Docker required. |
| `DataAccess.SQLServer.Tests` | CRUD, nullable values, date mapping, comparison and In filters, ordering, pagination, filtered totals and empty results; SQL Server 2022 via Testcontainers. |
| `DataAccess.Firestore.Tests` | CRUD, nullable values, date mapping, comparison and In filters, ordering, pagination, filtered totals and empty results; official Google Cloud CLI Firestore emulator via Testcontainers, without production credentials. |
| `DataAccess.PostgreSQL.Tests` | CRUD, filtering, pagination and schema mapping; PostgreSQL via Testcontainers. |
| `DataAccess.MongoDB.Tests` | CRUD, filtering, pagination and existence checks; MongoDB via Testcontainers. |
| `DataAccess.SQLite.Tests` | SQLite context integration tests using an in-memory database; no Docker required. |
| `DataAccess.MySQL.Tests` | CRUD, primary key uniqueness, nullable values, date mapping, comparison and In filters, combined filters, ordering, pagination and empty results; MySQL 8.4 via Testcontainers. |

Container tests require a running Docker daemon. The SQL Server image requires
an x86-64 environment or compatible container emulation. Docker is only a test
dependency, not a library dependency.

Run a specific suite:

```bash
dotnet test Tests/DataAccess.Core.Tests
dotnet test Tests/DataAccess.SQLServer.Tests
dotnet test Tests/DataAccess.Firestore.Tests
dotnet test Tests/DataAccess.MySQL.Tests
```

Run all solution tests:

```bash
dotnet test DataAccess.slnx
```

## Creating NuGet Packages After Tests

Run the pack script from the repository root (requires Python 3.9+, the .NET SDK,
and Docker for container tests):

```bash
python3 scripts/pack.py
```

The script runs all suites in `Release`, then generates packages independently:

* `DataAccess.Abstractions` and `DataAccess.Core` require the core suite to pass.
* Each provider requires both the core suite and its own suite to pass.
* Failed, aborted, skipped or missing tests leave the related packages pending.
  A successful command with zero tests is not considered approval.
* Pack failures also leave the package pending. Processing continues for the
  remaining packages.

Every invocation creates an isolated directory under `artifacts/pack/run-*`:

```text
run-*/
├── packages/       # Only packages generated by this run
├── tests/          # Per-suite TRX results and test.log
├── logs/           # Per-package pack logs
└── report.json     # Every package: generated or pending, with a reason
```

The script prints a summary and exits with code `1` if any package is pending,
even when other packages were generated successfully. Exit code `0` means all
packages were generated. Packages from earlier runs stay in their own directories.
No packages are published or uploaded.

Optional configuration and output parent directory:

```bash
python3 scripts/pack.py --configuration Release --output ./artifacts/validated-pack
```

Relative output paths are resolved from the current working directory. To retry
pending packages after fixing the failures, run the script again; it reruns the
suites and creates a fresh output directory.

The script's approval rules can be checked without Docker:

```bash
python3 -m unittest discover -s scripts -p 'test_*.py'
```

## Creating NuGet Packages Without Tests

The general pack command remains available and does not execute tests:

```bash
dotnet pack DataAccess.slnx -c Release -o ../nupkgs
```

This packages the library projects in the solution. Test projects are not packable.

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
