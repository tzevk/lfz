#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# LFZ deployment helper — builds, provisions and publishes both apps.
# Prerequisites: az CLI (logged in), dotnet SDK.
# Usage: ./deploy.sh <resource-group> [location]
# ---------------------------------------------------------------------------
set -euo pipefail

RG="${1:?Usage: ./deploy.sh <resource-group> [location]}"
LOCATION="${2:-westeurope}"
BASE_NAME="lfz-prod"

az group create --name "$RG" --location "$LOCATION" --output none

echo "Provisioning infrastructure…"
az deployment group create \
  --resource-group "$RG" \
  --template-file main.bicep \
  --parameters main.parameters.json \
  --query "properties.outputs" --output json

echo "Publishing LFZ.Api…"
dotnet publish ../src/LFZ.Api -c Release -o /tmp/lfz-api-publish
(cd /tmp/lfz-api-publish && zip -qr /tmp/lfz-api.zip .)
az webapp deploy --resource-group "$RG" --name "${BASE_NAME}-api" --src-path /tmp/lfz-api.zip --type zip

echo "Publishing LFZ.Web…"
dotnet publish ../src/LFZ.Web -c Release -o /tmp/lfz-web-publish
(cd /tmp/lfz-web-publish && zip -qr /tmp/lfz-web.zip .)
az webapp deploy --resource-group "$RG" --name "${BASE_NAME}-web" --src-path /tmp/lfz-web.zip --type zip

echo "Applying EF Core migrations…"
echo "Run: dotnet ef database update --project ../src/LFZ.Infrastructure --startup-project ../src/LFZ.Api --connection '<azure-sql-connection-string>'"

echo "Done."
