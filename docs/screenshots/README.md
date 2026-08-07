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

## Still to capture from the Azure Portal

These need a signed-in portal session, so they are taken by hand:

1. **Storage account overview** — `stabcretailgm001` in resource group `rg-abcretail-cldv7112`
2. **Storage browser, Tables, `CustomerProfiles`** — showing the entities and their PartitionKey and RowKey columns
3. **Storage browser, Tables, `Products`** — showing entities partitioned across the categories
4. **Storage browser, Blob containers, `product-images`** — showing the uploaded image blobs
5. **App Service, Environment variables** — showing `ConnectionStrings__AzureStorage` present with its value hidden

Sign in at <https://portal.azure.com> as `developer@drop-it.tech`.
