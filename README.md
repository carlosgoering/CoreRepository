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

DataAccess.MongoDB
DataAccess.Firestore
DataAccess.MySQL
DataAccess.PostgreSQL
DataAccess.SQLite
DataAccess.SQLServer
```

`DataAccess.Abstractions` contains the contracts and shared models used by consumers and providers.

`DataAccess.Core` contains database-independent repository functionality and entity metadata resolution.

Each provider project contains only the implementation and configuration required by its respective database.

---

## Entity Primary Key

Entities no longer need to implement a shared base interface such as `IBaseEntity`.

When an entity requires a primary key, it can identify the property using the `PrimaryKeyAttribute`:

```csharp
public class Project
{
    [PrimaryKey]
    public Guid ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;
}
```

The primary key is resolved by `EntityMetadata` in `DataAccess.Core`.

```csharp
var primaryKey = EntityMetadata.GetPrimaryKey<TEntity>();
```

The metadata is provider-independent. Each database provider can use the resolved primary key according to its own requirements.

For example:

* MongoDB uses the property as the BSON document ID.
* Firestore uses the property value as the document ID.
* SQLite uses the property as the database primary key.

This keeps the repository independent from a specific entity base class or fixed property name such as `Id`.

---

## appsettings.json

The consuming application is responsible for selecting the database provider and supplying its configuration.

```json
{
  "DBOfChoice": "Mongo",

  "MongoDBDatabase": {
    "ConnectionString": "...",
    "DatabaseName": "ProjectManagement"
  },

  "SQLiteDBDatabase": {
    "ConnectionString": "database.db",
    "ConnectionKey": "",
    "DatabaseName": "ProjectManagement"
  },

  "FirestoreDatabase": {
    "ProjectId": "...",
    "CredentialPath": "..."
  }
}
```

`DBOfChoice` currently supports:

* `Mongo`
* `Sqlite`
* `Firestore`

Only the configuration required by the selected provider needs to be supplied.

---

## Registering the Provider

Register the provider during application startup:

```csharp
builder.Services.AddDbProvider(builder.Configuration);
```

Example implementation:

```csharp
public static IServiceCollection AddDbProvider(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var dbChoice = configuration.GetValue<string>(Definitions.DBOfChoice);

    if (string.IsNullOrWhiteSpace(dbChoice))
        throw new InvalidOperationException(
            $"Configuration '{Definitions.DBOfChoice}' was not found.");

    if (!Enum.TryParse<Definitions.DataBases>(dbChoice, true, out var selectedDb))
        throw new InvalidOperationException(
            $"Unsupported database provider: {dbChoice}");

    var sectionName = selectedDb switch
    {
        Definitions.DataBases.Mongo => Definitions.MongoDbDatabaseSection,
        Definitions.DataBases.Sqlite => Definitions.SQLiteDbDatabaseSection,
        Definitions.DataBases.Firestore => Definitions.FirestoreDatabaseSection,

        _ => throw new InvalidOperationException(
            $"Unsupported database provider: {selectedDb}")
    };

    var section = configuration.GetSection(sectionName);

    if (!section.Exists())
        throw new InvalidOperationException(
            $"Configuration section '{sectionName}' was not found.");

    Action<Database> configure = options => section.Bind(options);

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

Repositories expose database-independent operations.

```csharp
await repository.InsertAsync(entity);

await repository.UpdateAsync(entity);

await repository.DeleteAsync(entity);
```

Provider-specific details remain inside each implementation.

For example, Firestore can construct a `DocumentReference` directly from the entity's primary key:

```csharp
var p = EntityMetadata
    .GetPrimaryKeyValue(entity)?
    .ToString();

var document = collection.Document(id);

await document.SetAsync(entity);
```

`Document(id)` creates a reference to the document. It does not perform a read before the update or delete operation.

MongoDB can use the same primary-key metadata to configure its BSON `_id` mapping and build filters for operations that require the entity key.

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

No application repository or service code changes are required.

---

## Supported Providers

Currently supported providers:

* MongoDB
* SQLite
* Firestore

Additional providers can be added as independent projects by implementing the required DataAccess abstractions and provider registration following the existing pattern:

```text
DataAccess.Abstractions
        ↑
DataAccess.Core
        ↑
┌───────┼────────┐
│       │        │
Mongo  SQLite  Firestore
```

The provider implementation is responsible for translating the database-independent repository operations into the API of the underlying database.

## Creating the NuGet Package

The library can be packaged as a NuGet package using:
```dotnet pack -c Release -o ../nupkgs```
The generated .nupkg files can be stored in a local folder and configured as a local NuGet package source.
For example:

```nupkgs/
├── DataAccess.Abstractions.x.x.x.nupkg
├── DataAccess.Core.x.x.x.nupkg
├── DataAccess.MongoDB.x.x.x.nupkg
├── DataAccess.Firestore.x.x.x.nupkg
└── DataAccess.SQLite.x.x.x.nupkg
```

The folder can then be added as a local NuGet source in the consuming project or development environment.
This allows the packages to be installed and tested as regular NuGet dependencies without publishing them to a public NuGet feed.
The project is currently being developed to provide a stable and database-agnostic version.
Package Version
The package version can be updated by changing the VersionPrefix property in Directory.Build.props:
```<VersionPrefix>x.x.x</VersionPrefix>```