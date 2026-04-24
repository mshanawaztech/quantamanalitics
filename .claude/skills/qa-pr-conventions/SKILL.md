---
name: qa-pr-conventions
description: Use this skill any time work begins on the Quantam Analytics repo, before any git branch, commit, or PR operation. Trigger on requests that mention starting new work, creating a branch, opening a PR, committing changes, pushing code, or beginning a deliverable. Also trigger if asked to "start working on" a feature, fix a bug, or pick up the next deliverable. The skill enforces the project's qa001 branch naming, small-PR discipline, and squash-merge workflow that keep main clean and AI-assisted contributions reviewable.
---

# Quantam Analytics — Branch & PR conventions

This skill activates whenever work is about to start on the `quantamanalitics` repo. Read these rules and follow them strictly. They override default git habits.

## Non-negotiable rules

1. **Never push to `main`.** `main` only accepts squash-merged PRs.
2. **Branch naming is mandatory:** `qa001-<short-scope>` (kebab-case). Examples:
   - `qa001-bootstrap`
   - `qa001-solution-scaffold`
   - `qa001-postgres-efcore`
   - `qa001-fix-tenant-leak-jobs-list`
3. **One PR = one deliverable** from `plan/phase-1-deliverables.md`. Don't bundle.
4. **Cap each PR at ~600 changed lines.** If it's growing beyond that, split into two PRs.
5. **Squash-merge only.** Linear history on `main`.

## Workflow Claude must follow

When the user asks to start a new piece of work:

1. **Check the active branch:**
   ```bash
   git branch --show-current
   ```
   If on `main`, branch off. If on a `qa001-*` branch already, confirm it matches the scope of the request before continuing.

2. **Branch off latest `main`:**
   ```bash
   git checkout main
   git pull
   git checkout -b qa001-<scope>
   ```

3. **Look up the next pending deliverable:**
   - Open `plan/phase-1-deliverables.md`
   - Find the next row with status `pending`
   - Use that row's branch name and scope

4. **Commit messages:** `<type>(<scope>): <short summary>` where type ∈ `feat | fix | chore | docs | refactor | test | ci | perf | build`. Body explains WHY. Always include the trailer the project standardizes on.

5. **Before suggesting `git push`:** confirm the branch is `qa001-*`, not `main`.

6. **PR template:** every PR uses `.github/pull_request_template.md`. Fill in Phase, Deliverable, Summary, Changes, Test plan, Deploy notes, and check every box on the Checklist.

## When to remind the user

If the user asks to commit work that doesn't fit the active branch's scope, push back: suggest a separate branch + PR. Scope creep on a feature branch is the #1 way these PRs balloon past 600 lines.

If the user asks to push directly to `main`, refuse and explain the rule. Offer to create a branch + PR instead.

## Cost-discipline reminder

The user is bootstrapping. Build-phase target is **$0–10/month total run cost**. Any change that adds recurring cost (new managed service, paid tier, upgraded SKU) must be called out explicitly in the PR description with the reason and dollar amount.

## When this skill does NOT apply

- Pure conversation / planning that doesn't touch git
- Reading code or docs without modification
- Tasks unrelated to the `quantamanalitics` repo
