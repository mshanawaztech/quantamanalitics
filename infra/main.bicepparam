// Parameter file for the dev environment.
// Apply with: az deployment group create -p main.bicepparam ...

using 'main.bicep'

param stack = 'qa'
param environmentName = 'dev'
param location = 'eastus2'

// API container image. Empty string = the bicep default
// (mcr.microsoft.com/azuredocs/containerapps-helloworld:latest), which is
// what we want until PR-05 wires up the GHA build/push pipeline.
param apiImage = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

// Add your AAD object ID here so you get Key Vault Secrets Officer rights
// on first deploy. Get it with: az ad signed-in-user show --query id -o tsv
param keyVaultAdminPrincipalIds = []
