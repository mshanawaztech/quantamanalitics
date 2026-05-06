import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';
import {
  CandidateApplication,
  CandidateProfile,
  CandidateProfileService,
} from './core/candidate/candidate-profile.service';

@Component({
  selector: 'app-candidate-dashboard',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="page">
      <section class="hero">
        <div>
          <p class="eyebrow">Candidate portal</p>
          <h1>Keep your profile ready before the first application lands.</h1>
          <p>
            This slice gives candidates a protected home for core profile data
            and resume state while recruiter-facing workflow lands next.
          </p>
        </div>
        <div class="hero-card">
          <p class="label">Portal status</p>
          <strong>{{ auth.isAuthenticated() ? 'Signed in' : 'Awaiting sign in' }}</strong>
          <span>{{ auth.email() || 'Use Auth0 to create or continue access.' }}</span>
        </div>
      </section>

      @if (!auth.isAuthenticated()) {
        <section class="gate-card">
          <h2>Candidate access starts with sign in.</h2>
          <p>
            Use the hosted Auth0 flow to sign in or create an account, then
            return here to finish your candidate profile.
          </p>
          <div class="actions">
            <button type="button" class="primary" (click)="auth.loginWithRedirect()">Sign in or sign up</button>
            <a routerLink="/jobs">Browse jobs first</a>
          </div>
        </section>
      } @else {
        <section class="workspace">
          <article class="profile-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Profile basics</p>
                <h2>Candidate dashboard skeleton</h2>
              </div>
              @if (loading()) {
                <span class="pill">Loading</span>
              }
            </div>

            @if (error()) {
              <p class="error">{{ error() }}</p>
            }

            <form class="profile-form" (ngSubmit)="saveProfile()">
              <label>
                Full name
                <input
                  type="text"
                  name="fullName"
                  [(ngModel)]="fullName"
                  placeholder="Jane Candidate"
                />
              </label>

              <label>
                Email
                <input
                  type="email"
                  name="email"
                  [(ngModel)]="email"
                  placeholder="jane@example.com"
                  required
                />
              </label>

              <label>
                Phone number
                <input
                  type="text"
                  name="phoneNumber"
                  [(ngModel)]="phoneNumber"
                  placeholder="+1 555 010 2040"
                />
              </label>

              <label>
                Headline
                <input
                  type="text"
                  name="headline"
                  [(ngModel)]="headline"
                  placeholder="Cloud recruiter focused on data talent"
                />
              </label>

              <label class="full-width">
                Summary
                <textarea
                  rows="6"
                  name="summary"
                  [(ngModel)]="summary"
                  placeholder="Share your specialty, target roles, and what makes you a strong fit."
                ></textarea>
              </label>

              <div class="actions full-width">
                <button type="submit" class="primary" [disabled]="saving()">
                  {{ saving() ? 'Saving…' : 'Save profile' }}
                </button>
                @if (savedMessage()) {
                  <span class="success">{{ savedMessage() }}</span>
                }
              </div>
            </form>
          </article>

          <aside class="resume-card">
            <p class="eyebrow">Resume state</p>
            <h2>Upload once, reuse later.</h2>
            <p>
              Resume metadata is stored on your profile now so future job
              application flows can reuse it instead of starting from scratch.
            </p>

            <dl class="meta-list">
              <dt>Tenant</dt>
              <dd>{{ profile()?.tenantId || 'pending' }}</dd>
              <dt>Current file</dt>
              <dd>{{ profile()?.resumeFileName || 'No resume uploaded yet' }}</dd>
              <dt>Last uploaded</dt>
              <dd>{{ profile()?.resumeUploadedAtUtc || '—' }}</dd>
            </dl>

            <label class="upload-field">
              Resume file
              <input type="file" accept=".pdf,.doc,.docx,application/pdf,.msword,.docx" (change)="onFileSelected($event)" />
            </label>

            @if (uploadMessage()) {
              <p class="success">{{ uploadMessage() }}</p>
            }

            <button type="button" class="primary" (click)="uploadResume()" [disabled]="!selectedFile() || uploading()">
              {{ uploading() ? 'Uploading…' : 'Upload resume' }}
            </button>
            <p class="hint">PDF, DOC, or DOCX up to 5 MB. Cloudflare R2 credentials are required in the environment.</p>
          </aside>
        </section>

        <section class="history-card">
          <div class="section-head">
            <div>
              <p class="eyebrow">Applied jobs</p>
              <h2>Track the roles already in motion.</h2>
            </div>
            @if (applicationsLoading()) {
              <span class="pill">Loading</span>
            }
          </div>

          @if (applicationsError()) {
            <p class="error">{{ applicationsError() }}</p>
          } @else {
            <div class="history-list">
              @for (application of applications(); track application.id) {
                <article class="history-item">
                  <div class="history-top">
                    <div>
                      <strong>{{ application.jobTitle }}</strong>
                      <p>{{ application.location }}</p>
                    </div>
                    <span class="status-pill">{{ application.status }}</span>
                  </div>
                  <p class="history-meta">Applied {{ application.appliedAtUtc }}</p>
                  @if (application.note) {
                    <p class="history-note">{{ application.note }}</p>
                  }
                  <a [routerLink]="['/jobs', application.jobSlug]">View role</a>
                </article>
              } @empty {
                <article class="history-empty">
                  <h3>No submitted applications yet</h3>
                  <p>
                    Your candidate profile is ready. Browse the live job board
                    and submit your first application to see it here.
                  </p>
                  <a routerLink="/jobs">Browse jobs</a>
                </article>
              }
            </div>
          }
        </section>
      }
    </main>
  `,
  styles: `
    .page { width: min(1120px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero, .workspace { display: grid; gap: 1.25rem; }
    .hero { grid-template-columns: 1.2fr 0.8fr; margin-bottom: 1.5rem; }
    .hero-card, .gate-card, .profile-card, .resume-card, .history-card, .history-item, .history-empty {
      padding: 1.6rem;
      border-radius: 1.5rem;
      background: rgb(255 251 244 / 0.88);
      border: 1px solid rgb(87 70 42 / 0.14);
      box-shadow: 0 1rem 2rem rgb(64 47 22 / 0.06);
    }
    .workspace { grid-template-columns: 1.1fr 0.9fr; align-items: start; }
    .eyebrow { margin: 0 0 0.7rem; color: #9a3412; text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0 0 0.8rem; font-size: clamp(2.1rem, 3.8vw, 4.2rem); line-height: 0.98; }
    h2 { margin: 0; font-size: 1.4rem; }
    p { color: #554d41; line-height: 1.7; }
    .label { margin: 0 0 0.4rem; font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.08em; color: #9a3412; font-weight: 800; }
    .hero-card strong { display: block; font-size: 1.2rem; margin-bottom: 0.45rem; }
    .hero-card span, .hint { color: #6b6255; }
    .section-head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 1rem; }
    .pill { padding: 0.35rem 0.7rem; border-radius: 999px; background: #e7e5e4; color: #44403c; font-size: 0.85rem; font-weight: 700; }
    .profile-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.95rem; }
    label { display: grid; gap: 0.35rem; color: #3f372c; font-weight: 600; }
    input, textarea {
      width: 100%;
      padding: 0.85rem 0.95rem;
      border-radius: 0.9rem;
      border: 1px solid #d8c8b0;
      background: #fffdf9;
      font: inherit;
    }
    .full-width { grid-column: 1 / -1; }
    .actions { display: flex; align-items: center; gap: 0.8rem; flex-wrap: wrap; }
    .primary {
      padding: 0.9rem 1rem;
      border: 0;
      border-radius: 999px;
      background: #1f2937;
      color: #fff8ee;
      font-weight: 700;
      cursor: pointer;
    }
    .primary[disabled] { opacity: 0.65; cursor: wait; }
    a { color: #9a3412; font-weight: 700; text-decoration: none; }
    .meta-list { display: grid; grid-template-columns: auto 1fr; gap: 0.4rem 0.9rem; margin: 1.25rem 0; }
    .meta-list dt { font-weight: 700; color: #3f372c; }
    .meta-list dd { margin: 0; color: #554d41; word-break: break-word; }
    .upload-field { margin-bottom: 1rem; }
    .history-card { margin-top: 1.25rem; }
    .history-list { display: grid; gap: 0.9rem; }
    .history-item, .history-empty { background: #fffdf9; border: 1px solid #eadcc8; }
    .history-top { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; }
    .history-top strong { display: block; margin-bottom: 0.35rem; font-size: 1.05rem; }
    .history-top p, .history-meta, .history-note { margin: 0; }
    .history-meta { color: #8b5e34; font-size: 0.92rem; }
    .history-note { margin-top: 0.65rem; }
    .status-pill {
      padding: 0.35rem 0.7rem;
      border-radius: 999px;
      background: #e7e5e4;
      color: #44403c;
      font-size: 0.85rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .history-empty h3 { margin: 0 0 0.75rem; font-size: 1.15rem; }
    .success { color: #166534; font-weight: 600; }
    .error { color: #b91c1c; font-weight: 600; }
    @media (max-width: 900px) {
      .hero, .workspace, .profile-form { grid-template-columns: 1fr; }
      .history-top { flex-direction: column; }
    }
  `,
})
export class CandidateDashboardComponent {
  protected auth = inject(AuthService);
  protected me = inject(MeService);
  private candidateProfile = inject(CandidateProfileService);

  protected profile = signal<CandidateProfile | null>(null);
  protected applications = signal<CandidateApplication[]>([]);
  protected loading = signal(false);
  protected applicationsLoading = signal(false);
  protected saving = signal(false);
  protected uploading = signal(false);
  protected error = signal<string | null>(null);
  protected applicationsError = signal<string | null>(null);
  protected savedMessage = signal<string | null>(null);
  protected uploadMessage = signal<string | null>(null);
  protected selectedFile = signal<File | null>(null);

  protected fullName = '';
  protected email = '';
  protected phoneNumber = '';
  protected headline = '';
  protected summary = '';

  constructor() {
    effect(() => {
      if (!this.auth.isAuthenticated()) {
        this.profile.set(null);
        this.applications.set([]);
        this.selectedFile.set(null);
        this.error.set(null);
        this.applicationsError.set(null);
        return;
      }

      if (this.me.loading()) {
        this.loading.set(true);
        return;
      }

      if (this.me.error()) {
        this.loading.set(false);
        this.error.set(this.me.error());
        return;
      }

      const tenantId = this.me.data()?.tenantId;
      if (!tenantId) {
        this.loading.set(false);
        this.error.set('Tenant context is still loading. Refresh once the home /me panel shows your tenant.');
        return;
      }

      this.fetchProfile();
      this.fetchApplications();
    });
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.item(0) ?? null);
    this.uploadMessage.set(null);
  }

  protected saveProfile(): void {
    this.saving.set(true);
    this.savedMessage.set(null);
    this.error.set(null);

    this.candidateProfile
      .save({
        email: this.email,
        fullName: this.fullName || null,
        phoneNumber: this.phoneNumber || null,
        headline: this.headline || null,
        summary: this.summary || null,
      })
      .subscribe({
        next: (profile) => {
          this.bindProfile(profile);
          this.saving.set(false);
          this.savedMessage.set('Profile saved.');
        },
        error: (error: unknown) => {
          this.saving.set(false);
          this.error.set(this.toErrorMessage(error));
        },
      });
  }

  protected uploadResume(): void {
    const file = this.selectedFile();
    if (!file) {
      return;
    }

    this.uploading.set(true);
    this.uploadMessage.set(null);
    this.error.set(null);

    this.candidateProfile.uploadResume(file).subscribe({
      next: (profile) => {
        this.bindProfile(profile);
        this.selectedFile.set(null);
        this.uploading.set(false);
        this.uploadMessage.set('Resume uploaded.');
      },
      error: (error: unknown) => {
        this.uploading.set(false);
        this.error.set(this.toErrorMessage(error));
      },
    });
  }

  private fetchProfile(): void {
    this.loading.set(true);
    this.error.set(null);

    this.candidateProfile.load().subscribe({
      next: (profile) => {
        this.bindProfile(profile);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.error.set(this.toErrorMessage(error));
      },
    });
  }

  private fetchApplications(): void {
    this.applicationsLoading.set(true);
    this.applicationsError.set(null);

    this.candidateProfile.applications().subscribe({
      next: (response) => {
        this.applications.set(response.items);
        this.applicationsLoading.set(false);
      },
      error: (error: unknown) => {
        this.applications.set([]);
        this.applicationsLoading.set(false);
        this.applicationsError.set(this.toErrorMessage(error));
      },
    });
  }

  private bindProfile(profile: CandidateProfile): void {
    this.profile.set(profile);
    this.fullName = profile.fullName ?? '';
    this.email = profile.email;
    this.phoneNumber = profile.phoneNumber ?? '';
    this.headline = profile.headline ?? '';
    this.summary = profile.summary ?? '';
    this.error.set(null);
  }

  private toErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return error.error?.detail ?? error.error?.title ?? error.message;
    }

    return error instanceof Error ? error.message : 'Unexpected candidate portal error.';
  }
}
