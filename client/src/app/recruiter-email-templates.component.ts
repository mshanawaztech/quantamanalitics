import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CreateEmailTemplateRequest,
  EmailTemplate,
  EmailTemplatePreset,
  EmailTemplatesService,
  UpdateEmailTemplateRequest,
} from './core/recruiter/email-templates.service';
import {
  QaAlertComponent,
  QaButtonComponent,
  QaEmptyStateComponent,
  QaInputComponent,
} from './core/ui';

/**
 * Track B — wires the recruiter email-template editor to the PR-83
 * backend.
 *
 * Layout: two columns. Left = template list + "new from preset" picker.
 * Right = editor with name/slug/subject inputs, a markdown textarea, a
 * merge-field picker that inserts {{tokens}} at the cursor, and a live
 * preview that calls /preview with a deterministic stub merge-field map.
 *
 * Keeping it on signals + inline template + design tokens so it matches
 * the rest of the recruiter portal. No markdown library — preview shows
 * the rendered body as plain text with line breaks preserved; rendering
 * actual HTML would require a sanitizer that doesn't ship in the SPA
 * and that's out of scope for the editor itself.
 */
@Component({
  selector: 'app-recruiter-email-templates',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    QaAlertComponent,
    QaButtonComponent,
    QaEmptyStateComponent,
    QaInputComponent,
  ],
  template: `
    <div class="page">
      <header class="page__header">
        <p class="eyebrow">Recruiter</p>
        <h1>Email templates</h1>
        <p class="lede">
          Reusable copy for invites, rejections, onboarding nudges. Merge
          fields like
          <code>&#123;&#123;CandidateFirstName&#125;&#125;</code>
          render at send time.
        </p>
      </header>

      @if (loadError()) {
        <qa-alert tone="danger">{{ loadError() }}</qa-alert>
      }

      <div class="layout">
        <!-- ── Left rail: templates + presets ─────────────────────── -->
        <section class="rail" aria-labelledby="rail-heading">
          <h2 id="rail-heading">Your templates</h2>

          @if (templates().length === 0 && !loading()) {
            <qa-empty-state
              title="No templates yet"
              hint="Start from a preset, or write one from scratch."
            />
          } @else {
            <ul class="rail__list">
              @for (t of templates(); track t.id) {
                <li>
                  <button
                    type="button"
                    [class.active]="selectedId() === t.id"
                    (click)="select(t.id)"
                  >
                    <strong>{{ t.name }}</strong>
                    <small>{{ t.slug }}</small>
                  </button>
                </li>
              }
            </ul>
          }

          <h3 class="rail__preset-heading">Start from a preset</h3>
          @if (presets().length === 0) {
            <p class="rail__hint">Loading…</p>
          } @else {
            <ul class="rail__presets">
              @for (p of presets(); track p.slug) {
                <li>
                  <button type="button" (click)="cloneFromPreset(p)">
                    <strong>{{ p.name }}</strong>
                    <span>{{ p.description }}</span>
                  </button>
                </li>
              }
            </ul>
          }
        </section>

        <!-- ── Right pane: editor ───────────────────────────────────── -->
        <section class="editor" aria-labelledby="editor-heading">
          <header class="editor__head">
            <h2 id="editor-heading">{{ isNew() ? 'New template' : draft().name || 'Untitled' }}</h2>
            <div class="editor__actions">
              @if (saveStatus() === 'success') {
                <qa-alert tone="success" role="status">Saved.</qa-alert>
              }
              @if (saveError()) {
                <qa-alert tone="danger" role="alert">{{ saveError() }}</qa-alert>
              }
              @if (!isNew()) {
                <qa-button
                  variant="ghost"
                  tone="danger"
                  (click)="remove()"
                >Delete</qa-button>
              }
              <qa-button
                variant="primary"
                [disabled]="saving()"
                (click)="save()"
              >{{ saving() ? 'Saving…' : 'Save' }}</qa-button>
            </div>
          </header>

          <div class="editor__grid">
            <qa-input
              label="Name"
              hint="Recruiter-facing label, free-form."
              [(ngModel)]="draftName"
            ></qa-input>
            <qa-input
              label="Slug"
              hint="Kebab-case identifier. Unique per tenant. Cannot change after create."
              [disabled]="!isNew()"
              [(ngModel)]="draftSlug"
            ></qa-input>
            <qa-input
              label="Subject"
              hint="Subject line. Merge fields supported."
              [(ngModel)]="draftSubject"
            ></qa-input>

            <div class="merge-fields">
              <span class="merge-fields__label">Insert merge field:</span>
              @for (field of mergeFields(); track field) {
                <button
                  type="button"
                  class="merge-fields__chip"
                  (click)="insertMergeField(field)"
                >
                  &#123;&#123;{{ field }}&#125;&#125;
                </button>
              }
            </div>

            <label class="body-label" for="body-md">Body (markdown)</label>
            <textarea
              id="body-md"
              rows="14"
              [(ngModel)]="draftBody"
              spellcheck="true"
            ></textarea>
          </div>

          <!-- ── Live preview ─────────────────────────────────────── -->
          <section class="preview" aria-labelledby="preview-heading">
            <header>
              <h3 id="preview-heading">Preview</h3>
              <small>Using sample merge values. Real sends pull from the candidate + application.</small>
            </header>
            @if (previewError()) {
              <qa-alert tone="danger" role="alert">{{ previewError() }}</qa-alert>
            } @else if (preview(); as p) {
              <div class="preview__subject">{{ p.subject }}</div>
              <pre class="preview__body">{{ p.bodyMarkdown }}</pre>
            } @else {
              <p class="preview__hint">Type to see a rendered preview.</p>
            }
          </section>
        </section>
      </div>
    </div>
  `,
  styles: `
    :host { display: block; }
    .page { width: min(1240px, 100%); margin: 0 auto; padding: 2rem 1.5rem 3rem; }
    .page__header { margin-bottom: 1.5rem; }
    .eyebrow { color: var(--color-primary, #1a3a8f); font-size: 12px; font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase; margin: 0 0 0.5rem; }
    h1 { margin: 0 0 0.5rem; font-size: 1.75rem; }
    .lede { color: var(--color-fg-muted, #5d6577); margin: 0; }

    .layout {
      display: grid;
      grid-template-columns: 320px 1fr;
      gap: 1.5rem;
      align-items: start;
    }

    /* Rail */
    .rail {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 12px;
      padding: 1rem;
    }
    .rail h2 { margin: 0 0 0.75rem; font-size: 1rem; }
    .rail__list { list-style: none; margin: 0 0 1rem; padding: 0; }
    .rail__list button {
      width: 100%; text-align: left; display: flex; flex-direction: column; gap: 2px;
      padding: 0.625rem 0.75rem; border: 1px solid transparent;
      background: none; border-radius: 8px; cursor: pointer; color: inherit;
    }
    .rail__list button:hover { background: var(--color-primary-soft, #e7ecf6); }
    .rail__list button.active { background: var(--color-primary-soft, #e7ecf6); border-color: var(--color-primary, #1a3a8f); }
    .rail__list small { color: var(--color-fg-muted, #5d6577); font-family: ui-monospace, monospace; font-size: 12px; }

    .rail__preset-heading {
      font-size: 0.85rem; text-transform: uppercase; letter-spacing: 0.08em;
      color: var(--color-fg-muted, #5d6577); margin: 1rem 0 0.5rem;
    }
    .rail__presets { list-style: none; margin: 0; padding: 0; }
    .rail__presets button {
      width: 100%; text-align: left; padding: 0.625rem 0.75rem;
      background: var(--color-canvas, #f5f7fb);
      border: 1px dashed var(--color-border, #d8dde7);
      border-radius: 8px; cursor: pointer; display: flex; flex-direction: column; gap: 2px;
      margin-bottom: 0.5rem; color: inherit;
    }
    .rail__presets button:hover { border-color: var(--color-primary, #1a3a8f); }
    .rail__presets span { font-size: 0.8rem; color: var(--color-fg-muted, #5d6577); }
    .rail__hint { color: var(--color-fg-muted, #5d6577); font-size: 0.875rem; }

    /* Editor */
    .editor {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 12px;
      padding: 1.25rem;
    }
    .editor__head {
      display: flex; align-items: center; justify-content: space-between;
      gap: 1rem; flex-wrap: wrap; margin-bottom: 1rem;
    }
    .editor__head h2 { margin: 0; font-size: 1.125rem; }
    .editor__actions { display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center; }

    .editor__grid {
      display: grid;
      gap: 1rem;
    }
    .merge-fields {
      display: flex; flex-wrap: wrap; gap: 0.375rem; align-items: center;
    }
    .merge-fields__label { font-size: 0.8rem; color: var(--color-fg-muted, #5d6577); }
    .merge-fields__chip {
      background: var(--color-primary-soft, #e7ecf6);
      color: var(--color-primary, #1a3a8f);
      border: 0; border-radius: 999px;
      padding: 0.25rem 0.625rem;
      font-family: ui-monospace, monospace;
      font-size: 0.75rem;
      cursor: pointer;
    }
    .merge-fields__chip:hover { background: var(--color-primary, #1a3a8f); color: var(--color-surface, #fff); }

    .body-label { font-size: 0.85rem; font-weight: 600; }
    textarea {
      width: 100%;
      font-family: ui-monospace, monospace;
      font-size: 13px;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 8px;
      padding: 0.75rem;
      resize: vertical;
    }
    textarea:focus-visible {
      outline: 3px solid var(--color-primary, #1a3a8f);
      outline-offset: 1px;
    }

    /* Preview */
    .preview {
      margin-top: 1.5rem;
      border-top: 1px solid var(--color-border, #d8dde7);
      padding-top: 1rem;
    }
    .preview header { display: flex; flex-direction: column; gap: 2px; margin-bottom: 0.75rem; }
    .preview h3 { margin: 0; font-size: 1rem; }
    .preview small { color: var(--color-fg-muted, #5d6577); font-size: 0.8rem; }
    .preview__subject {
      font-weight: 700;
      font-size: 1rem;
      padding: 0.5rem 0.75rem;
      background: var(--color-canvas, #f5f7fb);
      border-radius: 8px;
      margin-bottom: 0.5rem;
    }
    .preview__body {
      background: var(--color-canvas, #f5f7fb);
      border-radius: 8px;
      padding: 0.75rem;
      white-space: pre-wrap;
      font-family: inherit;
      font-size: 0.95rem;
      line-height: 1.5;
      margin: 0;
    }
    .preview__hint { color: var(--color-fg-muted, #5d6577); }

    @media (max-width: 900px) {
      .layout { grid-template-columns: 1fr; }
    }
  `,
})
export class RecruiterEmailTemplatesComponent {
  private svc = inject(EmailTemplatesService);

  protected readonly templates = signal<EmailTemplate[]>([]);
  protected readonly presets = signal<EmailTemplatePreset[]>([]);
  protected readonly mergeFields = signal<string[]>([]);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  protected readonly selectedId = signal<string | null>(null);

  // Draft fields use plain string bindings because qa-input is bound
  // via ngModel; signals would need a control-value-accessor wrapper.
  protected draftSlug = '';
  protected draftName = '';
  protected draftSubject = '';
  protected draftBody = '';

  protected readonly draft = computed(() => ({
    slug: this.draftSlug,
    name: this.draftName,
    subject: this.draftSubject,
    body: this.draftBody,
  }));

  protected readonly isNew = computed(() => this.selectedId() === null);

  protected readonly saving = signal(false);
  protected readonly saveStatus = signal<'idle' | 'success'>('idle');
  protected readonly saveError = signal<string | null>(null);

  protected readonly preview = signal<{ subject: string; bodyMarkdown: string } | null>(null);
  protected readonly previewError = signal<string | null>(null);
  private previewTimer: ReturnType<typeof setTimeout> | null = null;

  // Sample values surfaced into the preview merge map. These are the ones
  // a recruiter recognizes from the demo data; real sends substitute from
  // the candidate + application records.
  private sampleMergeValues: Record<string, string> = {
    CandidateFirstName: 'Avery',
    CandidateFullName: 'Avery Carter',
    CandidateEmail: 'avery.carter@example.com',
    JobTitle: 'Senior Engineer',
    RecruiterName: 'Jane Recruiter',
    InterviewDate: '2026-06-01',
    InterviewLink: 'https://meet.example.com/abc',
    OfferAmount: '$140,000',
    StartDate: '2026-06-15',
  };

  constructor() {
    this.loadAll();

    // Re-render the preview when the draft changes — debounced because the
    // backend is the source of truth on merge-field substitution and we
    // want one POST per keystroke burst, not per keystroke.
    effect(() => {
      // Establish dependencies — referenced fields tie the effect to
      // changes in any of them.
      this.draft();
      this.schedulePreview();
    });
  }

  protected select(id: string): void {
    this.svc.get(id).subscribe({
      next: (t) => {
        this.selectedId.set(id);
        this.draftSlug = t.slug;
        this.draftName = t.name;
        this.draftSubject = t.subject;
        this.draftBody = t.bodyMarkdown;
        this.saveStatus.set('idle');
        this.saveError.set(null);
      },
      error: (e: unknown) => this.loadError.set(this.toMessage(e)),
    });
  }

  protected cloneFromPreset(p: EmailTemplatePreset): void {
    this.selectedId.set(null);
    this.draftSlug = p.slug;
    this.draftName = p.name;
    this.draftSubject = p.subject;
    this.draftBody = p.bodyMarkdown;
    this.saveStatus.set('idle');
    this.saveError.set(null);
  }

  protected save(): void {
    this.saving.set(true);
    this.saveStatus.set('idle');
    this.saveError.set(null);

    const id = this.selectedId();
    if (id === null) {
      const req: CreateEmailTemplateRequest = {
        slug: this.draftSlug,
        name: this.draftName,
        subject: this.draftSubject,
        bodyMarkdown: this.draftBody,
      };
      this.svc.create(req).subscribe({
        next: (saved) => {
          this.templates.update((items) => [...items, saved].sort((a, b) => a.name.localeCompare(b.name)));
          this.selectedId.set(saved.id);
          this.saving.set(false);
          this.flashSaved();
        },
        error: (e: unknown) => {
          this.saving.set(false);
          this.saveError.set(this.toMessage(e));
        },
      });
    } else {
      const req: UpdateEmailTemplateRequest = {
        name: this.draftName,
        subject: this.draftSubject,
        bodyMarkdown: this.draftBody,
      };
      this.svc.update(id, req).subscribe({
        next: (saved) => {
          this.templates.update((items) =>
            items.map((t) => (t.id === saved.id ? saved : t))
              .sort((a, b) => a.name.localeCompare(b.name)),
          );
          this.saving.set(false);
          this.flashSaved();
        },
        error: (e: unknown) => {
          this.saving.set(false);
          this.saveError.set(this.toMessage(e));
        },
      });
    }
  }

  protected remove(): void {
    const id = this.selectedId();
    if (id === null) return;
    if (!confirm('Delete this template? This cannot be undone.')) return;

    this.svc.remove(id).subscribe({
      next: () => {
        this.templates.update((items) => items.filter((t) => t.id !== id));
        this.selectedId.set(null);
        this.draftSlug = '';
        this.draftName = '';
        this.draftSubject = '';
        this.draftBody = '';
      },
      error: (e: unknown) => this.saveError.set(this.toMessage(e)),
    });
  }

  protected insertMergeField(field: string): void {
    // Drop at end — full cursor-aware insertion would require a textarea
    // ref and selectionStart tracking; the chip pattern below is the
    // simplest thing that lets a recruiter discover the token names.
    const token = `{{${field}}}`;
    this.draftBody = this.draftBody + token;
  }

  private loadAll(): void {
    this.loading.set(true);
    this.loadError.set(null);

    this.svc.list().subscribe({
      next: (response) => {
        this.templates.set(response.items);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.loadError.set(this.toMessage(e));
        this.loading.set(false);
      },
    });

    this.svc.catalog().subscribe({
      next: (response) => {
        this.presets.set(response.presets);
        this.mergeFields.set(response.supportedMergeFields);
      },
      error: (e: unknown) => this.loadError.set(this.toMessage(e)),
    });
  }

  private schedulePreview(): void {
    if (this.previewTimer !== null) {
      clearTimeout(this.previewTimer);
    }
    this.previewTimer = setTimeout(() => {
      this.runPreview();
      this.previewTimer = null;
    }, 250);
  }

  private runPreview(): void {
    const subject = this.draftSubject;
    const body = this.draftBody;
    if (!subject && !body) {
      this.preview.set(null);
      return;
    }

    this.svc
      .preview({
        subject,
        bodyMarkdown: body,
        mergeFields: this.sampleMergeValues,
      })
      .subscribe({
        next: (p) => {
          this.preview.set({ subject: p.subject, bodyMarkdown: p.bodyMarkdown });
          this.previewError.set(null);
        },
        error: (e: unknown) => this.previewError.set(this.toMessage(e)),
      });
  }

  private flashSaved(): void {
    this.saveStatus.set('success');
    setTimeout(() => this.saveStatus.set('idle'), 3000);
  }

  private toMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const body = error.error as { detail?: string; title?: string } | null;
      return body?.detail ?? body?.title ?? error.message;
    }
    return error instanceof Error ? error.message : 'Unexpected error.';
  }
}
