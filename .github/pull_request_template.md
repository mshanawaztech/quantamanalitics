<!--
Branch must be qa001-<scope>. Title format: "<type>: <short summary>"
Squash-merge only. Cap at ~600 changed lines — split if larger.
-->

## Phase / Deliverable

Phase: <!-- 1 / 2 / 3 / 4 / 5 -->
Deliverable: <!-- e.g., PR-02 · Solution scaffold -->

## Summary

<!-- 1–3 sentences: what this PR does and WHY. -->

## Changes

<!-- Bullet list of meaningful changes, grouped by area. -->
- 

## Test plan

<!-- REQUIRED. How did you verify this works? Steps a reviewer can repeat. -->
- [ ] 
- [ ] 
- [ ] 

## Screenshots / recordings

<!-- For UI changes only. Drag-drop here. -->

## Deploy notes

<!-- Anything that has to happen at deploy time: new env vars, migrations, infra changes, secret rotations. -->
- [ ] No deploy-time action needed
- [ ] New environment variable: <!-- name + where to set -->
- [ ] Database migration required (`dotnet ef database update`)
- [ ] Bicep change — `infra/` updated and re-applied
- [ ] New secret added to Key Vault: <!-- name -->

## Checklist

- [ ] Branch is `qa001-<scope>`
- [ ] Diff is under ~600 lines (else: justify in summary)
- [ ] No secrets committed
- [ ] `dotnet format` and `npm run lint` clean (where applicable)
- [ ] `plan/phase-1-deliverables.md` status column updated
- [ ] CLAUDE.md "Current state" updated if this PR closes a milestone
