import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccessService } from './core/auth/access.service';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';
import { AdminAuditService, AuditLogEntry } from './core/admin/admin-audit.service';

interface SubjectSummary {
  subject: string;
  count: number;
  lastSeenUtc: string;
}

interface EntitySummary {
  entityType: string;
  count: number;
}

@Component({
  selector: 'app-admin-audit-console',
  standalone: true,
  imports: [FormsModule],
  template: `
    <main class="page">
      <section class="hero">
        <div>
          <p class="eyebrow">Admin audit</p>
          <h1>Review who changed what, and when.</h1>
          <p>
            This console gives PlatformAdmin users a tenant-scoped evidence surface for actor,
            entity, and change-history review.
          </p>
        </div>
        <div class="hero-card">
          <p class="label">Access review</p>
          <strong>{{ accessLabel() }}</strong>
          <span>{{ me.data()?.roles?.join(', ') || 'No role claims' }}</span>
          <p class="hint">Permissions: {{ me.data()?.permissions?.join(', ') || 'derived locally' }}</p>
        </div>
      </section>

      @if (!auth.isAuthenticated()) {
        <section class="gate-card">
          <h2>Admin access starts with sign in.</h2>
          <p>Use Auth0 to sign in, then return here to review the tenant audit history.</p>
          <button type="button" class="primary" (click)="auth.loginWithRedirect()">Sign in</button>
        </section>
      } @else if (!access.isPlatformAdmin()) {
        <section class="gate-card">
          <h2>Platform admin access required.</h2>
          <p>
            This view is restricted because raw audit history can expose sensitive operational
            details across recruiting, payroll, and client workflows.
          </p>
        </section>
      } @else {
        <section class="summary-grid">
          <article class="summary-card">
            <p class="eyebrow">Review pulse</p>
            <h2>Recent actors</h2>
            <div class="chip-list">
              @for (actor of recentSubjects(); track actor.subject) {
                <button type="button" class="chip" (click)="focusSubject(actor.subject)">
                  {{ actor.subject }} · {{ actor.count }}
                </button>
              } @empty {
                <span class="empty">No actors in current result set.</span>
              }
            </div>
          </article>

          <article class="summary-card">
            <p class="eyebrow">History focus</p>
            <h2>Entity types</h2>
            <div class="chip-list">
              @for (entity of entityTypes(); track entity.entityType) {
                <button type="button" class="chip" (click)="focusEntityType(entity.entityType)">
                  {{ entity.entityType }} · {{ entity.count }}
                </button>
              } @empty {
                <span class="empty">No entity history in current result set.</span>
              }
            </div>
          </article>
        </section>

        <section class="workspace">
          <article class="filters-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Audit query</p>
                <h2>Filter the review surface</h2>
              </div>
              @if (loading()) {
                <span class="pill">Loading</span>
              }
            </div>

            <form class="filter-form" (ngSubmit)="runQuery()">
              <label>
                Actor subject
                <input type="text" name="authSubject" [(ngModel)]="authSubject" placeholder="auth0|user" />
              </label>
              <label>
                Action
                <select name="action" [(ngModel)]="action">
                  <option value="">All actions</option>
                  <option value="Created">Created</option>
                  <option value="Updated">Updated</option>
                  <option value="Deleted">Deleted</option>
                </select>
              </label>
              <label>
                Entity type
                <input type="text" name="entityType" [(ngModel)]="entityType" placeholder="Application" />
              </label>
              <label>
                Entity id
                <input type="text" name="entityId" [(ngModel)]="entityId" placeholder="Guid / natural key" />
              </label>
              <label>
                From
                <input type="datetime-local" name="fromUtc" [(ngModel)]="fromUtc" />
              </label>
              <label>
                To
                <input type="datetime-local" name="toUtc" [(ngModel)]="toUtc" />
              </label>
              <label>
                Page size
                <select name="pageSize" [(ngModel)]="pageSize">
                  <option [ngValue]="50">50</option>
                  <option [ngValue]="100">100</option>
                  <option [ngValue]="200">200</option>
                </select>
              </label>
              <div class="actions full-width">
                <button type="submit" class="primary" [disabled]="loading()">
                  {{ loading() ? 'Refreshing…' : 'Run query' }}
                </button>
                <button type="button" class="secondary" (click)="resetFilters()" [disabled]="loading()">
                  Clear filters
                </button>
              </div>
            </form>

            @if (error()) {
              <p class="error">{{ error() }}</p>
            }
          </article>

          <article class="results-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Results</p>
                <h2>{{ entries().length }} rows in the current window</h2>
              </div>
              <span class="pill">Page size {{ pageSize }}</span>
            </div>

            <div class="results-list">
              @for (entry of entries(); track entry.id) {
                <article class="result-item">
                  <div class="result-top">
                    <div>
                      <strong>{{ entry.entityType }} · {{ entry.action }}</strong>
                      <p>{{ entry.entityId }}</p>
                    </div>
                    <span class="timeline-date">{{ formatUtc(entry.recordedAtUtc) }}</span>
                  </div>

                  <dl class="meta-list">
                    <dt>Actor</dt>
                    <dd>
                      @if (entry.authSubject) {
                        <button type="button" class="link-button" (click)="focusSubject(entry.authSubject)">
                          {{ entry.authSubject }}
                        </button>
                      } @else {
                        system
                      }
                    </dd>
                    <dt>Action</dt>
                    <dd>{{ entry.action }}</dd>
                    <dt>Entity history</dt>
                    <dd>
                      <button type="button" class="link-button" (click)="focusEntity(entry.entityType, entry.entityId)">
                        Focus this record
                      </button>
                    </dd>
                  </dl>

                  @if (entry.metadataJson) {
                    <pre>{{ prettyMetadata(entry.metadataJson) }}</pre>
                  }
                </article>
              } @empty {
                <article class="result-empty">
                  <h3>No audit rows match these filters.</h3>
                  <p>Try widening the time window or clearing the entity and actor filters.</p>
                </article>
              }
            </div>
          </article>
        </section>
      }
    </main>
  `,
  styles: `
    :host { display:block; }
    .page { display:grid; gap:1.5rem; }
    .hero, .workspace, .summary-grid { display:grid; gap:1.5rem; }
    .hero { grid-template-columns: minmax(0, 1.6fr) minmax(18rem, 0.9fr); align-items:start; }
    .summary-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .workspace { grid-template-columns: minmax(18rem, 0.95fr) minmax(0, 1.35fr); align-items:start; }
    .hero-card, .summary-card, .filters-card, .results-card, .gate-card {
      border: 1px solid var(--color-line, #d8dee8);
      border-radius: 1.5rem;
      background: #fffdf8;
      padding: 1.5rem;
      box-shadow: 0 20px 45px rgba(15, 23, 42, 0.07);
    }
    .eyebrow { margin: 0 0 0.35rem; color: #a6461a; font-weight: 700; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.82rem; }
    h1, h2, h3, p { margin-top: 0; }
    .label, .hint, .empty, .timeline-date { color: #6b7280; }
    .section-head, .result-top { display:flex; justify-content:space-between; gap:1rem; align-items:flex-start; }
    .pill { border-radius:999px; border:1px solid #d8dee8; padding:0.2rem 0.65rem; font-size:0.86rem; color:#334155; }
    .filter-form { display:grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap:1rem; }
    label { display:grid; gap:0.45rem; font-weight:600; color:#1f2937; }
    input, select {
      width:100%; border:1px solid #d7cfbf; border-radius:1rem; padding:0.8rem 0.95rem; background:#fff;
      font: inherit;
    }
    .actions { display:flex; gap:0.75rem; align-items:center; }
    .full-width { grid-column: 1 / -1; }
    .primary, .secondary {
      border-radius:999px; padding:0.85rem 1.2rem; font-weight:700; font:inherit; cursor:pointer;
    }
    .primary { border:0; background:#1e293b; color:#fff; }
    .secondary { border:1px solid #d7cfbf; background:#fff; color:#1f2937; }
    .results-list { display:grid; gap:1rem; }
    .result-item, .result-empty {
      border:1px solid #ece4d3; border-radius:1.25rem; padding:1rem; background:#fff;
    }
    .meta-list { display:grid; grid-template-columns:max-content 1fr; gap:0.35rem 0.75rem; margin:1rem 0; }
    .meta-list dt { font-weight:700; color:#334155; }
    .meta-list dd { margin:0; color:#475569; }
    .chip-list { display:flex; gap:0.6rem; flex-wrap:wrap; }
    .chip, .link-button {
      border:1px solid #d7cfbf; background:#fff; color:#1f2937; border-radius:999px; padding:0.45rem 0.8rem; cursor:pointer; font:inherit;
    }
    .link-button { padding:0; border:0; border-radius:0; background:transparent; color:#0f4c81; text-decoration:underline; }
    .error { color:#b42318; font-weight:600; }
    pre {
      margin:0; background:#172033; color:#f8fafc; padding:0.95rem; border-radius:1rem; overflow:auto; font-size:0.84rem;
      white-space:pre-wrap; word-break:break-word;
    }
    @media (max-width: 980px) {
      .hero, .workspace, .summary-grid, .filter-form { grid-template-columns: 1fr; }
    }
  `,
})
export class AdminAuditConsoleComponent {
  readonly auth = inject(AuthService);
  readonly access = inject(AccessService);
  readonly me = inject(MeService);
  private readonly audit = inject(AdminAuditService);

  readonly entries = signal<AuditLogEntry[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');

  authSubject = '';
  action = '';
  entityType = '';
  entityId = '';
  fromUtc = '';
  toUtc = '';
  pageSize = 100;

  readonly recentSubjects = computed<SubjectSummary[]>(() => {
    const bySubject = new Map<string, SubjectSummary>();

    for (const entry of this.entries()) {
      const subject = entry.authSubject?.trim();
      if (!subject) {
        continue;
      }

      const current = bySubject.get(subject);
      if (!current) {
        bySubject.set(subject, { subject, count: 1, lastSeenUtc: entry.recordedAtUtc });
        continue;
      }

      current.count += 1;
      if (entry.recordedAtUtc > current.lastSeenUtc) {
        current.lastSeenUtc = entry.recordedAtUtc;
      }
    }

    return [...bySubject.values()]
      .sort((a, b) => b.count - a.count || b.lastSeenUtc.localeCompare(a.lastSeenUtc))
      .slice(0, 8);
  });

  readonly entityTypes = computed<EntitySummary[]>(() => {
    const counts = new Map<string, number>();

    for (const entry of this.entries()) {
      counts.set(entry.entityType, (counts.get(entry.entityType) ?? 0) + 1);
    }

    return [...counts.entries()]
      .map(([entityType, count]) => ({ entityType, count }))
      .sort((a, b) => b.count - a.count || a.entityType.localeCompare(b.entityType));
  });

  constructor() {
    effect(() => {
      if (this.auth.isAuthenticated() && this.access.isPlatformAdmin()) {
        this.runQuery();
      } else {
        this.entries.set([]);
        this.error.set('');
        this.loading.set(false);
      }
    });
  }

  accessLabel(): string {
    return this.access.isPlatformAdmin() ? 'Platform-admin review ready' : 'Restricted';
  }

  runQuery(): void {
    if (!this.auth.isAuthenticated() || !this.access.isPlatformAdmin()) {
      return;
    }

    this.loading.set(true);
    this.error.set('');

    this.audit.query({
      authSubject: this.authSubject,
      action: this.action,
      entityType: this.entityType,
      entityId: this.entityId,
      from: this.toUtcIso(this.fromUtc),
      to: this.toUtcIso(this.toUtc),
      pageSize: this.pageSize,
    }).subscribe({
      next: (response) => {
        this.entries.set(response.items);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        this.error.set(this.describeError(err));
        this.entries.set([]);
      },
    });
  }

  resetFilters(): void {
    this.authSubject = '';
    this.action = '';
    this.entityType = '';
    this.entityId = '';
    this.fromUtc = '';
    this.toUtc = '';
    this.pageSize = 100;
    this.runQuery();
  }

  focusSubject(subject: string): void {
    this.authSubject = subject;
    this.runQuery();
  }

  focusEntityType(entityType: string): void {
    this.entityType = entityType;
    this.runQuery();
  }

  focusEntity(entityType: string, entityId: string): void {
    this.entityType = entityType;
    this.entityId = entityId;
    this.runQuery();
  }

  formatUtc(value: string): string {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }

  prettyMetadata(metadataJson: string | null): string {
    if (!metadataJson) {
      return '';
    }

    try {
      return JSON.stringify(JSON.parse(metadataJson), null, 2);
    } catch {
      return metadataJson;
    }
  }

  private toUtcIso(value: string): string | null {
    if (!value) {
      return null;
    }

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? null : date.toISOString();
  }

  private describeError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (typeof err.error?.detail === 'string') {
        return err.error.detail;
      }

      return err.status === 0
        ? 'Audit log is unavailable right now. Retry in a moment.'
        : `Audit query failed (${err.status}).`;
    }

    return 'Audit query failed. Retry in a moment.';
  }
}
