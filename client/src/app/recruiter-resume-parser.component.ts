import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ParsedResume,
  ResumeParseService,
} from './core/recruiter/resume-parse.service';
import { QaAlertComponent, QaButtonComponent, QaInputComponent } from './core/ui';

/**
 * Recruiter "Resume parser" tool — Track B affordance for the
 * PR-82 backend.
 *
 * Drop a PDF / DOC / DOCX / TXT (max 5 MB, allowlisted by the backend).
 * The parser returns the candidate's likely name, email, phone, headline,
 * skills, and work history. The recruiter can edit each field inline,
 * then copy the structured result as JSON to paste into whatever
 * candidate-create flow they prefer (we don't yet expose a recruiter-side
 * create-candidate endpoint, so JSON-on-clipboard is the bridge until
 * Phase 9 lands the offer-generator + recruiter candidate create).
 *
 * ADA: drop-zone is keyboard-reachable (Enter / Space triggers the file
 * picker), drag-over state has both a visual and an `aria-busy` cue,
 * skill chips are removable via keyboard, and every form field is bound
 * to a real <label>.
 */
@Component({
  selector: 'app-recruiter-resume-parser',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, QaAlertComponent, QaButtonComponent, QaInputComponent],
  template: `
    <div class="page">
      <header class="page__header">
        <p class="eyebrow">Recruiter</p>
        <h1>Resume parser</h1>
        <p class="lede">
          Drop a resume to extract the candidate's headline, contact, skills,
          and recent work history. Edit any field, then copy the structured
          result to use elsewhere.
        </p>
      </header>

      <!-- ── Drop zone ────────────────────────────────────────────── -->
      <section
        class="drop"
        [class.drop--active]="dragActive()"
        [class.drop--busy]="parsing()"
        (dragenter)="onDragEnter($event)"
        (dragover)="onDragOver($event)"
        (dragleave)="onDragLeave($event)"
        (drop)="onDrop($event)"
        (click)="pickFile()"
        (keydown.enter)="pickFile()"
        (keydown.space)="pickFile()"
        role="button"
        tabindex="0"
        [attr.aria-busy]="parsing()"
        [attr.aria-label]="'Drop a resume here or activate to pick a file'"
      >
        @if (parsing()) {
          <strong>Parsing…</strong>
          <p>One moment.</p>
        } @else if (lastFileName(); as f) {
          <strong>Loaded: {{ f }}</strong>
          <p>Drop another resume to replace it.</p>
        } @else {
          <strong>Drag a resume here</strong>
          <p>or activate to pick a file. PDF, DOC, DOCX, or TXT up to 5 MB.</p>
        }
        <input
          #picker
          type="file"
          accept=".pdf,.doc,.docx,.txt,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/plain"
          class="drop__input"
          (change)="onPicked($event)"
          aria-hidden="true"
          tabindex="-1"
        />
      </section>

      @if (parseError()) {
        <qa-alert tone="danger" role="alert">{{ parseError() }}</qa-alert>
      }
      @if (copyStatus() === 'copied') {
        <qa-alert tone="success" role="status">Copied JSON to clipboard.</qa-alert>
      }

      @if (parsed(); as p) {
        <section class="results" aria-labelledby="results-heading">
          <header class="results__head">
            <h2 id="results-heading">Parsed fields</h2>
            <div class="results__actions">
              <qa-button variant="primary" (click)="copyJson()">Copy JSON</qa-button>
              <qa-button variant="ghost" (click)="reset()">Clear</qa-button>
            </div>
          </header>

          <div class="grid">
            <qa-input label="Full name" [(ngModel)]="draftFullName"></qa-input>
            <qa-input label="Email" [(ngModel)]="draftEmail" type="email"></qa-input>
            <qa-input label="Phone" [(ngModel)]="draftPhone"></qa-input>
            <qa-input label="Headline" [(ngModel)]="draftHeadline"></qa-input>
          </div>

          <h3>Skills</h3>
          <div class="skills">
            @for (skill of draftSkills(); track skill; let i = $index) {
              <span class="chip">
                {{ skill }}
                <button
                  type="button"
                  class="chip__remove"
                  (click)="removeSkill(i)"
                  [attr.aria-label]="'Remove ' + skill"
                >×</button>
              </span>
            }
            <form class="skill-add" (submit)="addSkill($event)">
              <label class="visually-hidden" for="skill-input">Add skill</label>
              <input
                id="skill-input"
                type="text"
                [(ngModel)]="newSkill"
                name="newSkill"
                placeholder="Add skill and press Enter"
              />
            </form>
          </div>

          <h3>Work history</h3>
          @if (p.workHistory.length === 0) {
            <p class="hint">No prior roles extracted.</p>
          } @else {
            <ol class="history">
              @for (job of p.workHistory; track $index) {
                <li>
                  <strong>{{ job.title }}</strong>
                  <span class="history__employer">{{ job.employer }}</span>
                  <small>{{ job.startDate ?? '?' }} → {{ job.endDate ?? 'current' }}</small>
                </li>
              }
            </ol>
          }
        </section>
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    .page { width: min(900px, 100%); margin: 0 auto; padding: 2rem 1.5rem 3rem; }
    .eyebrow { color: var(--color-primary, #1a3a8f); font-size: 12px; font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase; margin: 0 0 0.5rem; }
    h1 { margin: 0 0 0.5rem; font-size: 1.75rem; }
    .lede { color: var(--color-fg-muted, #5d6577); margin: 0 0 1.5rem; max-width: 56ch; }

    .drop {
      position: relative;
      border: 2px dashed var(--color-border, #d8dde7);
      background: var(--color-canvas, #f5f7fb);
      border-radius: 14px;
      padding: 2rem 1.5rem;
      text-align: center;
      cursor: pointer;
      transition: border-color 120ms ease, background-color 120ms ease;
    }
    .drop:hover, .drop:focus-visible { border-color: var(--color-primary, #1a3a8f); }
    .drop:focus-visible { outline: 3px solid var(--color-primary, #1a3a8f); outline-offset: 2px; }
    .drop--active { border-color: var(--color-primary, #1a3a8f); background: var(--color-primary-soft, #e7ecf6); }
    .drop--busy { opacity: 0.7; cursor: wait; }
    .drop strong { display: block; font-size: 1.05rem; margin-bottom: 0.25rem; }
    .drop p { color: var(--color-fg-muted, #5d6577); margin: 0; }
    .drop__input { position: absolute; opacity: 0; pointer-events: none; }

    .results {
      margin-top: 1.5rem;
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 12px;
      padding: 1.25rem;
    }
    .results__head {
      display: flex; align-items: center; justify-content: space-between;
      gap: 1rem; flex-wrap: wrap; margin-bottom: 1rem;
    }
    .results__head h2 { margin: 0; font-size: 1.125rem; }
    .results__actions { display: flex; gap: 0.5rem; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; margin-bottom: 1rem; }
    @media (max-width: 720px) { .grid { grid-template-columns: 1fr; } }

    h3 { font-size: 0.9rem; text-transform: uppercase; letter-spacing: 0.06em; color: var(--color-fg-muted, #5d6577); margin: 1.25rem 0 0.5rem; }

    .skills { display: flex; flex-wrap: wrap; gap: 0.375rem; align-items: center; }
    .chip {
      display: inline-flex; align-items: center; gap: 0.375rem;
      padding: 0.25rem 0.625rem;
      border-radius: 999px;
      background: var(--color-primary-soft, #e7ecf6);
      color: var(--color-primary, #1a3a8f);
      font-size: 0.85rem;
    }
    .chip__remove {
      background: none; border: 0; color: inherit;
      font-size: 1rem; line-height: 1;
      cursor: pointer; padding: 0; margin: 0;
    }
    .skill-add input {
      border: 1px dashed var(--color-border, #d8dde7);
      background: var(--color-canvas, #f5f7fb);
      border-radius: 999px;
      padding: 0.25rem 0.75rem;
      font-size: 0.85rem;
    }
    .skill-add input:focus-visible {
      outline: 3px solid var(--color-primary, #1a3a8f);
      outline-offset: 1px;
    }

    .history { list-style: none; margin: 0; padding: 0; display: grid; gap: 0.5rem; }
    .history li {
      padding: 0.625rem 0.75rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 8px;
      display: grid;
      grid-template-columns: 1fr 1fr auto;
      gap: 0.75rem;
      align-items: baseline;
    }
    .history__employer { color: var(--color-fg-muted, #5d6577); font-size: 0.9rem; }
    .history small { color: var(--color-fg-muted, #5d6577); }

    .hint { color: var(--color-fg-muted, #5d6577); }
    .visually-hidden {
      position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px;
      overflow: hidden; clip: rect(0,0,0,0); white-space: nowrap; border: 0;
    }
  `,
})
export class RecruiterResumeParserComponent {
  private svc = inject(ResumeParseService);

  protected readonly parsed = signal<ParsedResume | null>(null);
  protected readonly parsing = signal(false);
  protected readonly parseError = signal<string | null>(null);
  protected readonly dragActive = signal(false);
  protected readonly lastFileName = signal<string | null>(null);
  protected readonly copyStatus = signal<'idle' | 'copied'>('idle');

  protected readonly draftSkills = signal<string[]>([]);
  protected draftFullName = '';
  protected draftEmail = '';
  protected draftPhone = '';
  protected draftHeadline = '';
  protected newSkill = '';

  protected readonly hasResult = computed(() => this.parsed() !== null);

  protected pickFile(): void {
    const input = document.querySelector<HTMLInputElement>('.drop__input');
    input?.click();
  }

  protected onPicked(event: Event): void {
    const target = event.target as HTMLInputElement;
    const file = target.files?.[0];
    if (file) {
      this.uploadAndParse(file);
    }
    // Reset so the same filename can be re-picked in a row.
    target.value = '';
  }

  protected onDragEnter(event: DragEvent): void {
    event.preventDefault();
    this.dragActive.set(true);
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragActive.set(true);
  }

  protected onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.dragActive.set(false);
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragActive.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.uploadAndParse(file);
    }
  }

  protected addSkill(event: Event): void {
    event.preventDefault();
    const trimmed = this.newSkill.trim();
    if (!trimmed) return;
    if (this.draftSkills().includes(trimmed)) {
      this.newSkill = '';
      return;
    }
    this.draftSkills.update((s) => [...s, trimmed]);
    this.newSkill = '';
  }

  protected removeSkill(index: number): void {
    this.draftSkills.update((s) => s.filter((_, i) => i !== index));
  }

  protected copyJson(): void {
    const payload = {
      fullName: this.draftFullName || null,
      email: this.draftEmail || null,
      phoneNumber: this.draftPhone || null,
      headline: this.draftHeadline || null,
      skills: this.draftSkills(),
      workHistory: this.parsed()?.workHistory ?? [],
    };
    const json = JSON.stringify(payload, null, 2);

    void navigator.clipboard.writeText(json).then(() => {
      this.copyStatus.set('copied');
      setTimeout(() => this.copyStatus.set('idle'), 3000);
    });
  }

  protected reset(): void {
    this.parsed.set(null);
    this.lastFileName.set(null);
    this.draftFullName = '';
    this.draftEmail = '';
    this.draftPhone = '';
    this.draftHeadline = '';
    this.draftSkills.set([]);
    this.parseError.set(null);
  }

  private uploadAndParse(file: File): void {
    this.parsing.set(true);
    this.parseError.set(null);
    this.lastFileName.set(file.name);

    this.svc.parse(file).subscribe({
      next: (result) => {
        this.parsed.set(result);
        this.draftFullName = result.fullName ?? '';
        this.draftEmail = result.email ?? '';
        this.draftPhone = result.phoneNumber ?? '';
        this.draftHeadline = result.headline ?? '';
        this.draftSkills.set(result.skills ?? []);
        this.parsing.set(false);
      },
      error: (e: unknown) => {
        this.parseError.set(this.toMessage(e));
        this.parsing.set(false);
      },
    });
  }

  private toMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const body = error.error as { detail?: string; title?: string } | null;
      return body?.detail ?? body?.title ?? error.message;
    }
    return error instanceof Error ? error.message : 'Unexpected error parsing the resume.';
  }
}
