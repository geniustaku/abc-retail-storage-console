#!/usr/bin/env bash
#
# ABC Retail - Azure resource provisioning
# Author: Genius Mhirizhonga
# Module: CLDV7112 - Cloud Development B
#
# Creates every Azure resource the application needs. Safe to re-run: each command
# either creates the resource or reports the existing one.
#
# Usage: ./infra/provision.sh

set -euo pipefail

RESOURCE_GROUP="rg-abcretail-cldv7112"
LOCATION="southafricanorth"
STORAGE_ACCOUNT="stabcretailgm001"
APP_SERVICE_PLAN="asp-abcretail-free"
WEB_APP="abcretail-gm-cldv7112"
BLOB_CONTAINER="product-images"
FILE_SHARE="application-logs"

echo "Resource group"
az group create \
    --name "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --output none

echo "Storage account (Standard_LRS is the cheapest replication tier)"
az storage account create \
    --name "$STORAGE_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --sku Standard_LRS \
    --kind StorageV2 \
    --access-tier Hot \
    --allow-blob-public-access true \
    --min-tls-version TLS1_2 \
    --output none

CONNECTION_STRING=$(az storage account show-connection-string \
    --name "$STORAGE_ACCOUNT" \
    --resource-group "$RESOURCE_GROUP" \
    --output tsv --query connectionString)

echo "Tables"
az storage table create --name CustomerProfiles --connection-string "$CONNECTION_STRING" --output none
az storage table create --name Products --connection-string "$CONNECTION_STRING" --output none

echo "Blob container (blob-level anonymous read so image tags can address blobs directly)"
az storage container create \
    --name "$BLOB_CONTAINER" \
    --public-access blob \
    --connection-string "$CONNECTION_STRING" \
    --output none

echo "Queues"
az storage queue create --name order-processing --connection-string "$CONNECTION_STRING" --output none
az storage queue create --name inventory-management --connection-string "$CONNECTION_STRING" --output none

echo "File share for log files and generated reports"
az storage share create \
    --name "$FILE_SHARE" \
    --quota 5 \
    --connection-string "$CONNECTION_STRING" \
    --output none

az storage directory create --share-name "$FILE_SHARE" --name logs \
    --connection-string "$CONNECTION_STRING" --output none
az storage directory create --share-name "$FILE_SHARE" --name exports \
    --connection-string "$CONNECTION_STRING" --output none

echo "App Service plan (F1 is the free Linux tier)"
az appservice plan create \
    --name "$APP_SERVICE_PLAN" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    --is-linux \
    --sku F1 \
    --output none

echo "Web app"
az webapp create \
    --name "$WEB_APP" \
    --resource-group "$RESOURCE_GROUP" \
    --plan "$APP_SERVICE_PLAN" \
    --runtime "DOTNETCORE:10.0" \
    --output none

az webapp update \
    --name "$WEB_APP" \
    --resource-group "$RESOURCE_GROUP" \
    --https-only true \
    --output none

echo "Application settings"
# The double underscore is how the configuration provider expresses a colon, so this
# reaches the application as ConnectionStrings:AzureStorage. The key lives only here
# and in local user secrets, never in the repository.
az webapp config appsettings set \
    --name "$WEB_APP" \
    --resource-group "$RESOURCE_GROUP" \
    --settings \
        "ConnectionStrings__AzureStorage=$CONNECTION_STRING" \
        "AzureStorage__BlobContainer=$BLOB_CONTAINER" \
        "AzureStorage__FileShare=$FILE_SHARE" \
        "ASPNETCORE_ENVIRONMENT=Production" \
    --output none

echo
echo "Provisioning complete."
echo "Site:  https://${WEB_APP}.azurewebsites.net"
echo
echo "For local development, store the same key outside the repository:"
echo "  dotnet user-secrets --project src/ABCRetail.Web set \"ConnectionStrings:AzureStorage\" \"<connection string>\""
