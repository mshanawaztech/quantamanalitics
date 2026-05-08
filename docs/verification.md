# Verification Reference — Phases 1 → 4 + PR-44

Pragmatic checklist for confirming everything shipped so far works as
intended. Two halves:

- **Automated coverage** — what the test suite already proves. CI on every
  PR runs `dotnet test`; this section maps each shipped surface to the
  tests that gate it.
- **Manual smoke tests** — what you should click / curl through after a
  deploy or whenever you want to confirm a path end-to-end. None of these
  require external partner credentials; everything runs against the dev
  environment as it stands today.

When something on this list breaks, fix-it PRs use the `qa001-fix-…`
naming convention and don't bump phase deliverables.

---

## 1. Automated coverage map

Every row below is a shipped surface paired with the test class that
gates it. If a test class moves or gets renamed, update the row here.

| Surface                          | Phase | Tests                                                                                       |
| -------------------------------- | ----- | ------------------------------------------------------------------------------------------- |
| EF model wiring (every entity)   | 1–4   | `AppDbContextModelTests`                                                                    |
| Tenant query filter              | 1     | `AppDbContextTenantFilterTests` (unit)                                                      |
| **Multi-tenant isolation IT**    | 4     | `TenantIsolationIntegrationTests` (Testcontainers Postgres)                                  |
| Tenant resolution from JWT       | 1     | `TenantResolutionMiddlewareTests`                                                           |
| `/health` + `/ready`             | 1     | `HealthEndpointTests`                                                                       |
| `/me` (auth probe)               | 1     | `MeEndpointTests`                                                                           |
| Public jobs board                | 1     | `PublicJobsEndpointTests`                                                                   |
| Candidate profile + applications | 1     | `CandidateProfileTests`, `CandidateApplicationsEndpointTests`, `ApplicationTests`            |
| Recruiter portal + invoice paths | 2     | `RecruiterInvoiceReadyEndpointTests`                                                        |
| Contractor timesheets            | 2     | `ContractorTimesheetEndpointTests`, `TimesheetTests`                                        |
| Client approval                  | 2     | `ClientApprovalEndpointTests`                                                               |
| Submission state machine         | 3     | `SubmissionTests`                                                                           |
| Interview scheduling shell       | 3     | `InterviewSchedulingEndpointTests`, `InterviewEventTests`                                   |
| Meeting-link generator (stub)    | 3     | `MeetingLinkGeneratorTests`                                                                 |
| Background-check baseline        | 3     | `BackgroundCheckTests`                                                                      |
| E-sign baseline                  | 3     | `EsignDocumentTests`                                                                        |
| Onboarding checklist             | 3     | `OnboardingChecklistItemTests`                                                              |
| **Indeed XML feed**              | 4     | `JobFeedEndpointTests` (Testcontainers; cross-tenant + XML shape)                            |
| **Dice posting scaffold**        | 4     | `StubDicePostingClientTests` (unit; determinism)                                            |
| **Client portal · jobs**         | 4     | `ClientPortalJobsEndpointTests` (Testcontainers; cross-tenant)                              |
| **Client portal · documents**    | 4     | `ClientPortalDocumentsEndpointTests` (Testcontainers; field-allowlist)                       |
| **Reporting summary**            | 4     | `ReportingEndpointTests` (Testcontainers; funnel + cross-tenant)                             |
| **SOC 2 audit-log baseline**     | 5     | `AuditLogInterceptorTests` (Testcontainers; create flow + non-recursion + tenant-scoped)     |

Run the whole suite locally:

```
cd api
dotnet test
```

Docker has to be running for the Testcontainers-backed tests (any test in
the `PostgresCollection` collection). The first run pulls
`postgres:17-alpine` (~80 MB).

CI runs the same command on `ubuntu-24.04` — Docker is preinstalled
there so the Testcontainers path works without extra setup.

### Known gaps

- **Existing endpoint tests pre-PR-30** (`RecruiterInvoiceReadyEndpointTests`
  and similar) still use raw SQL via `db.Database.ExecuteSqlRaw` against
  whatever Postgres user-secrets points at. They run green on the
  maintainer's laptop because of the local Neon connection string; in
  CI without that secret they currently fail open. Migrating these onto
  the PR-30 `PostgresFixture` pattern is on the standing-follow-ups
  list — not a Phase 5 epic of its own, but worth doing before any new
  endpoint test lands.

---

## 2. Manual smoke tests after a dev deploy

Run after any merge that re-deploys, or whenever you want a
five-minute confidence check.

### 2.1 Anonymous public surface

Open the SPA at the dev URL listed in `CLAUDE.md`. Confirm:

- `/` renders the marketing shell, no console errors
- `/jobs` lists at least one seeded job (the demo seeder runs on startup)
- Click a job card → `/jobs/{slug}` renders detail
- The "apply" path works without signing in (guest application intake)

Confirm the API directly:

```
curl https://{api-host}/health        # → 200, JSON body
curl https://{api-host}/ready         # → 200, hits Postgres
curl https://{api-host}/api/v1/jobs   # → 200, array of job summaries
```

### 2.2 Indeed feed (PR-32 + PR-32.5)

```
curl https://{api-host}/api/v1/feeds/{tenantSlug}/indeed.xml
```

Substitute the dev tenant's slug. Expected: `200 OK`,
`Content-Type: application/xml`, valid XML with `<source>` root.

Per-job `<url>` elements should point at the SPA host
(`https://{swa-hostname}/jobs/{slug}`), not the API host. If they point
at the API host, PR-32.5's `PublicWeb__BaseUrl` env var didn't make it
through Bicep — re-check `infra/main.bicep`'s `containerApp` module
wiring.

### 2.3 Sign-in roundtrips

For each role, sign in to the SPA and confirm the role-specific landing:

| Role          | Lands on                       | Should see                                                |
| ------------- | ------------------------------ | --------------------------------------------------------- |
| Candidate     | `/dashboard`                   | applied-jobs history, profile editor                       |
| Recruiter     | `/recruiter`                   | jobs CRUD, applications board, invoice paths              |
| Contractor    | `/contractor`                  | weekly timesheet entry, draft / submit                    |
| Client        | `/client`                      | approval queue, jobs visibility (PR-34), documents (PR-35) |
| PlatformAdmin | recruiter portal + admin gates | everything above + `/admin/audit-log` once it merges       |

Auth0 issues custom claims for `roles` and `tenant_id`. Confirm by
hitting `/me`:

```
curl -H "Authorization: Bearer $TOKEN" https://{api-host}/me
```

Expected JSON includes `roles` array and `tenantId` Guid.

### 2.4 Multi-tenant isolation spot check

PR-30 covers this with an automated IT, but a manual sanity check after
a deploy is still worthwhile because the IT runs against Testcontainers,
not against the live dev database.

1. Sign in as a recruiter at tenant A
2. Hit `/api/v1/recruiter/applications` — note the count
3. Sign out, sign in as a recruiter at a different tenant (or use
   PlatformAdmin to switch tenants)
4. Hit the same URL — count should be different / disjoint

If they overlap, something in the global query filter regressed. Check
`AppDbContext.ApplyTenantQueryFilters` against `git log -p`.

### 2.5 EF migrations (PR-31)

After every merge that changes a migration:

1. Open the GitHub Actions run for `deploy-dev.yml`
2. Find the `apply-migrations` job
3. Confirm the `List migration state` step printed the expected pending
   migration name(s) under "Pending"
4. Confirm the `Apply pending migrations` step exited 0

If `apply-migrations` fails, `deploy-infra` won't run — the old image
keeps serving the old schema. Investigate before clicking "re-run jobs".

### 2.6 Client portal (PR-34, PR-35)

As a Client-role user:

```
curl -H "Authorization: Bearer $TOKEN" https://{api-host}/api/v1/client/jobs
```

Should list jobs at the user's tenant. Pick one job's id, then:

```
curl -H "Authorization: Bearer $TOKEN" https://{api-host}/api/v1/client/jobs/{id}/applications
curl -H "Authorization: Bearer $TOKEN" https://{api-host}/api/v1/client/jobs/{id}/submissions
curl -H "Authorization: Bearer $TOKEN" https://{api-host}/api/v1/client/documents
```

Documents response must NOT include the `sentByAuthSubject`,
`providerSubmissionId`, or `templateSlug` keys — those are recruiter-
internal. The IT has an explicit assertion on this.

### 2.7 Reporting summary (PR-36)

As a Recruiter:

```
curl -H "Authorization: Bearer $TOKEN" https://{api-host}/api/v1/recruiter/reports/summary
```

Expected JSON has three sections: `funnel` (5 status counts), `timeToFill`
(`totalJobsHired`, `averageDays`), `recruiterActivity` (array of
`{authSubject, submissionCount}`). The demo seeder produces enough rows
that funnel.applied > 0 and recruiterActivity is non-empty.

### 2.8 Audit log (PR-44, after merge)

As a PlatformAdmin:

```
curl -H "Authorization: Bearer $TOKEN" "https://{api-host}/api/v1/admin/audit-log?pageSize=20"
```

Should return recent mutations across the tenant. Trigger one by hand
(e.g., update an application's status from the recruiter portal) and
re-run — the new mutation should appear at the top with the recruiter's
auth subject.

If you see an `AuditLogEntry` row in the result, the interceptor's
self-recursion guard regressed — that's a bug to fix before anything
else.

---

## 3. Deploy pipeline checks

After a clean deploy run (`deploy-dev.yml` ends green):

- `api-image` job pushed both a SHA-pinned tag and `latest` to GHCR
- `apply-migrations` job logged "No migrations were applied" if no new
  migration was added — anything else means the migration succeeded
- `deploy-infra` job's smoke test got `200` from `/health`
- `deploy-client` job's smoke test loaded `/`, `/jobs`, and a
  non-existent route (all should return the SPA shell)

If any of these break, the failure mode is fail-loud — no partial
deploys. The previous image keeps serving traffic until the next clean
run.

---

## 4. Observability

Application Insights is wired (free tier). After a deploy:

- Open the AI resource in the Azure portal
- Live Metrics → confirm requests are flowing
- Failures → expect zero new exceptions on a healthy run
- Logs → the `ApplicationInsightsLogger` captures structured logs at
  Information and above

Health probe history lives in Container Apps' Revision view. A failing
readiness probe takes the replica out of rotation; if you see the API
url 503-ing, that's probably Postgres asleep on Neon's free tier — wait
~15s and retry.
