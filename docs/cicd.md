# CI/CD setup

This is a one-time setup. After it's done, every squash-merge into `main` deploys automatically.

## What runs when

| Workflow | Trigger | What it does |
| --- | --- | --- |
| `.github/workflows/ci.yml` | every PR + push to `main` | Restore + build + test the .NET solution; lint + build the Angular client; lint every Bicep file; build the Docker image (no push). Branch protection on `main` requires this to pass. |
| `.github/workflows/deploy-dev.yml` | push to `main` + manual `workflow_dispatch` | Build & push the API image to GHCR, redeploy the Bicep template with the new image tag, smoke-test `/health`, build the Angular bundle pointing at the live API URL, deploy to Static Web Apps. |

The deploy job uses **OIDC federation** to authenticate to Azure — no client-secret stored anywhere.

---

## One-time setup

Do every step in order. None of this needs to be repeated unless you blow away the resource group or rotate the federation trust.

### 1. Make sure dev infra exists

PR-04 ships the Bicep. If you haven't run it yet:

```bash
az login
az account set --subscription "<your-subscription-id>"
az group create --name rg-qa-dev --location eastus2
MY_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
az deployment group create \
  --resource-group rg-qa-dev \
  --name qa-dev-bootstrap \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam \
  --parameters keyVaultAdminPrincipalIds="['$MY_OBJECT_ID']"
```

### 2. Create the GitHub OIDC service principal

This is the Entra ID identity GitHub Actions assumes when it runs `az login`. It carries no secret — trust comes from the OIDC token GitHub mints per workflow run.

```bash
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
REPO=mshanawaz114/quantamanalitics

# Create the AAD app + SP
APP_ID=$(az ad app create --display-name "qa-github-dev" --query appId -o tsv)
az ad sp create --id "$APP_ID"

# Federated credential: trust GitHub when the token says
# "this run is from main on $REPO".
az ad app federated-credential create --id "$APP_ID" --parameters "{
  \"name\": \"github-main\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"repo:$REPO:ref:refs/heads/main\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}"

# Optional — also trust workflow_dispatch runs from any branch (handy for
# manual deploys without merging). Skip if you only want main deploys.
az ad app federated-credential create --id "$APP_ID" --parameters "{
  \"name\": \"github-workflow-dispatch\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"repo:$REPO:environment:dev\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}"

# Grant the SP Contributor on rg-qa-dev so it can run `az deployment group create`.
az role assignment create \
  --assignee "$APP_ID" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/rg-qa-dev" \
  --role Contributor

# Grant the SP Key Vault Secrets Officer so it can read/write secrets later.
KV_NAME=$(az deployment group show \
  --resource-group rg-qa-dev \
  --name qa-dev-bootstrap \
  --query properties.outputs.keyVaultName.value -o tsv)
KV_ID=$(az keyvault show --name "$KV_NAME" --query id -o tsv)
az role assignment create \
  --assignee "$APP_ID" \
  --scope "$KV_ID" \
  --role "Key Vault Secrets Officer"

echo
echo "Now go to GitHub → repo → Settings → Secrets and variables → Actions"
echo "and add these three repository secrets:"
echo
echo "  AZURE_CLIENT_ID       $APP_ID"
echo "  AZURE_TENANT_ID       $TENANT_ID"
echo "  AZURE_SUBSCRIPTION_ID $SUBSCRIPTION_ID"
```

### 3. Add the SWA deployment token to GitHub secrets

```bash
SWA_NAME=$(az deployment group show \
  --resource-group rg-qa-dev \
  --name qa-dev-bootstrap \
  --query properties.outputs.staticWebAppName.value -o tsv)

az staticwebapp secrets list \
  --name "$SWA_NAME" \
  --resource-group rg-qa-dev \
  --query properties.apiKey -o tsv
```

Copy that value into a fourth GitHub repository secret named **`AZURE_STATIC_WEB_APPS_API_TOKEN`**.

### 4. Make the GHCR package public (one click, after first push)

The first deploy will push `ghcr.io/mshanawaz114/quantamanalitics-api`. By default GHCR packages are private, which means the Container App needs registry credentials to pull. For dev simplicity, make it public:

1. Go to `https://github.com/mshanawaz114?tab=packages` after the first deploy
2. Click `quantamanalitics-api`
3. Package settings → Change visibility → Public

(Production should keep it private and use managed identity pulls — a Phase 5 hardening task.)

### 5. Enable branch protection on `main`

Settings → Branches → Add rule for `main`:

- ✅ Require a pull request before merging
- ✅ Require approvals: 0 (single-maintainer; bump when team grows)
- ✅ Require status checks to pass: select **API · build + test**, **Client · build + lint**, **Infra · bicep lint**, **API · docker build (no push)**
- ✅ Require branches to be up to date before merging
- ✅ Require conversation resolution
- ✅ Do not allow bypassing the above settings (even for admins)
- ❌ Allow force pushes
- ❌ Allow deletions

---

## Verifying it works

1. Open a trivial PR (e.g. README typo). Confirm CI runs and goes green.
2. Squash-merge it. Watch `Deploy · dev` run end to end. It should:
   - Build the API image and push `ghcr.io/.../quantamanalitics-api:<sha>` and `:latest`
   - Re-deploy the Bicep with `apiImage` set to the new SHA tag
   - Get a 200 from `https://<container-app-fqdn>/health`
   - Build the Angular bundle pointing at that URL
   - Upload to SWA

The final SWA URL is in the workflow logs (`deploy-infra` job → `Read deployment outputs` step → `swa_hostname`).

---

## Database migrations

Migrations are **not** applied automatically by `Deploy · dev`. They're run manually so a bad migration doesn't take the API down mid-deploy.

When a PR adds a new migration:

```bash
# Make sure user-secrets has the Neon connection string set
cd api
dotnet ef database update \
  --project QuantamAnalytics.Infrastructure \
  --startup-project QuantamAnalytics.Api
```

(PR-12 hardening adds a manual `migrate-dev.yml` workflow so you don't have to do this from your laptop.)

---

## Troubleshooting

**`az login` step fails with AADSTS70021** → the federated credential subject doesn't match. Re-check that `subject` is exactly `repo:<owner>/<repo>:ref:refs/heads/main`.

**`az deployment group create` fails with `AuthorizationFailed`** → the SP isn't Contributor on `rg-qa-dev` yet. Re-run the role-assignment step.

**Container App pull fails: `unauthorized`** → GHCR package is still private. See step 4.

**SWA deploy fails: `Invalid api token`** → the token in `AZURE_STATIC_WEB_APPS_API_TOKEN` is stale (regenerated when the SWA was redeployed). Re-fetch with `az staticwebapp secrets list`.

**Health smoke test fails after deploy** → the Container App took longer than 2 min to become ready. Bump the retry count in `deploy-dev.yml` or check `az containerapp logs show` for the actual error.
