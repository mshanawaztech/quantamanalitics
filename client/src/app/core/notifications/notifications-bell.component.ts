import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Router } from '@angular/router';
import { NotificationItem, NotificationsService } from './notifications.service';

/**
 * Header bell + dropdown feed for the in-app notifications shipped in PR-57.
 *
 * The bell renders an unread-count badge sourced from the
 * NotificationsService signal store. Clicking it opens a popover with the
 * last 50 notifications grouped Unread → Read. Clicking a row marks it
 * read optimistically and navigates to its target URL if one is set.
 * The popover closes on Escape, on outside click, and on route change so
 * navigation stays unblocked.
 *
 * ADA: the trigger is a real <button> with `aria-haspopup="menu"`,
 * `aria-expanded`, and `aria-label` that includes the unread count for
 * screen readers; the popover root is `role="menu"` and rows are
 * `role="menuitem"`.
 */
@Component({
  selector: 'app-notifications-bell',
  standalone: true,
  imports: [NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="bell" #root>
      <button
        type="button"
        class="bell__trigger"
        (click)="toggle()"
        [attr.aria-expanded]="open()"
        aria-haspopup="menu"
        [attr.aria-label]="triggerLabel()"
      >
        <span class="bell__icon" aria-hidden="true">
          <!-- Inline SVG keeps the bundle off any icon-pack dependency. -->
          <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M18 8a6 6 0 1 0-12 0c0 7-3 9-3 9h18s-3-2-3-9" />
            <path d="M13.73 21a2 2 0 0 1-3.46 0" />
          </svg>
        </span>
        @if (svc.hasUnread()) {
          <span class="bell__badge" aria-hidden="true">{{ badgeText() }}</span>
        }
      </button>

      @if (open()) {
        <div class="bell__panel" role="menu" aria-label="Notifications">
          <header class="bell__head">
            <h2>Notifications</h2>
            @if (svc.hasUnread()) {
              <button
                type="button"
                class="bell__mark-all"
                (click)="svc.markAllRead()"
              >Mark all read</button>
            }
          </header>

          @if (svc.loading()) {
            <div class="bell__state" role="status">Loading…</div>
          } @else if (svc.error()) {
            <div class="bell__state bell__state--error" role="alert">
              {{ svc.error() }}
              <button type="button" class="bell__retry" (click)="svc.refresh()">Retry</button>
            </div>
          } @else if (svc.items().length === 0) {
            <div class="bell__state">You're all caught up.</div>
          } @else {
            <ul class="bell__list">
              @for (n of unread(); track n.id) {
                <li class="bell__row bell__row--unread">
                  <ng-container *ngTemplateOutlet="row; context: { $implicit: n }" />
                </li>
              }
              @if (read().length > 0) {
                <li class="bell__sep" role="separator">Earlier</li>
                @for (n of read(); track n.id) {
                  <li class="bell__row">
                    <ng-container *ngTemplateOutlet="row; context: { $implicit: n }" />
                  </li>
                }
              }
            </ul>
          }
        </div>
      }

      <ng-template #row let-n>
        <button
          type="button"
          role="menuitem"
          class="bell__btn"
          (click)="activate(n)"
        >
          <span class="bell__row-kind">{{ kindLabel(n.kind) }}</span>
          <strong class="bell__row-title">{{ n.title }}</strong>
          <span class="bell__row-body">{{ n.body }}</span>
          <span class="bell__row-meta">{{ relative(n.createdAtUtc) }}</span>
        </button>
      </ng-template>
    </div>
  `,
  styles: `
    :host { position: relative; display: inline-flex; }

    .bell { position: relative; }

    .bell__trigger {
      position: relative;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 40px;
      height: 40px;
      border: 1px solid var(--color-border, #d8dde7);
      background: var(--color-surface, #fff);
      color: var(--color-fg, #102046);
      border-radius: 999px;
      cursor: pointer;
      transition: background-color 120ms ease;
    }
    .bell__trigger:hover { background: var(--color-primary-soft, #e7ecf6); }

    .bell__badge {
      position: absolute;
      top: 2px;
      right: 2px;
      min-width: 18px;
      height: 18px;
      padding: 0 5px;
      border-radius: 999px;
      background: #c0392b;
      color: #fff;
      font-size: 11px;
      font-weight: 600;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      line-height: 1;
    }

    .bell__panel {
      position: absolute;
      top: calc(100% + 8px);
      right: 0;
      width: min(380px, calc(100vw - 24px));
      max-height: 480px;
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 12px;
      box-shadow: 0 12px 32px rgba(20, 30, 60, 0.18);
      display: flex;
      flex-direction: column;
      overflow: hidden;
      z-index: 100;
    }

    .bell__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 12px 16px;
      border-bottom: 1px solid var(--color-border, #d8dde7);
    }
    .bell__head h2 { font-size: 14px; margin: 0; font-weight: 600; }
    .bell__mark-all {
      background: none;
      border: 0;
      color: var(--color-primary, #1a3a8f);
      font-size: 12px;
      font-weight: 600;
      cursor: pointer;
      padding: 4px 6px;
      border-radius: 6px;
    }
    .bell__mark-all:hover { background: var(--color-primary-soft, #e7ecf6); }

    .bell__state {
      padding: 20px 16px;
      text-align: center;
      color: var(--color-fg-muted, #5d6577);
      font-size: 13px;
    }
    .bell__state--error { color: #b03939; }
    .bell__retry {
      display: block;
      margin: 8px auto 0;
      background: none;
      border: 1px solid currentColor;
      color: inherit;
      border-radius: 6px;
      padding: 4px 10px;
      cursor: pointer;
      font-size: 12px;
    }

    .bell__list {
      list-style: none;
      margin: 0;
      padding: 0;
      overflow-y: auto;
    }
    .bell__row { border-top: 1px solid var(--color-border, #d8dde7); }
    .bell__row:first-child { border-top: 0; }
    .bell__row--unread { background: var(--color-primary-soft, #e7ecf6); }

    .bell__sep {
      padding: 6px 16px;
      font-size: 11px;
      font-weight: 600;
      letter-spacing: 0.04em;
      text-transform: uppercase;
      color: var(--color-fg-muted, #5d6577);
      background: var(--color-canvas, #f5f7fb);
      border-top: 1px solid var(--color-border, #d8dde7);
      border-bottom: 1px solid var(--color-border, #d8dde7);
    }

    .bell__btn {
      display: grid;
      grid-template-columns: 1fr auto;
      grid-template-areas:
        "kind meta"
        "title title"
        "body body";
      width: 100%;
      padding: 12px 16px;
      gap: 4px 12px;
      background: none;
      border: 0;
      text-align: left;
      cursor: pointer;
      color: inherit;
    }
    .bell__btn:hover { background: rgba(20, 30, 60, 0.04); }
    .bell__row-kind { grid-area: kind; font-size: 11px; color: var(--color-fg-muted, #5d6577); text-transform: uppercase; letter-spacing: 0.04em; font-weight: 600; }
    .bell__row-meta { grid-area: meta; font-size: 11px; color: var(--color-fg-muted, #5d6577); }
    .bell__row-title { grid-area: title; font-size: 14px; }
    .bell__row-body { grid-area: body; font-size: 13px; color: var(--color-fg-muted, #5d6577); display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
  `,
})
export class NotificationsBellComponent {
  protected readonly svc = inject(NotificationsService);
  private router = inject(Router);

  protected readonly open = signal(false);
  protected readonly root = viewChild<ElementRef<HTMLElement>>('root');

  protected readonly unread = computed(() => this.svc.items().filter((n) => !n.readAtUtc));
  protected readonly read = computed(() => this.svc.items().filter((n) => n.readAtUtc));

  protected readonly badgeText = computed(() => {
    const c = this.svc.unreadCount();
    return c > 99 ? '99+' : String(c);
  });

  protected readonly triggerLabel = computed(() => {
    const c = this.svc.unreadCount();
    return c === 0 ? 'Notifications' : `Notifications (${c} unread)`;
  });

  constructor() {
    // Lazy-load on first construction. RouterLink in the marketing-shell
    // already gates this component on auth, so we know there's a session.
    this.svc.refresh();
  }

  protected toggle(): void {
    const next = !this.open();
    this.open.set(next);
    if (next) {
      // Refresh on open so the count stays honest if the user has had
      // the page open in another tab.
      this.svc.refresh();
    }
  }

  protected activate(n: NotificationItem): void {
    if (!n.readAtUtc) {
      this.svc.markRead(n.id);
    }
    this.open.set(false);
    if (n.targetUrl) {
      this.router.navigateByUrl(n.targetUrl);
    }
  }

  protected kindLabel(kind: string): string {
    // application.stage_changed → Application
    const head = kind.split('.')[0] ?? kind;
    return head.charAt(0).toUpperCase() + head.slice(1);
  }

  protected relative(value: string): string {
    const then = new Date(value).getTime();
    if (Number.isNaN(then)) return value;
    const diffSec = Math.round((Date.now() - then) / 1000);
    const abs = Math.abs(diffSec);
    if (abs < 60) return 'just now';
    if (abs < 3600) return `${Math.round(diffSec / 60)} min ago`;
    if (abs < 86_400) return `${Math.round(diffSec / 3600)} h ago`;
    if (abs < 86_400 * 30) return `${Math.round(diffSec / 86_400)} d ago`;
    return new Date(value).toLocaleDateString();
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (!this.open()) return;
    const root = this.root()?.nativeElement;
    if (root && !root.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.open.set(false);
  }
}
