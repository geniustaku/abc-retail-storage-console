# ABC Retail Storage Console

An ASP.NET Core MVC web application that runs ABC Retail's order processing on Azure
Storage. Customer, product and order records are held in **Azure Table Storage**; product
imagery is held in **Azure Blob Storage**; order and inventory work is queued on **Azure
Queue Storage**; and every action is logged to an **Azure Files** share.

**Genius Mhirizhonga** — CLDV7112, Cloud Development B, ICE Tasks 1 and 2.

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
- Reports live entity, blob, queue and log-file counts on the dashboard, read from Azure
  on each request
- Places customer orders and queues them on `order-processing` for a consumer to handle
- Queues catalogue and stock movements, including image uploads, on `inventory-management`
- Monitors both queues, and processes messages off them to advance orders and move stock
- Appends every action to a daily log file on the `application-logs` file share, and reads
  those logs back in the browser
- Writes inventory reports to the same share as CSV, downloadable from the site

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

### Orders

| | |
|---|---|
| Partition key | `ORDER` |
| Row key | Generated GUID |

An order is written as `Submitted` and only advances once its queue message has been
handled, so the queue drives the workflow rather than merely recording it.

## Queue design

Two queues, so that a backlog of one kind of work cannot delay the other:

| Queue | Carries |
|---|---|
| `order-processing` | Orders waiting to be picked, packed and dispatched |
| `inventory-management` | Catalogue and stock movements, including image uploads |

A queue message body is a string, so each one carries JSON holding a readable description
for the operator alongside the reference a consumer needs. One message therefore serves
both the monitoring screen and the processing code without a second lookup.

Receiving a message hides it for a visibility timeout rather than deleting it; it is
deleted only once the work has succeeded, so a consumer that fails part way through leaves
the message to reappear and be retried. That is at-least-once delivery, not exactly-once,
which is why the order handler checks whether an order is already complete before acting
on it.

## Log files on Azure Files

Every action is appended to `logs/app-YYYY-MM-DD.log` on the `application-logs` share, and
generated inventory reports are written to `exports/`. A file share was chosen over a blob
container because it is a real SMB file system: it can be mounted as a drive and read with
ordinary tools, which is what an operations team wants from a log.

Azure Files has no append operation of its own, unlike an append blob. Adding a line means
reading the file's current length, growing it by the size of the new bytes, and writing
those bytes into the range that growth created. That sequence is not atomic, so writers are
serialised within the application; across several instances the correct answer would be a
lease on the file.

A failure writing to the share is logged to the platform and swallowed. A customer losing
their order because the log file was busy would be the worse outcome.

### Ordering of writes

Creating a product uploads the blob before writing the table entity; deleting one removes
the blob before deleting the entity. Failing partway through therefore leaves an
unreferenced blob rather than a catalogue entry pointing at an image that does not exist,
which is the cheaper of the two failures to reconcile.

## Architecture

```
src/ABCRetail.Web
├── Controllers          Home, Customers, Products, Orders, Queues, Logs
├── Models               CustomerProfile, Product, Order (all implement ITableEntity)
├── Services
│   ├── ITableStorageService<T> / TableStorageService<T>    Azure.Data.Tables
│   ├── IBlobStorageService / BlobStorageService            Azure.Storage.Blobs
│   ├── IQueueStorageService / QueueStorageService          Azure.Storage.Queues
│   ├── IFileShareService / FileShareService                Azure.Storage.Files.Shares
│   └── IActivityRecorder / ActivityRecorder                pairs a queue write with a log write
├── Views                Razor views with a hand-written stylesheet
└── wwwroot              CSS and the upload preview script
```

All four storage services sit behind interfaces resolved through dependency injection.
Neither the controllers nor the views hold a reference to an Azure SDK type, which is why
adding Queues and Files for Task 2 was additive rather than a rewrite, and why replacing
the in-application queue consumer with a background worker or an Azure Function would need
no change to anything that produces messages.

Controllers do not talk to the queue and the file share separately. They call
`IActivityRecorder`, which writes the queue message and the log line together, so no caller
can queue work and forget to log it.

Every client is registered as a singleton. `TableClient`, `BlobContainerClient`,
`QueueClient` and `ShareClient` are all thread safe and pool their connections, so one
instance for the lifetime of the application avoids the socket exhaustion that comes from
constructing a client per request.

## Azure resources

| Resource | Name | SKU |
|---|---|---|
| Resource group | `rg-abcretail-cldv7112` | South Africa North |
| Storage account | `stabcretailgm001` | Standard LRS, StorageV2, Hot |
| Tables | `CustomerProfiles`, `Products`, `Orders` | |
| Blob container | `product-images` | Blob-level anonymous read |
| Queues | `order-processing`, `inventory-management` | |
| File share | `application-logs` | 5 GiB quota, `logs/` and `exports/` |
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
