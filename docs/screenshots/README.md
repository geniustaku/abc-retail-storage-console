# Screenshots

Evidence that the deployed application uses Azure Table Storage and Azure Blob Storage.
All captures are of the live site at <https://abcretail-gm-cldv7112.azurewebsites.net>.

| File | What it shows |
|---|---|
| `01-dashboard.png` | Entity and blob counts read live from the `stabcretailgm001` storage account |
| `02-products-blob-images.png` | Catalogue grid, every image served from the `product-images` blob container |
| `03-customers-table.png` | Customer profiles read from the `CustomerProfiles` table |
| `04-product-detail-blob-url.png` | One product showing its blob URL and blob name beside its table, partition key and row key |
| `05-product-create-form.png` | The form that writes a table entity and uploads a blob in a single submission |
| `06-storage-design.png` | The partition key and container design, and the reasoning behind it |

## ICE Task 2 - Azure Queues and Azure Files

| File | What it shows |
|---|---|
| `07-queue-monitor.png` | Both queues with live depth, and the messages waiting on them |
| `08-orders-queued.png` | Orders held at Submitted until their queue message is processed |
| `09-order-place-form.png` | The form that writes an entity, queues a message and logs a line |
| `10-file-share-logs.png` | The `application-logs` share, its two directories, and log contents read back |
| `11-dashboard-all-four.png` | Tables, Blobs, Queues and Files counted live on one dashboard |
| `12-storage-design.png` | The queue and file share rationale published in the site |

## Still to capture from the Azure Portal

These need a signed-in portal session, so they are taken by hand:

1. **Storage account overview** — `stabcretailgm001` in resource group `rg-abcretail-cldv7112`
2. **Storage browser, Tables, `CustomerProfiles`** — showing the entities and their PartitionKey and RowKey columns
3. **Storage browser, Tables, `Products`** — showing entities partitioned across the categories
4. **Storage browser, Blob containers, `product-images`** — showing the uploaded image blobs
5. **App Service, Environment variables** — showing `ConnectionStrings__AzureStorage` present with its value hidden

For Task 2, save these into `docs/azurescreenshots-task2`:

6. **Storage browser, Queues** — showing `order-processing` and `inventory-management`
7. **Queue messages** — click `inventory-management` to show the messages sitting on it
8. **Storage browser, File shares, `application-logs`** — showing the `logs` and `exports` directories
9. **Inside `logs`** — showing `app-YYYY-MM-DD.log` with its size and modified date

Sign in at <https://portal.azure.com> as `developer@drop-it.tech`.
