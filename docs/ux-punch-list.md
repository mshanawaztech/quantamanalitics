# Portal UX punch list (qa009 audit, May 2026)

A focused sweep of the Angular client looking for: dead UI, accessibility
gaps, broken cross-role nav, stub click handlers, and obvious refactor
candidates. Each entry is small enough to land in a single follow-up PR.

Findings are grouped by component. File paths are relative to `client/src/app/`.

## Priority 1 — broken or misleading UX

### `contractor-dashboard.component.ts`
- **Line 172:** A `routerLink="/recruiter"` ships in the contractor portal
  template. Contractors should never navigate to the recruiter dashboard.
  Either gate by role or remove. **Fix: remove the link.**

### `candidate-portal.component.ts`
- **Line 589:** `timelineAllRoute` is hardcoded to `/candidate/dashboard`,
  but that path renders a different component. Either the route or the
  link target is wrong. **Verify routing and align.**

### `marketing-shell.component.ts`
- **Lines 699–707:** Search input shows the placeholder
  "Search jobs, candidates, submissions…" but the submit handler only
  blurs the field — Phase 7 work was deferred. Sets false expectations.
  **Either hide the input until search is real, or change placeholder to
  "Search coming soon".**

### `recruiter-dashboard.component.ts`
- **Line 1306:** `loadTemplatePreset()` doesn't clear `emailTemplatePreview`
  when switching presets — stale preview lingers. **Reset preview signal
  at the top of the method.**

## Priority 2 — accessibility (a11y)

### `admin-audit-console.component.ts`
- **Line 149:** `<p class="error">` for async error has no `role="alert"`.
  Screen readers won't announce it. **Add `role="alert"` and
  `aria-live="assertive"`.**
- **Line 272:** `error` signal initialized as `''` but checked with `??`
  / null guards downstream. **Initialize as `null` for type consistency.**
- **Lines 376–389:** Chip click handlers (`focusSubject`, `focusEntityType`,
  `focusEntity`) fire `runQuery()` immediately on every click. Rapid
  clicking floods the API. **Debounce 250ms or use a single coalesced run.**

### `interview-scheduling.component.ts`
- **Line 109:** Error `<p class="error">` missing `role="alert"`. **Add it.**
- **Line 142:** Candidate email shown unobfuscated in a grid cell. Consider
  whether recruiter-level access is appropriate; if so, leave; if PII
  policy is stricter, mask middle characters.

### `jobs-list.component.ts`
- **Line 30:** "Try again" retry button has no `aria-label` describing what
  is being retried. **Add `aria-label="Retry loading jobs"`.**

### `job-detail.component.ts`
- **Line 31:** Inline retry button on error state needs a clearer
  `aria-label`. **Use `aria-label="Retry loading job details"`.**
- **Line 69:** Submit button uses opacity dim while busy but no
  `aria-busy="true"`. **Add the attribute when `submitting()`.**

### `settings-branding.component.ts`
- **Line 164:** Save button toggles text "Save" ↔ "Saving…" but no
  `aria-busy`. **Same fix.**

## Priority 3 — code quality / refactor

### `recruiter-email-templates.component.ts`
- **Line 481:** `insertMergeField()` appends to the end of the textarea
  rather than at the user's cursor position. Limits discoverability.
  **Use `selectionStart` / `selectionEnd` to splice at the caret.**

### `client-dashboard.component.ts`
- **Line 280:** `reject()` doesn't validate that `reviewNotes` is non-empty
  before rejecting a timesheet. Silent rejects are bad audit trail.
  **Require a non-blank reason; render a field-level error otherwise.**

### `candidate-portal.component.ts`
- **Line 307:** `<button (click)="scrollToProfile()">` styled as a tile.
  Semantically fine but `.tile--button { width: 100%; }` overrides the
  default tile width. **Pick one shape and stick to it.**

## Priority 4 — minor polish

- `marketing-shell.component.ts:102` — `aria-label="Quantam Analytics — home"` on
  brand logo duplicates the visible text; can be dropped.
- `marketing-shell.component.ts:188` — mobile nav items don't get the
  `routerLinkActive` class; desktop nav does. Inconsistent.
- `contact.component.ts:26+` — `mailto:` links use HTML entities
  (`&#64;`) for obfuscation. Works but unreadable in source.
- `services.component.ts:115` — "AI copilot" card routerLinks to
  `/recruiter`; should land on a dedicated route or be made clear.

## Files audited clean

- `recruiter-pipeline.component.ts`
- `recruiter-resume-parser.component.ts`
- `recruiter-recycle-bin.component.ts`
- `status-panel.component.ts`
- `not-found.component.ts`
- `about.component.ts`
- `contact.component.ts` (other than the mailto entity note)

## Not audited (future passes)

- The shared `core/ui/qa-*` design-system primitives (no obvious issues
  surfaced from consumer review).
- `core/auth/*` (auth flow audit deserves its own pass).
- `core/notifications/notifications-bell.component.ts`.
- `style-guide.component.ts`, `accessibility.component.ts`,
  `home.component.ts` — mostly static.
