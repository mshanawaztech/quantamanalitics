# Contributing

Single-maintainer project today, but the workflow below is enforced from PR-01 to keep `main` clean, reviewable, and AI-assist friendly.

## Branch naming

Every branch has the prefix **`qa001-`** followed by a short kebab-case scope.

```
qa001-bootstrap
qa001-solution-scaffold
qa001-postgres-efcore
qa001-jobs-list-endpoint
qa001-fix-tenant-leak
```

The `qa001-` prefix is the project abbreviation — it stays even if the repo grows multiple sub-projects later (a future repo would use `qa002-`, etc.).

## Branch lifecycle

```bash
# 1. Always start from latest main
git checkout main
git pull

# 2. Branch
git checkout -b qa001-<scope>

# 3. Work, commit small, commit often
git add <files>
git commit -m "feat(jobs): add list endpoint"

# 4. Push and open PR via GitHub UI or `gh pr create`
git push -u origin qa001-<scope>
gh pr create --fill

# 5. Self-review the diff in the PR view (look at it as a stranger)

# 6. Squash-merge into main via GitHub UI

# 7. Delete the remote branch (GitHub UI offers this on merge)
git checkout main
git pull
git branch -d qa001-<scope>
```

## Commit message conventions

```
<type>(<scope>): <short summary>

<body — explains WHY, not WHAT>
```

Allowed types: `feat | fix | chore | docs | refactor | test | ci | perf | build`

Examples:
```
feat(jobs): list endpoint with tenant scoping

The jobs list now derives tenant_id from the JWT instead of the query
string. Closes the cross-tenant read vector flagged in PR-07 review.
```

```
chore(deps): bump Npgsql 9.0.1 → 9.0.2

CVE-2025-XXXX in connection pooling — patched upstream.
```

## PR rules

1. **One PR = one deliverable** from `plan/phase-1-deliverables.md`. If scope drifts, split.
2. **Cap PRs at ~600 changed lines.** Bigger than that, split.
3. **Fill out the PR template** end-to-end. The Test Plan section is required.
4. **Self-review** before merging — look at the diff as if it were someone else's.
5. **Squash-merge only.** Keep `main` linear.
6. **Update `plan/phase-1-deliverables.md`** status column when you merge.

## Code style

- **.NET:** `dotnet format` before every commit. EditorConfig in repo root governs whitespace.
- **TypeScript / Angular:** `npm run lint` (ESLint + Prettier) before every commit.
- **SQL / migrations:** name migrations descriptively (`20250424_add_tenants_table`, not `Migration1`).
- **Imports:** ordered, deduplicated, no unused.

## Secrets

Never commit secrets. The `.gitignore` excludes:

- `appsettings.Development.json`
- `appsettings.Local.json`
- `*.env`
- `local.settings.json`
- `.vscode/launch.json` (often contains tokens)

Real secrets live in **Azure Key Vault** (production / dev environments) and **`appsettings.Local.json`** (local dev only — gitignored).

If a secret accidentally lands in git, **rotate it immediately**, then `git filter-repo` to scrub history. Don't just delete it in the next commit — git history is forever.

## Adding dependencies

A new dependency = a new maintenance liability. Before adding one:

1. Is it actively maintained (commits in the last 6 months)?
2. Does it have at least 100 GitHub stars or be from a known publisher (Microsoft, JetBrains, etc.)?
3. Is the license MIT / Apache 2.0 / BSD?
4. Could you write what you need in <50 LOC instead?

If yes to all 1–3 and no to 4, add it. Otherwise reconsider.

## ADRs (Architecture Decision Records)

Material decisions go in `docs/adr/NNNN-short-title.md`. Format:

```
# ADR-NNNN: Short title

Date: 2026-04-24
Status: Accepted | Superseded by ADR-MMMM | Deprecated

## Context
What's the problem and why does it need a decision?

## Decision
What was decided.

## Consequences
What does this make easier? Harder? What did we accept the cost of?
```

ADRs are append-only — never delete one. To change a decision, write a new ADR that supersedes the old one.
