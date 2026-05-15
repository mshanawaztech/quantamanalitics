# Production hardening playbook — Phase 5 / Story 41

The set of changes that move the platform from "shared dev box" to
"environment a paying customer can sign onto". Each section is
self-contained so they can land independently as the prod blockers
clear.

## 1. Private container registry (this PR)

### What landed

`infra/modules/container-app.bicep` and `infra/main.bicep` now accept
three new parameters: `registryServer`, `registryUsername`,
`registryPassword`. When `registryServer` is set, the Container App
declares a `registries` block; when a username/password pair is also
supplied, the password lives in a Container App secret named
`registry-password` and the pull is authenticated.

This makes the existing public-image deploy keep working (all three
params default to empty) while letting prod opt in to a private
GHCR image without code changes.

### Cutover steps for production

1. Generate a GitHub fine-grained PAT on the `mshanawaz114/quantamanalitics`
   repo with read-only access to packages. Copy the value once; you
   won't be able to retrieve it again.
2. Store it as a GitHub Actions repo secret named `GHCR_PULL_TOKEN`.
3. Update `.github/workflows/deploy-prd.yml` (or whichever workflow
   targets prod) to pass three new bicep parameters:
   ```yaml
   - name: Deploy infra
     run: |
       az deployment group create \
         --resource-group rg-qa-prd \
         --template-file infra/main.bicep \
         --parameters \
           registryServer=ghcr.io \
           registryUsername=${{ github.actor }} \
           registryPassword=${{ secrets.GHCR_PULL_TOKEN }} \
           [...existing parameters...]
   ```
4. Confirm the next deploy revision pulls the image without 401s in
   the Container App revision log.

### Future improvement: managed-identity pull

Once Azure Container Registry (ACR) replaces GHCR as the prod image
source, we swap the username/password block for a User-Assigned Managed
Identity with the `AcrPull` role on the registry. The Container App's
`identity` block already requests a system-assigned MI; the only delta
is binding it to the ACR resource. Tracked separately as a follow-up
under `qa001-prod-hardening-acr` because it requires ACR provisioning
that the current cost target doesn't budget for.

## 2. Production Auth0 tenant (separate PR)

Tracked as part of the `docs/auth-hardening.md` playbook (Phase 8 /
Story 63). The dev tenant `quantamanalitics-dev.us.auth0.com` keeps
serving dev; prod gets its own tenant + custom domain + the
post-login Action body that wires the `/me/security/suspicious-login`
landing pad.

Cutover blocked on: an Auth0 paid plan (custom domains require the
Essentials tier and up).

## 3. Custom domain + managed cert (separate PR)

The `static-web-app.bicep` module needs a `customDomain` resource
plus a `dnsAuthorization` text record. The container-app side gets
its own `customDomain` resource pointing at the same Cloudflare DNS
record. Both are short Bicep changes; both need the Cloudflare
DNS records created first.

Cutover blocked on: domain TXT validation records.

## 4. Branch protection on `main` (one-time)

Settings → Branches → add a rule for `main` requiring:

  - `build` (CI)
  - `unit-tests` (CI)
  - `bicep-what-if` (deploy preview)
  - `e2e-axe` (accessibility gate)

Required signed commits + linear history. PR template enforced.

## 5. SOC 2 evidence reuse (already merged)

The audit-log baseline (PR-44) and audit-console UI (PR-89) cover the
"who did what" half of SOC 2's CC6 controls. The
ops-runbook + auth-hardening + trust-center pages cover the
"documented controls" half. Quote from `docs/ops-runbook.md` and
`docs/auth-hardening.md` directly when a customer's questionnaire
asks for evidence — that's why those documents exist.

---

## Open follow-ups

- **ACR-with-MI pull** — wait until ACR is provisioned. Tracked under
  `qa001-prod-hardening-acr`.
- **Container App scaling beyond min=0** — once a paying customer is on
  prod, set `minReplicas=1` so cold-start latency doesn't bite their
  first request of the day. Costs ~$5/month extra at consumption SKU.
- **Application Insights live-stream + log retention** — currently the
  default 30-day retention. Bump to 90 days for prod once a customer
  is paying for it.
