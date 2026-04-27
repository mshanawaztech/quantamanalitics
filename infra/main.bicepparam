// Parameter file for the dev environment.
// Apply with: az deployment group create -p main.bicepparam ...

using 'main.bicep'

param stack = 'qa'
param environmentName = 'dev'
param location = 'eastus2'

// `apiImage` and `postgresConnectionString` are NOT set here on purpose:
//   - The deploy workflow always passes apiImage as a CLI override.
//   - The Postgres conn string is a secret and never lands in git.
// If apiImage is omitted on a manual deploy, main.bicep falls back to the
// hello-world placeholder. Pass it explicitly with --parameters apiImage=...
// when you want your real image.

// Add your AAD object ID here so you get Key Vault Secrets Officer rights
// on first deploy. Get it with: az ad signed-in-user show --query id -o tsv
param keyVaultAdminPrincipalIds = []
