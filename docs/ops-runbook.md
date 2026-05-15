# Ops runbook — Phase 8 / Story 65

The on-call notes for keeping Quantam Analytics healthy. Owned by the
maintainer team. Update this file when an incident teaches us something
new; never delete a section — strike it through.

## Where to look first

| Surface | URL / location | Auth |
| --- | --- | --- |
| Live API health | `https://ca-qa-dev-api.icymushroom-94be4003.eastus2.azurecontainerapps.io/healthz` | none |
| Live API readiness | `…/ready` | none |
| Container App logs | Azure Portal → Container Apps → `ca-qa-dev-api` → Log stream | Azure RBAC |
| Database | Neon dev branch, prod = Azure DB for PostgreSQL flexible server | secret |
| Static Web App | Azure Portal → Static Web Apps → `swa-qa-dev` | Azure RBAC |
| App Insights | Azure Portal → Application Insights → `ai-qa-dev` | Azure RBAC |
| GitHub Actions | https://github.com/mshanawaz114/quantamanalitics/actions | GitHub |

## Monitoring — what is wired

| Signal | Source | Alerts on |
| --- | --- | --- |
| `/ready` HTTP 200 | Container Apps probe | 3 consecutive failures → restart pod |
| EF Core DbContext open | `AddDbContextCheck` in `DependencyInjection.cs` | Reflected in `/ready` |
| Request error rate | App Insights | >5% over 5m → email maintainer alias |
| Average request latency | App Insights | p95 > 1500ms over 10m → email maintainer alias |
| Failed signed-URL mints | App Insights custom event | none yet — add when first incident calls for it |

Alert rules live in `infra/main.bicep` under the `monitoring` module
(if it doesn't exist yet, file a follow-up; the current baseline is
container-app default + manual App Insights dashboards).

## Backup / restore

### Postgres

- **Dev (Neon free tier):** point-in-time restore from the Neon
  console, retention is the Neon default (7 days on the free tier).
  Documented exception: PII workloads should not run against the dev
  tenant.
- **Production (Azure DB for PostgreSQL flexible server):** automated
  backups enabled at server creation, 30 days retention, geo-redundant
  storage in the production resource group. To restore: Azure Portal →
  PostgreSQL flexible server → Restore → "point in time".

### R2 object storage

- Versioning enabled on the prod bucket; objects deleted via the API
  remain recoverable for 30 days via the bucket's versions tab.
- To restore: Cloudflare dashboard → R2 → bucket → enable "show
  deleted versions" → restore the prior version.

### Recovery checklist

```
[ ] Confirm scope of incident (single tenant? single table? whole DB?)
[ ] Page the maintainer alias before touching prod data.
[ ] Snapshot current state so a failed restore is reversible.
[ ] For Postgres: restore to a *new* server, sanity-check, then cut over.
[ ] For R2: restore object versions, do not overwrite live bucket.
[ ] After cutover: re-run /ready + run the smoke flow in docs/verification.md.
[ ] Write a postmortem in docs/postmortems/ (create if doesn't exist).
```

## Feature flags

Code surface: `IFeatureGate` in `QuantamAnalytics.Infrastructure.Features`.

Implementation: `AppSettingsFeatureGate` reads the configuration tree
under `Features:<name>`. Values can come from `appsettings.json`,
environment variables (`Features__name=true` — double underscore =
section separator), or Azure App Configuration when wired.

### Currently exposed flags

| Flag | Default | Reads in |
| --- | --- | --- |
| `Features:CandidatePortalV3` | false | SPA `FeatureService` (TBD) |
| `Features:RecruiterAiCopilot` | false | gates the Phase 9 copilot UI |
| `Features:InvoiceAutomation` | false | gates async invoice job posting |
| `Features:PlatformApiBeta` | false | reveals the public-API endpoints in `/docs` |

The SPA reads these once at session start via
`GET /api/v1/me/features`. Adding a new flag is two steps: (1) add the
name to `PublicFlags` in `FeaturesEndpoint.cs`, (2) consume it
client-side. Skipping step 1 means the SPA reads it as disabled — the
safe default.

### Flipping a flag in production

```
az containerapp update \
  --name ca-qa-prod-api \
  --resource-group rg-qa-prod \
  --set-env-vars Features__CandidatePortalV3=true
```

Apply takes ~30 seconds (revision rollover). Roll back by re-running
with the prior value. Document the flip in the team channel; do not
leave a flag flipped in prod without an owner.

## Common incidents

### Dev API returning 500 across the board

1. Check Container App log stream.
2. If "Connection string 'Postgres' is not configured", the
   `POSTGRES_CONNECTION_STRING` env var is missing from the revision
   — re-run the `deploy-dev` workflow with the latest secrets.
3. If you see `42P01 relation does not exist`, a migration was
   skipped — see the migration-failure runbook below.

### Migration job failed in deploy-dev

The `apply-migrations` job runs `dotnet ef database update` against
the live DB. Failures are usually one of:

- **Connection string missing**: re-add `POSTGRES_CONNECTION_STRING`
  in repo secrets and re-run the workflow.
- **Migration drift**: a generated migration doesn't match the model.
  Usually means someone generated the migration from a branch that
  didn't have the entity yet (empty `Up()` / `Down()`). Regenerate
  from the correct branch and force-push.
- **Cannot drop column / table is not empty**: the migration is
  destructive against existing data. Either add a data-copy step or
  split into a two-phase migration. **Never** apply a destructive
  migration without snapshotting the database first.

### SPA loads but every API call is 401

Auth0 dashboard → check the Application status. If the API audience
or domain changed, the SPA's `app.config.ts` `provideAuth0` config
must be updated and redeployed.

### "Empty notification dropdown" reports

The `notifications` feed is best-effort. Real misses (mutation
happened, no notification) usually trace to a writer that forgot to
add the row. Check the audit log for the same time window — audit
rows always land via the SaveChangesInterceptor, so if the audit
shows it but notifications doesn't, the writer is the culprit.

## Postmortem template

When something breaks:

1. Restore service first, ask why second.
2. Within 48 hours, write a postmortem to `docs/postmortems/YYYY-MM-DD-<slug>.md`
   covering: what failed, who noticed, when each step happened, what
   we tried that didn't work, what fixed it, what we'd do differently.
3. Add at least one preventive action to the backlog with an owner.
4. Link the postmortem here.

## Postmortems

_None yet. Add one and link it here when the first incident closes._
