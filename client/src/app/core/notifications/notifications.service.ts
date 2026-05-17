import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

/**
 * Client wrapper for the in-app notifications feed shipped in PR-57.
 *
 * The store is exposed as a small set of signals so any consumer (the
 * header bell, a future "notifications drawer", a contractor-portal
 * banner) can subscribe without each one running its own HTTP roundtrip.
 * One scoped HTTP fetch per signed-in session; consumers `markRead`
 * locally and the optimistic state is reconciled in place.
 */
@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/api/v1/me/notifications`;

  readonly items = signal<NotificationItem[]>([]);
  readonly unreadCount = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly hasUnread = computed(() => this.unreadCount() > 0);

  refresh(): void {
    this.loading.set(true);
    this.error.set(null);

    this.http.get<NotificationFeedResponse>(this.base).subscribe({
      next: (response) => {
        this.items.set(response.items ?? []);
        this.unreadCount.set(response.unreadCount ?? 0);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(this.toMessage(err));
        this.loading.set(false);
      },
    });
  }

  markRead(id: string): void {
    // Optimistic — flip locally first, the server is the tiebreaker.
    const before = this.items();
    const nowIso = new Date().toISOString();
    const after = before.map((n) =>
      n.id === id && !n.readAtUtc ? { ...n, readAtUtc: nowIso } : n,
    );
    if (after === before) return;

    this.items.set(after);
    this.unreadCount.update((c) => Math.max(0, c - 1));

    this.http.post(`${this.base}/${id}/read`, null).subscribe({
      error: () => {
        // Roll back if the server rejected the flip.
        this.items.set(before);
        this.unreadCount.update((c) => c + 1);
      },
    });
  }

  markAllRead(): void {
    const before = this.items();
    if (!before.some((n) => !n.readAtUtc)) return;

    const nowIso = new Date().toISOString();
    this.items.set(before.map((n) => (n.readAtUtc ? n : { ...n, readAtUtc: nowIso })));
    this.unreadCount.set(0);

    this.http.post(`${this.base}/read-all`, null).subscribe({
      error: () => {
        this.items.set(before);
        this.unreadCount.set(before.filter((n) => !n.readAtUtc).length);
      },
    });
  }

  private toMessage(error: unknown): string {
    if (error instanceof Error) {
      return error.message;
    }
    return 'Unable to load notifications.';
  }
}

export interface NotificationItem {
  id: string;
  kind: string;
  title: string;
  body: string;
  targetUrl: string | null;
  createdAtUtc: string;
  readAtUtc: string | null;
}

interface NotificationFeedResponse {
  items: NotificationItem[];
  unreadCount: number;
}
