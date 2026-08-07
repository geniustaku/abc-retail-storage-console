# ABC Retail Storage Console

An ASP.NET Core MVC web application that manages ABC Retail's customer profiles and
product catalogue on Azure Storage. Customer and product records are held in **Azure
Table Storage**; product imagery is held in **Azure Blob Storage** and served directly
from the storage account.

**Genius Mhirizhonga** — CLDV7112, Cloud Development B, ICE Task 1.

Live: <https://abcretail-gm-cldv7112.azurewebsites.net>

---

## The problem this addresses

ABC Retail ran order processing on ageing on-premises infrastructure. A single
relational database absorbed both the read traffic of customers browsing and the write
traffic of orders landing, which is why it buckled during the Christmas peak. Product
images sat on network shares that were slow to read and awkward to grow.

This application moves both concerns onto storage services that scale independently of
each other and of the web tier.

## What it does

- Creates, reads, updates and deletes customer profiles in the `CustomerProfiles` table
- Creates, reads, updates and deletes catalogue items in the `Products` table
- Uploads product images to the `product-images` blob container and links each blob to
  its product record
- Replaces and deletes blobs in step with the product they belong to
- Reports live entity and blob counts on the dashboard, read from Azure on each request

## Storage design

### CustomerProfiles

| | |
|---|---|
| Partition key | `CUSTOMER` |
| Row key | Generated GUID |

Every customer sits in one partition. At profile volumes that is comfortable, and it
keeps entity group transactions available across the whole table, since those are scoped
to a single partition. If the customer base outgrew one partition server, region would be
the natural key to split on, because fulfilment queries are already regional.

Search runs after the read rather than inside the query, because Table Storage has no
contains operator. That is honest at this scale and would be replaced by a dedicated
search index at a larger one.

### Products

| | |
|---|---|
| Partition key | Category |
| Row key | Generated GUID |

The catalogue is browsed one category at a time far more often than it is read whole, so
partitioning on category turns the dominant query into a single-partition scan rather
than a full table scan, and spreads writes across partition servers during a bulk load.
The cost is that the partition key is part of an entity's identity, so moving a product
between categories is a delete followed by an insert rather than an update in place. The
edit path handles that explicitly.

Prices are stored as `Double`. Table Storage has no decimal type.

### Blob Storage

Images never enter the table. Table Storage caps an entity at one megabyte and a single
property at sixty-four kilobytes, so imagery belongs in Blob Storage regardless. The
product record holds two fields: the blob URL, so a view can point an image tag straight
at it, and the blob name, so the blob can be replaced or removed when the product changes.

The container grants anonymous read at blob level. The browser then fetches images
directly from the storage account instead of routing bytes through the web application,
which keeps thumbnail traffic off the App Service compute budget entirely. Where the
imagery was not already public, shared access signatures would be the alternative, at the
cost of a signing round trip per image.

### Ordering of writes

Creating a product uploads the blob before writing the table entity; deleting one removes
the blob before deleting the entity. Failing partway through therefore leaves an
unreferenced blob rather than a catalogue entry pointing at an image that does not exist,
which is the cheaper of the two failures to reconcile.

## Architecture

```
src/ABCRetail.Web
├── Controllers          Home, Customers, Products
├── Models               CustomerProfile, Product (both implement ITableEntity)
├── Services
│   ├── ITableStorageService<T> / TableStorageService<T>    Azure.Data.Tables
│   └── IBlobStorageService / BlobStorageService            Azure.Storage.Blobs
├── Views                Razor views with a hand-written stylesheet
└── wwwroot              CSS and the upload preview script
```

Both storage services sit behind interfaces resolved through dependency injection.
Neither the controllers nor the views hold a reference to an Azure SDK type, so the queue
and file share work that follows is additive rather than a rewrite.

The clients are registered as singletons. `TableClient` and `BlobContainerClient` are
thread safe and pool their connections, so one instance for the lifetime of the
application avoids the socket exhaustion that comes from constructing a client per
request.

## Azure resources

| Resource | Name | SKU |
|---|---|---|
| Resource group | `rg-abcretail-cldv7112` | South Africa North |
| Storage account | `stabcretailgm001` | Standard LRS, StorageV2, Hot |
| App Service plan | `asp-abcretail-free` | F1, Linux |
| Web app | `abcretail-gm-cldv7112` | .NET 10 |

Standard LRS is the cheapest replication tier and F1 is free, which keeps the running
cost of the whole deployment to the few cents of storage the data actually occupies.

Provision everything with:

```bash
./infra/provision.sh
```

## Configuration

The storage connection string is never committed. `appsettings.json` carries the key with
an empty value purely to document that it is expected.

**Locally**, it is held in user secrets, which live outside the project directory:

```bash
dotnet user-secrets --project src/ABCRetail.Web set "ConnectionStrings:AzureStorage" "<connection string>"
```

**In Azure**, it is an App Service application setting named
`ConnectionStrings__AzureStorage`. The configuration provider reads a double underscore as
a colon, so the application resolves it as `ConnectionStrings:AzureStorage` with no code
difference between the two environments.

## Running locally

```bash
dotnet run --project src/ABCRetail.Web
```

The local run reads and writes the same Azure storage account as the deployed site.

## Deployment

Pushing to `main` triggers `.github/workflows/deploy.yml`, which restores, builds,
publishes and deploys to App Service. Deployment authenticates with a publish profile held
as the repository secret `AZURE_WEBAPP_PUBLISH_PROFILE`. The storage key is not involved,
because the deployed build reads it from its own application settings at start up.

## Screenshots

Evidence of both storage services in use is under [`docs/screenshots`](docs/screenshots).
