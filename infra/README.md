# Infrastructure (Bicep)

Declarative Azure resources for every environment. PR-04 ships the **dev** environment; staging and prod copies follow in Phase 5.

## What's in the box

| Resource | Purpose | SKU / cost in dev |
| --- | --- | --- |
| Log Analytics workspace | Single sink for all logs and metrics | Pay-per-GB, 5 GB/month free |
| Application Insights | API telemetry (requests, dependencies, exceptions) | Workspace-based, piggybacks on the LA quota |
| Key Vault | Secrets store (Auth0, Resend, R2, etc.) | Standard, ~$0.03/10K ops — pennies |
| Container Apps Environment | Compute boundary + log routing | Consumption profile, no fixed cost |
| Container App (API) | Runs the .NET 10 API | Scales 0 → 1, free monthly allowance covers dev usage |
| Static Web App | Hosts the Angular client | Free SKU |

Postgres lives in **Neon free tier**, not in Azure — keeps the Postgres line item at $0.

Estimated monthly run cost for dev: **$0–3** (Key Vault ops + occasional Container App spin-ups beyond free tier).

## Prerequisites

- Azure CLI 2.60+ (`az --version`)
- Bicep CLI bundled with the Azure CLI (`az bicep version` should report 0.30+)
- An Azure subscription you have Contributor on

## First-time deploy

```bash
# 1. Sign in and pick the subscription
az login
az account set --subscription "<your-subscription-id-or-name>"

# 2. Capture your AAD object ID — needed so Bicep can give you Key Vault access
MY_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
echo $MY_OBJECT_ID

# 3. Create the resource group
az group create \
  --name rg-qa-dev \
  --location eastus2 \
  --tags project=quantamanalitics environment=dev managedBy=bicep

# 4. (Optional) Validate the template before applying
az deployment group validate \
  --resource-group rg-qa-dev \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam \
  --parameters keyVaultAdminPrincipalIds="['$MY_OBJECT_ID']"

# 5. Deploy
az deployment group create \
  --resource-group rg-qa-dev \
  --name qa-dev-bootstrap \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam \
  --parameters keyVaultAdminPrincipalIds="['$MY_OBJECT_ID']"
```

Takes ~5–8 minutes the first time (Container Apps environment is the slow one).

## What you get back

After deploy, fetch the outputs:

```bash
az deployment group show \
  --resource-group rg-qa-dev \
  --name qa-dev-bootstrap \
  --query properties.outputs
```

Useful ones:

- `containerAppUrl` — the FQDN of the API (`<name>.<region>.azurecontainerapps.io`). Hit `/health` to confirm it's up.
- `staticWebAppDefaultHostname` — where the Angular client will live once PR-05 deploys it.
- `keyVaultName` — for `az keyvault secret set` commands.
- `appInsightsConnectionString` — already injected into the Container App as an env var; surfaced here in case you need it for local dev.

## Updating the dev environment

Re-run the same `az deployment group create` command. Bicep is declarative — only changed resources get touched.

## Adding a real API image

Until PR-05 wires up the GitHub Actions build/push pipeline, the Container App runs Microsoft's `containerapps-helloworld` placeholder image. To swap in your own image manually:

```bash
az deployment group create \
  --resource-group rg-qa-dev \
  --name qa-dev-image-update \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam \
  --parameters apiImage='ghcr.io/mshanawaz114/quantamanalitics-api:latest'
```

## Tearing it down

Free-tier resources cost nothing when idle, so there's rarely a reason to delete. If you do:

```bash
az group delete --name rg-qa-dev --yes
```

Key Vault soft-delete keeps secrets recoverable for 7 days even after RG delete.
