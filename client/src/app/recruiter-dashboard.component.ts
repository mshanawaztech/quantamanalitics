import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AccessService } from './core/auth/access.service';
import { AuthService } from './core/auth/auth.service';
import { MeService } from './core/auth/me.service';
import {
  EmailTemplateCatalog,
  EmailTemplatePreviewResponse,
  RecruiterEmailTemplate,
  RecruiterApplication,
  RecruiterApplicationsBoard,
  RecruiterBulkStatusMoveResponse,
  RecruiterCandidateActivityItem,
  RecruiterNotificationItem,
  RecruiterInvoiceHandoffResponse,
  RecruiterInvoiceReadyItem,
  RecruiterJob,
  RecruiterPortalService,
  RecruiterStripeFallbackItem,
} from './core/recruiter/recruiter-portal.service';
import { ParsedResumeResult } from './core/resume/resume-parse.models';

@Component({
  selector: 'app-recruiter-dashboard',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="page">
      <section class="hero">
        <div>
          <p class="eyebrow">Recruiter portal</p>
          <h1>Jobs and applications on one tenant-safe operating surface.</h1>
          <p>
            This internal workspace gives recruiting users a protected place to
            create jobs and triage candidate applications.
          </p>
        </div>
        <div class="hero-card">
          <p class="label">Access</p>
          <strong>{{ accessLabel() }}</strong>
          <span>{{ roleLabel() }}</span>
          <a routerLink="/recruiter/pipeline">Open search pipeline</a>
        </div>
      </section>

      @if (auth.isAuthenticated() && hasRecruitingAccess()) {
        <section class="workspace">
          <article class="jobs-card">
            <p class="eyebrow">Team pulse</p>
            <h2>Keep attention on the bottlenecks.</h2>
            @if (loadingPulse()) {
              <span class="pill">Loading</span>
            } @else {
              <div class="board-toolbar">
                <article class="toolbar-stat">
                  <span class="label">Open pipeline</span>
                  <strong>{{ openPipelineCount() }}</strong>
                </article>
                <article class="toolbar-stat">
                  <span class="label">Active jobs</span>
                  <strong>{{ activeJobCount() }}</strong>
                </article>
                <article class="toolbar-stat">
                  <span class="label">Stuck &gt; 7d</span>
                  <strong>{{ totalStuckCount() }}</strong>
                </article>
                <article class="toolbar-stat">
                  <span class="label">Ready to invoice</span>
                  <strong>{{ invoiceReady().length }}</strong>
                </article>
              </div>
            }
          </article>

          <article class="activity-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Notification center</p>
                <h2>See what needs action right now.</h2>
              </div>
              @if (loadingNotifications()) {
                <span class="pill">Loading</span>
              } @else if (attentionCount() > 0) {
                <span class="stuck-pill">{{ attentionCount() }} attention</span>
              }
            </div>

            @if (notificationsError()) {
              <p class="error" role="alert" aria-live="assertive">{{ notificationsError() }}</p>
              <button type="button" class="secondary" (click)="fetchNotifications()">Retry notifications</button>
            } @else {
              <div class="activity-list">
                @for (item of notifications(); track item.id) {
                  <article class="activity-item" [attr.data-severity]="item.severity">
                    <div class="activity-top">
                      <strong>{{ item.title }}</strong>
                      <span class="timeline-date">{{ formatUtc(item.occurredAtUtc) }}</span>
                    </div>
                    <p>{{ item.detail }}</p>
                    <div class="activity-top">
                      <span class="pill">{{ item.category }}</span>
                      @if (item.actionHref && item.actionLabel) {
                        <a [routerLink]="item.actionHref">{{ item.actionLabel }}</a>
                      }
                    </div>
                  </article>
                } @empty {
                  <article class="activity-empty">
                    <h3>All clear for now</h3>
                    <p>No new recruiter-facing alerts are waiting right now.</p>
                  </article>
                }
              </div>
            }
          </article>
        </section>
      }

      @if (!auth.isAuthenticated()) {
        <section class="gate-card">
          <h2>Recruiter access starts with sign in.</h2>
          <p>Use Auth0 to sign in, then return here to open the recruiter workspace.</p>
          <button type="button" class="primary" (click)="auth.loginWithRedirect()">Sign in</button>
        </section>
      } @else if (!hasRecruitingAccess()) {
        <section class="gate-card">
          <h2>Recruiting access required.</h2>
          <p>
            Your current session is valid, but this portal requires recruiting
            access such as Recruiter, HR admin, manager, or PlatformAdmin.
          </p>
        </section>
      } @else {
        <section class="workspace">
          <article class="jobs-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Jobs workspace</p>
                <h2>Create and review active roles</h2>
              </div>
              @if (loadingJobs()) {
                <span class="pill">Loading</span>
              }
            </div>

            <form class="job-form" (ngSubmit)="createJob()">
              <label>
                Title
                <input type="text" name="title" [(ngModel)]="jobTitle" required />
              </label>
              <label>
                Location
                <input type="text" name="location" [(ngModel)]="jobLocation" required />
              </label>
              <label class="full-width">
                Summary
                <input type="text" name="summary" [(ngModel)]="jobSummary" required />
              </label>
              <label class="full-width">
                Description
                <textarea rows="5" name="description" [(ngModel)]="jobDescription" required></textarea>
              </label>
              <label class="checkbox full-width">
                <input type="checkbox" name="published" [(ngModel)]="jobPublished" />
                Publish immediately
              </label>
              <div class="actions full-width">
                <button type="submit" class="primary" [disabled]="creatingJob()">
                  {{ creatingJob() ? 'Creating…' : 'Create job' }}
                </button>
              </div>
            </form>

            @if (jobsError()) {
              <p class="error" role="alert" aria-live="assertive">{{ jobsError() }}</p>
            }

            <div class="jobs-list">
              @for (job of jobs(); track job.id) {
                <article class="job-item">
                  <div>
                    <strong>{{ job.title }}</strong>
                    <p>{{ job.location }} · {{ job.isPublished ? 'Published' : 'Draft' }}</p>
                  </div>
                  <span class="slug">{{ job.slug }}</span>
                </article>
              } @empty {
                <p class="empty">No tenant jobs yet.</p>
              }
            </div>
          </article>

          <article class="board-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Applications board</p>
                <h2>Move candidate intake across the funnel</h2>
                <p class="board-copy">
                  Drag cards between stages, or select multiple candidates for a bulk move.
                </p>
              </div>
              @if (loadingApplications()) {
                <span class="pill">Loading</span>
              }
            </div>

            <div class="board-toolbar">
              <div class="toolbar-stat">
                <span class="label">Selected</span>
                <strong>{{ selectedCount() }}</strong>
              </div>
              <div class="toolbar-stat">
                <span class="label">Stuck &gt; 7d</span>
                <strong>{{ totalStuckCount() }}</strong>
              </div>
              <label class="bulk-control">
                Bulk move to
                <select [ngModel]="bulkMoveStatus" (ngModelChange)="bulkMoveStatus = $event">
                  @for (status of movableStatuses; track status) {
                    <option [ngValue]="status">{{ status }}</option>
                  }
                </select>
              </label>
              <button
                type="button"
                class="secondary"
                (click)="bulkMoveSelected()"
                [disabled]="selectedCount() === 0 || movingApplications()"
              >
                {{ movingApplications() ? 'Moving…' : 'Move selected' }}
              </button>
              <button type="button" class="secondary" (click)="clearSelection()" [disabled]="selectedCount() === 0">
                Clear selection
              </button>
            </div>

            @if (applicationsError()) {
              <p class="error" role="alert" aria-live="assertive">{{ applicationsError() }}</p>
            }

            <div class="board">
              @for (column of columns; track column) {
                <section
                  class="column"
                  [attr.data-active-drop]="draggedApplicationId() ? 'true' : null"
                  (dragover)="allowDrop($event)"
                  (drop)="dropOnColumn(column)"
                >
                  <div class="column-head">
                    <div>
                      <h3>{{ column }}</h3>
                      <p>{{ applicationsByStatus(column).length }} candidates</p>
                    </div>
                    @if (stuckCount(column) > 0) {
                      <span class="stuck-pill">{{ stuckCount(column) }} stuck</span>
                    }
                  </div>
                  @for (application of applicationsByStatus(column); track application.id) {
                    <article
                      class="application-card"
                      draggable="true"
                      [attr.data-dragging]="draggedApplicationId() === application.id ? 'true' : null"
                      [attr.data-selected]="isSelected(application.id) ? 'true' : null"
                      (dragstart)="dragStart(application.id)"
                      (dragend)="dragEnd()"
                    >
                      <label class="card-select">
                        <input
                          type="checkbox"
                          [checked]="isSelected(application.id)"
                          (change)="toggleSelection(application.id, $any($event.target).checked)"
                        />
                        <span>Select</span>
                      </label>
                      <strong>{{ application.candidateName }}</strong>
                      <p>{{ application.jobTitle }}</p>
                      <p>{{ application.candidateEmail }}</p>
                      <div class="card-meta">
                        <span>{{ application.daysInStage }} day{{ application.daysInStage === 1 ? '' : 's' }} in stage</span>
                        @if (application.isStuck) {
                          <span class="stuck-tag">Needs attention</span>
                        }
                      </div>
                      @if (application.note) {
                        <p>{{ application.note }}</p>
                      }
                      <label>
                        Move to
                        <select [ngModel]="application.status" (ngModelChange)="move(application, $event)">
                          @for (status of columns; track status) {
                            <option [ngValue]="status">{{ status }}</option>
                          }
                        </select>
                      </label>
                    </article>
                  } @empty {
                    <p class="empty">No applications in this stage.</p>
                  }
                </section>
              }
            </div>
          </article>
        </section>

        <section class="resume-review-card">
          <div class="section-head">
            <div>
              <p class="eyebrow">Resume review</p>
              <h2>Parse a resume before you create the candidate record.</h2>
              <p class="board-copy">
                This preview uses the deterministic parser stub so recruiters can inspect contact details,
                skills, and work history before committing a profile.
              </p>
            </div>
            @if (parsingResume()) {
              <span class="pill">Parsing</span>
            }
          </div>

          <div class="resume-review-grid">
            <div class="resume-upload-panel">
              <label>
                Candidate resume
                <input
                  type="file"
                  accept=".pdf,.doc,.docx,.txt,application/pdf,.msword,.docx,text/plain"
                  (change)="onResumeFileSelected($event)"
                />
              </label>

              <div class="actions">
                <button
                  type="button"
                  class="primary"
                  (click)="parseResumePreview()"
                  [disabled]="!selectedResumeFile() || parsingResume()"
                >
                  {{ parsingResume() ? 'Parsing…' : 'Parse resume' }}
                </button>
              </div>

              @if (resumeParseMessage()) {
                <p class="success">{{ resumeParseMessage() }}</p>
              }

              @if (resumeParseError()) {
                <p class="error" role="alert" aria-live="assertive">{{ resumeParseError() }}</p>
              }
            </div>

            <div class="resume-preview-panel">
              @if (parsedResume()) {
                <dl class="meta-list">
                  <dt>Name</dt>
                  <dd>{{ parsedResume()!.fullName || 'Not detected' }}</dd>
                  <dt>Email</dt>
                  <dd>{{ parsedResume()!.email || 'Not detected' }}</dd>
                  <dt>Phone</dt>
                  <dd>{{ parsedResume()!.phoneNumber || 'Not detected' }}</dd>
                  <dt>Headline</dt>
                  <dd>{{ parsedResume()!.headline || 'Not detected' }}</dd>
                </dl>

                @if (parsedResume()!.skills.length > 0) {
                  <div class="chip-list">
                    @for (skill of parsedResume()!.skills; track skill) {
                      <span class="chip">{{ skill }}</span>
                    }
                  </div>
                }

                @if (parsedResume()!.workHistory.length > 0) {
                  <div class="work-history">
                    @for (item of parsedResume()!.workHistory; track item.employer + item.title + item.startDate) {
                      <article class="work-item">
                        <strong>{{ item.title }}</strong>
                        <p>{{ item.employer }}</p>
                        <span>{{ formatDateRange(item.startDate, item.endDate) }}</span>
                      </article>
                    }
                  </div>
                }
              } @else {
                <article class="activity-empty">
                  <h3>No parsed resume preview yet</h3>
                  <p>Upload a candidate resume and run the parser to preview the extracted fields here.</p>
                </article>
              }
            </div>
          </div>
        </section>

        <section class="template-card">
          <div class="section-head">
            <div>
              <p class="eyebrow">Email templates</p>
              <h2>Reuse outreach instead of rewriting every invite and update.</h2>
              <p class="board-copy">
                Start from recruiter-friendly presets, customize the copy, and preview the merge fields before a workflow uses it.
              </p>
            </div>
            @if (loadingEmailTemplates()) {
              <span class="pill">Loading</span>
            }
          </div>

          <div class="template-grid">
            <article class="template-editor">
              <div class="template-toolbar">
                <label>
                  Starter preset
                  <select [ngModel]="templatePresetSlug" (ngModelChange)="loadTemplatePreset($event)">
                    @for (preset of emailTemplateCatalog()?.presets ?? []; track preset.slug) {
                      <option [ngValue]="preset.slug">{{ preset.name }}</option>
                    }
                  </select>
                </label>

                @if (currentTemplate()) {
                  <span class="pill">Editing saved template</span>
                }
              </div>

              <form class="template-form" (ngSubmit)="saveEmailTemplate()">
                <label>
                  Slug
                  <input
                    type="text"
                    name="templateSlug"
                    [(ngModel)]="templateSlug"
                    [disabled]="!!currentTemplate()"
                    required
                  />
                </label>
                <label>
                  Name
                  <input type="text" name="templateName" [(ngModel)]="templateName" required />
                </label>
                @if (currentTemplate()) {
                  <p class="template-hint full-width">
                    Slug stays fixed after creation so recruiter workflows keep a stable reference.
                  </p>
                }
                <label class="full-width">
                  Subject
                  <input type="text" name="templateSubject" [(ngModel)]="templateSubject" required />
                </label>
                <label class="full-width">
                  Body
                  <textarea rows="10" name="templateBodyMarkdown" [(ngModel)]="templateBodyMarkdown" required></textarea>
                </label>
                <div class="actions full-width">
                  <button type="submit" class="primary" [disabled]="savingEmailTemplate()">
                    {{ savingEmailTemplate() ? 'Saving…' : (currentTemplate() ? 'Update template' : 'Create template') }}
                  </button>
                  <button type="button" class="secondary" (click)="previewCurrentEmailTemplate()" [disabled]="previewingEmailTemplate()">
                    {{ previewingEmailTemplate() ? 'Rendering…' : 'Preview merge fields' }}
                  </button>
                  @if (currentTemplate()) {
                    <button type="button" class="secondary" (click)="deleteCurrentEmailTemplate()">
                      Delete template
                    </button>
                  }
                </div>
              </form>

              @if (emailTemplateMessage()) {
                <p class="success">{{ emailTemplateMessage() }}</p>
              }

              @if (emailTemplateError()) {
                <p class="error" role="alert" aria-live="assertive">{{ emailTemplateError() }}</p>
              }

              <div class="merge-field-panel">
                <p class="label">Supported merge fields</p>
                <div class="chip-list">
                  @for (field of availableTemplateFields(); track field) {
                    <span class="chip">{{ field }}</span>
                  }
                </div>
              </div>
            </article>

            <article class="template-preview">
              <div class="section-head compact">
                <div>
                  <p class="eyebrow">Preview</p>
                  <h3>Sample recruiter output</h3>
                </div>
              </div>

              <div class="template-preview-fields">
                <label>
                  Candidate
                  <input type="text" name="previewCandidateName" [(ngModel)]="previewCandidateName" />
                </label>
                <label>
                  Candidate email
                  <input type="text" name="previewCandidateEmail" [(ngModel)]="previewCandidateEmail" />
                </label>
                <label>
                  Company
                  <input type="text" name="previewCompany" [(ngModel)]="previewCompany" />
                </label>
                <label>
                  Job title
                  <input type="text" name="previewJobTitle" [(ngModel)]="previewJobTitle" />
                </label>
                <label>
                  Interview date
                  <input type="text" name="previewInterviewDate" [(ngModel)]="previewInterviewDate" />
                </label>
                <label>
                  Recruiter
                  <input type="text" name="previewRecruiterName" [(ngModel)]="previewRecruiterName" />
                </label>
                <label>
                  Portal link
                  <input type="text" name="previewPortalLink" [(ngModel)]="previewPortalLink" />
                </label>
                <label>
                  Offer amount
                  <input type="text" name="previewOfferAmount" [(ngModel)]="previewOfferAmount" />
                </label>
                <label>
                  Onboarding due
                  <input type="text" name="previewOnboardingDueDate" [(ngModel)]="previewOnboardingDueDate" />
                </label>
              </div>

              @if (emailTemplatePreview()) {
                <div class="rendered-preview">
                  <p class="label">Subject</p>
                  <strong>{{ emailTemplatePreview()!.subject }}</strong>
                  <p class="label">Body</p>
                  <pre>{{ emailTemplatePreview()!.bodyMarkdown }}</pre>
                </div>
              } @else {
                <article class="activity-empty">
                  <h3>No preview rendered yet</h3>
                  <p>Use Preview merge fields to render the current subject and body with recruiter-safe sample values.</p>
                </article>
              }
            </article>
          </div>

          <div class="saved-template-list">
            @for (template of emailTemplates(); track template.id) {
              <article class="saved-template-item">
                <div>
                  <strong>{{ template.name }}</strong>
                  <p>{{ template.slug }}</p>
                  <span>Updated {{ formatUtc(template.updatedAtUtc) }}</span>
                </div>
                <button type="button" class="secondary" (click)="editEmailTemplate(template.id)">
                  Edit
                </button>
              </article>
            } @empty {
              <p class="empty">No tenant templates saved yet. Start from a preset and save your first recruiter-ready template.</p>
            }
          </div>
        </section>

        <section class="activity-card">
          <div class="section-head">
            <div>
              <p class="eyebrow">Candidate activity</p>
              <h2>See the newest movement without opening each profile.</h2>
            </div>
            @if (loadingActivity()) {
              <span class="pill">Loading</span>
            }
          </div>

          @if (activityError()) {
            <p class="error" role="alert" aria-live="assertive">{{ activityError() }}</p>
          } @else {
            <div class="activity-list">
              @for (item of candidateActivity(); track item.id) {
                <article class="activity-item">
                  <div class="activity-top">
                    <div>
                      <strong>{{ item.candidateName }}</strong>
                      <p>{{ item.candidateEmail }}</p>
                    </div>
                    <span class="timeline-date">{{ formatUtc(item.occurredAtUtc) }}</span>
                  </div>
                  <div class="activity-event">
                    <span class="timeline-marker" [attr.data-kind]="timelineKind(item.eventType)"></span>
                    <div>
                      <strong>{{ item.title }}</strong>
                      <p>
                        {{ item.jobTitle || 'Candidate workflow' }}
                        @if (item.status) {
                          · {{ item.status }}
                        }
                      </p>
                    </div>
                  </div>
                  @if (item.detail) {
                    <p class="activity-detail">{{ item.detail }}</p>
                  }
                </article>
              } @empty {
                <article class="activity-empty">
                  <h3>No timeline activity yet</h3>
                  <p>
                    New applications, recruiter notes, interview changes, and
                    candidate progress updates will surface here once the
                    timeline feed starts recording events.
                  </p>
                </article>
              }
            </div>
          }
        </section>

        @if (hasPayrollAccess()) {
          <section class="invoice-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">Invoice staging</p>
                <h2>Approved weeks ready for billing handoff</h2>
              </div>
              @if (loadingInvoiceReady()) {
                <span class="pill">Loading</span>
              }
            </div>

            @if (invoiceReadyError()) {
              <p class="error" role="alert" aria-live="assertive">{{ invoiceReadyError() }}</p>
            } @else {
              <div class="invoice-list">
                @for (item of invoiceReady(); track item.timesheetId) {
                  <article class="invoice-item">
                    <strong>{{ item.contractorEmail }}</strong>
                    <p>Week of {{ item.weekStartUtc }}</p>
                    <p>{{ item.regularHours }} reg · {{ item.overtimeHours }} OT · {{ item.paidTimeOffHours }} PTO</p>
                    <p>{{ item.payableHours }} payable hours · Approved {{ item.approvedAtUtc || 'pending timestamp' }}</p>
                  </article>
                } @empty {
                  <p class="empty">No approved time is staged for invoicing yet.</p>
                }
              </div>
            }
          </section>

          <section class="handoff-card">
            <div class="section-head">
              <div>
                <p class="eyebrow">QuickBooks + Stripe baseline</p>
                <h2>Turn approved time into a billing handoff package</h2>
              </div>
              @if (loadingHandoff()) {
                <span class="pill">Loading</span>
              }
            </div>

            <div class="handoff-actions">
              <button type="button" class="primary" (click)="downloadQuickBooksCsv()" [disabled]="downloadingQuickBooks()">
                {{ downloadingQuickBooks() ? 'Preparing CSV…' : 'Download QuickBooks CSV' }}
              </button>
              <button type="button" class="secondary" (click)="fetchInvoiceHandoff()" [disabled]="loadingHandoff()">
                Refresh Stripe fallback preview
              </button>
            </div>

            @if (handoffError()) {
              <p class="error" role="alert" aria-live="assertive">{{ handoffError() }}</p>
            } @else if (invoiceHandoff()) {
              <div class="handoff-summary">
                <article class="handoff-stat">
                  <span class="label">CSV file</span>
                  <strong>{{ invoiceHandoff()!.quickBooksFileName }}</strong>
                </article>
                <article class="handoff-stat">
                  <span class="label">Approved weeks</span>
                  <strong>{{ invoiceHandoff()!.approvedTimesheetCount }}</strong>
                </article>
                <article class="handoff-stat">
                  <span class="label">Payable hours</span>
                  <strong>{{ invoiceHandoff()!.totalPayableHours }}</strong>
                </article>
                <article class="handoff-stat">
                  <span class="label">Stripe batch</span>
                  <strong>{{ invoiceHandoff()!.stripeFallback.batchReference }}</strong>
                </article>
              </div>

              <div class="handoff-preview">
                @for (item of stripeFallbackItems(); track item.timesheetId) {
                  <article class="handoff-item">
                    <strong>{{ item.contractorEmail }}</strong>
                    <p>{{ item.description }}</p>
                    <p>{{ item.quantity }} {{ item.unit }} · {{ item.collectionMethod }}</p>
                  </article>
                } @empty {
                  <p class="empty">No Stripe fallback lines are ready yet.</p>
                }
              </div>
            }
          </section>
        }
      }
    </main>
  `,
  styles: `
    .page { width: min(1320px, 100%); margin: 0 auto; padding: 2rem 0 3rem; }
    .hero, .workspace { display: grid; gap: 1.25rem; }
    .hero { grid-template-columns: 1.15fr 0.85fr; margin-bottom: 1.5rem; }
    .workspace { grid-template-columns: minmax(0, 0.95fr) minmax(0, 1.15fr); align-items: start; }
    .hero-card, .gate-card, .jobs-card, .board-card, .resume-review-card, .invoice-card, .handoff-card, .activity-card, .column, .application-card, .invoice-item, .handoff-item, .handoff-stat, .activity-item, .activity-empty {
      border-radius: 1.5rem;
      background: var(--color-surface, #ffffff);
      border: 1px solid var(--color-border, #d8dee9);
      box-shadow: var(--shadow-md);
    }
    .hero-card, .gate-card, .jobs-card, .board-card, .resume-review-card, .invoice-card, .handoff-card, .activity-card { padding: 1.6rem; }
    .eyebrow { margin: 0 0 0.7rem; color: var(--color-primary, #1a3a8f); text-transform: uppercase; letter-spacing: 0.08em; font-size: 0.78rem; font-weight: 800; }
    h1 { margin: 0 0 0.8rem; font-size: clamp(2.1rem, 3.8vw, 4.2rem); line-height: 0.98; }
    h2, h3 { margin: 0; }
    p { color: var(--color-ink-muted, #4b5a72); line-height: 1.65; }
    .label { margin: 0 0 0.4rem; font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.08em; color: var(--color-primary, #1a3a8f); font-weight: 800; }
    .hero-card strong { display: block; font-size: 1.2rem; margin-bottom: 0.45rem; }
    .section-head { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; margin-bottom: 1rem; }
    .board-copy { margin: 0.45rem 0 0; }
    .pill { padding: 0.35rem 0.7rem; border-radius: 999px; background: var(--color-surface-alt, #f0f3f9); color: var(--color-ink, #1a2942); font-size: 0.85rem; font-weight: 700; }
    .activity-item[data-severity='warning'] { border-color: #0891b2; }
    .activity-item[data-severity='success'] { border-color: #16a34a; }
    .activity-item[data-severity='info'] { border-color: #2563eb; }
    .job-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.95rem; margin-bottom: 1.25rem; }
    label { display: grid; gap: 0.35rem; color: var(--color-ink, #1a2942); font-weight: 600; }
    input, textarea, select {
      width: 100%;
      padding: 0.85rem 0.95rem;
      border-radius: 0.9rem;
      border: 1px solid var(--color-border, #d8dee9);
      background: var(--color-surface, #ffffff);
      font: inherit;
    }
    .checkbox { display: flex; align-items: center; gap: 0.6rem; }
    .checkbox input { width: auto; }
    .full-width { grid-column: 1 / -1; }
    .actions { display: flex; gap: 0.8rem; flex-wrap: wrap; }
    .primary {
      padding: 0.9rem 1rem;
      border: 0;
      border-radius: 999px;
      background: var(--color-primary, #1a3a8f);
      color: var(--color-ink-onblue, #ffffff);
      font-weight: 700;
      cursor: pointer;
    }
    .secondary {
      padding: 0.9rem 1rem;
      border-radius: 999px;
      border: 1px solid var(--color-border, #d8dee9);
      background: var(--color-surface, #ffffff);
      color: var(--color-ink, #1a2942);
      font-weight: 700;
      cursor: pointer;
    }
    .primary[disabled], .secondary[disabled] { opacity: 0.65; cursor: wait; }
    .board-toolbar {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(9rem, max-content));
      gap: 0.85rem;
      align-items: end;
      margin-bottom: 1rem;
    }
    .toolbar-stat {
      display: grid;
      gap: 0.2rem;
      padding: 0.85rem 1rem;
      border-radius: 1rem;
      background: var(--color-surface, #ffffff);
      border: 1px solid var(--color-border, #d8dee9);
    }
    .jobs-list { display: grid; gap: 0.8rem; }
    .resume-review-grid { display: grid; grid-template-columns: minmax(16rem, 0.8fr) minmax(0, 1.2fr); gap: 1rem; align-items: start; }
    .resume-upload-panel, .resume-preview-panel { display: grid; gap: 0.9rem; }
    .template-grid { display: grid; grid-template-columns: minmax(0, 1.05fr) minmax(18rem, 0.95fr); gap: 1rem; align-items: start; }
    .template-editor, .template-preview { display: grid; gap: 0.9rem; }
    .template-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.95rem; }
    .template-hint { margin: -0.2rem 0 0; color: var(--color-ink-muted, #4b5a72); font-size: 0.92rem; }
    .merge-field-panel { display: grid; }
    .template-preview-fields { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 0.85rem; }
    .rendered-preview { display: grid; gap: 0.6rem; padding: 1rem; border-radius: 1rem; }
    .rendered-preview pre {
      margin: 0;
      white-space: pre-wrap;
      font: inherit;
      color: var(--color-ink-muted, #4b5a72);
    }
    .saved-template-list { display: grid; gap: 0.8rem; margin-top: 1rem; }
    .saved-template-item {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      padding: 1rem;
      border-radius: 1rem;
      background: var(--color-surface, #ffffff);
      border: 1px solid var(--color-border, #d8dee9);
    }
    .saved-template-item p, .saved-template-item span { margin: 0; color: var(--color-ink-muted, #4b5a72); }
    .invoice-list { display: grid; grid-template-columns: repeat(auto-fit, minmax(15rem, 1fr)); gap: 0.9rem; }
    .handoff-actions, .handoff-summary, .handoff-preview { display: grid; gap: 0.9rem; }
    .handoff-actions { grid-template-columns: repeat(auto-fit, minmax(14rem, max-content)); margin-bottom: 1rem; }
    .handoff-summary, .handoff-preview { grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr)); }
    .activity-list { display: grid; grid-template-columns: repeat(auto-fit, minmax(17rem, 1fr)); gap: 0.9rem; }
    .job-item, .application-card { padding: 1rem; }
    .invoice-item, .handoff-item, .handoff-stat, .activity-item, .activity-empty { padding: 1rem; background: var(--color-surface, #ffffff); }
    .job-item { display: grid; grid-template-columns: minmax(0, 1fr) minmax(12rem, 16rem); align-items: start; gap: 1rem; border-radius: 1.1rem; background: var(--color-surface, #ffffff); border: 1px solid var(--color-border, #d8dee9); }
    .job-item strong, .application-card strong { display: block; margin-bottom: 0.45rem; }
    .slug { font-family: monospace; color: var(--color-ink-muted, #4b5a72); overflow-wrap: anywhere; text-align: right; }
    .board-card { overflow: hidden; }
    .board {
      display: grid;
      grid-template-columns: repeat(5, minmax(15rem, 1fr));
      gap: 1rem;
      overflow-x: auto;
      padding-bottom: 0.35rem;
      align-items: start;
    }
    .column {
      padding: 1rem;
      display: grid;
      gap: 0.8rem;
      align-content: start;
      min-height: 20rem;
      min-width: 15rem;
      transition: background-color 120ms ease, border-color 120ms ease;
    }
    .column[data-active-drop='true'] { background: var(--color-primary-soft, #e7ecf6); border-color: var(--color-primary, #1a3a8f); }
    .column-head {
      display: flex;
      justify-content: space-between;
      gap: 0.8rem;
      align-items: flex-start;
    }
    .column-head p { margin: 0.35rem 0 0; color: var(--color-ink-muted, #4b5a72); font-size: 0.92rem; }
    .stuck-pill, .stuck-tag {
      display: inline-flex;
      align-items: center;
      padding: 0.25rem 0.55rem;
      border-radius: 999px;
      background: var(--color-primary-soft, #e7ecf6);
      color: var(--color-primary, #1a3a8f);
      font-size: 0.8rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .application-card {
      background: var(--color-surface, #ffffff);
      border: 1px solid var(--color-border, #d8dee9);
      display: grid;
      gap: 0.75rem;
      min-width: 0;
      transition: transform 120ms ease, box-shadow 120ms ease, border-color 120ms ease;
    }
    .application-card[data-dragging='true'] { opacity: 0.72; transform: rotate(1deg); }
    .application-card[data-selected='true'] {
      border-color: rgb(26 58 143 / 0.45);
      box-shadow: 0 0 0 0.18rem rgb(26 58 143 / 0.15);
    }
    .application-card p { margin: 0; overflow-wrap: anywhere; }
    .application-card select { min-width: 0; }
    .card-select {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      justify-self: start;
      font-size: 0.88rem;
      color: var(--color-ink-muted, #4b5a72);
      font-weight: 700;
    }
    .card-select input { width: auto; }
    .card-meta {
      display: flex;
      flex-wrap: wrap;
      gap: 0.55rem;
      align-items: center;
      color: var(--color-ink-muted, #4b5a72);
      font-size: 0.9rem;
    }
    .activity-item { display: grid; gap: 0.85rem; }
    .chip-list { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .chip {
      display: inline-flex;
      align-items: center;
      padding: 0.35rem 0.65rem;
      border-radius: 999px;
      background: var(--color-primary-soft, #e7ecf6);
      color: var(--color-primary, #1a3a8f);
      font-size: 0.9rem;
      font-weight: 700;
    }
    .work-history { display: grid; gap: 0.8rem; }
    .work-item {
      padding: 0.9rem 1rem;
      border-radius: 1rem;
      background: var(--color-surface, #ffffff);
      border: 1px solid var(--color-border, #d8dee9);
    }
    .work-item strong { display: block; margin-bottom: 0.2rem; }
    .work-item p, .work-item span { margin: 0; color: var(--color-ink-muted, #4b5a72); }
    .meta-list { display: grid; grid-template-columns: auto 1fr; gap: 0.4rem 0.9rem; margin: 0; }
    .meta-list dt { font-weight: 700; color: var(--color-ink, #1a2942); }
    .meta-list dd { margin: 0; color: var(--color-ink-muted, #4b5a72); word-break: break-word; }
    .activity-top {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: flex-start;
    }
    .activity-top strong, .activity-event strong { display: block; margin-bottom: 0.35rem; }
    .activity-top p, .activity-event p, .activity-detail { margin: 0; }
    .activity-event {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 0.8rem;
      align-items: start;
    }
    .activity-detail { color: var(--color-ink-muted, #4b5a72); }
    .timeline-date { color: var(--color-ink-muted, #4b5a72); font-size: 0.92rem; white-space: nowrap; }
    .timeline-marker {
      width: 0.9rem;
      height: 0.9rem;
      border-radius: 999px;
      margin-top: 0.3rem;
      background: #9ca3af;
      box-shadow: 0 0 0 0.25rem rgb(156 163 175 / 0.18);
    }
    .timeline-marker[data-kind='applied'] { background: #2563eb; box-shadow: 0 0 0 0.25rem rgb(37 99 235 / 0.15); }
    .timeline-marker[data-kind='interview'] { background: #4f46e5; box-shadow: 0 0 0 0.25rem rgb(79 70 229 / 0.15); }
    .timeline-marker[data-kind='offer'] { background: #0891b2; box-shadow: 0 0 0 0.25rem rgb(8 145 178 / 0.15); }
    .timeline-marker[data-kind='hired'] { background: #16a34a; box-shadow: 0 0 0 0.25rem rgb(22 163 74 / 0.15); }
    .timeline-marker[data-kind='rejected'] { background: #dc2626; box-shadow: 0 0 0 0.25rem rgb(220 38 38 / 0.15); }
    .error { color: #b91c1c; font-weight: 600; }
    @media (max-width: 980px) {
      .hero, .workspace, .job-form, .resume-review-grid, .template-grid, .template-form, .template-preview-fields { grid-template-columns: 1fr; }
      .job-item { grid-template-columns: 1fr; }
      .slug { text-align: left; }
      .activity-top { flex-direction: column; }
      .saved-template-item { align-items: start; flex-direction: column; }
    }
  `,
})
export class RecruiterDashboardComponent {
  protected auth = inject(AuthService);
  protected me = inject(MeService);
  protected access = inject(AccessService);
  private recruiter = inject(RecruiterPortalService);

  protected readonly columns = ['Applied', 'Interviewing', 'OfferSent', 'Hired', 'Rejected'];
  protected readonly movableStatuses = ['Interviewing', 'OfferSent', 'Hired', 'Rejected'];

  protected jobs = signal<RecruiterJob[]>([]);
  protected applications = signal<RecruiterApplication[]>([]);
  protected candidateActivity = signal<RecruiterCandidateActivityItem[]>([]);
  protected notifications = signal<RecruiterNotificationItem[]>([]);
  protected emailTemplateCatalog = signal<EmailTemplateCatalog | null>(null);
  protected emailTemplates = signal<RecruiterEmailTemplate[]>([]);
  protected selectedEmailTemplateId = signal<string | null>(null);
  protected emailTemplatePreview = signal<EmailTemplatePreviewResponse | null>(null);
  protected invoiceReady = signal<RecruiterInvoiceReadyItem[]>([]);
  protected invoiceHandoff = signal<RecruiterInvoiceHandoffResponse | null>(null);
  protected loadingJobs = signal(false);
  protected loadingApplications = signal(false);
  protected loadingActivity = signal(false);
  protected loadingNotifications = signal(false);
  protected loadingEmailTemplates = signal(false);
  protected loadingInvoiceReady = signal(false);
  protected loadingHandoff = signal(false);
  protected creatingJob = signal(false);
  protected savingEmailTemplate = signal(false);
  protected previewingEmailTemplate = signal(false);
  protected movingApplications = signal(false);
  protected parsingResume = signal(false);
  protected downloadingQuickBooks = signal(false);
  protected jobsError = signal<string | null>(null);
  protected applicationsError = signal<string | null>(null);
  protected activityError = signal<string | null>(null);
  protected notificationsError = signal<string | null>(null);
  protected emailTemplateError = signal<string | null>(null);
  protected emailTemplateMessage = signal<string | null>(null);
  protected invoiceReadyError = signal<string | null>(null);
  protected handoffError = signal<string | null>(null);
  protected resumeParseError = signal<string | null>(null);
  protected resumeParseMessage = signal<string | null>(null);

  protected jobTitle = '';
  protected jobLocation = '';
  protected jobSummary = '';
  protected jobDescription = '';
  protected jobPublished = true;
  protected templatePresetSlug = 'interview-invite';
  protected templateSlug = 'interview-invite';
  protected templateName = 'Interview Invite';
  protected templateSubject = 'Interview invite for {{job_title}}';
  protected templateBodyMarkdown = `Hi {{candidate_name}},

Thanks again for your interest in {{job_title}} at {{company}}.

We'd like to invite you to a recruiter screen on {{interview_date}}.

Best,
{{recruiter_name}}`;
  protected previewCandidateName = 'Jane Candidate';
  protected previewCandidateEmail = 'jane@example.com';
  protected previewCompany = 'Quantam Analytics';
  protected previewJobTitle = 'Technical Recruiter - Cloud & Data';
  protected previewInterviewDate = 'May 20, 2026 10:00 AM PT';
  protected previewRecruiterName = 'Riley Recruiter';
  protected previewPortalLink = 'https://quantamanalitics.com/jobs/technical-recruiter-cloud-and-data';
  protected previewOfferAmount = '$145,000 base';
  protected previewOnboardingDueDate = 'May 29, 2026';
  protected bulkMoveStatus = 'Interviewing';
  protected selectedApplicationIds = signal<string[]>([]);
  protected draggedApplicationId = signal<string | null>(null);
  protected selectedResumeFile = signal<File | null>(null);
  protected parsedResume = signal<ParsedResumeResult | null>(null);

  constructor() {
    effect(() => {
      if (!this.hasRecruitingAccess()) {
        return;
      }

      this.fetchJobs();
      this.fetchApplications();
      this.fetchCandidateActivity();
      this.fetchNotifications();
      this.fetchEmailTemplateCatalog();
      this.fetchEmailTemplates();
      if (this.hasPayrollAccess()) {
        this.fetchInvoiceReady();
        this.fetchInvoiceHandoff();
      } else {
        this.loadingInvoiceReady.set(false);
        this.loadingHandoff.set(false);
        this.invoiceReady.set([]);
        this.invoiceHandoff.set(null);
        this.invoiceReadyError.set(null);
        this.handoffError.set(null);
      }
    });
  }

  protected hasRecruitingAccess(): boolean {
    return this.access.canAccessRecruitingWorkspace();
  }

  protected hasPayrollAccess(): boolean {
    return this.access.canAccessPayrollBilling();
  }

  protected accessLabel(): string {
    if (!this.auth.isAuthenticated()) {
      return 'Awaiting sign in';
    }

    if (this.hasRecruitingAccess() && this.hasPayrollAccess()) {
      return 'Recruiting + payroll controls ready';
    }

    return this.hasRecruitingAccess()
      ? 'Recruiter workspace ready'
      : 'Authenticated without recruiter access';
  }

  protected roleLabel(): string {
    return this.me.data()?.roles.join(', ') || 'Role not yet available';
  }

  protected attentionCount(): number {
    return this.notifications().filter((item) => item.severity === 'warning').length;
  }

  protected openPipelineCount(): number {
    return this.applications().filter((application) =>
      application.status === 'Applied' ||
      application.status === 'Interviewing' ||
      application.status === 'OfferSent').length;
  }

  protected activeJobCount(): number {
    return this.jobs().filter((job) => job.isPublished).length;
  }

  protected loadingPulse(): boolean {
    return this.loadingJobs() || this.loadingApplications() || this.loadingInvoiceReady();
  }

  protected applicationsByStatus(status: string): RecruiterApplication[] {
    return this.applications().filter((application) => application.status === status);
  }

  protected selectedCount(): number {
    return this.selectedApplicationIds().length;
  }

  protected totalStuckCount(): number {
    return this.applications().filter((application) => application.isStuck).length;
  }

  protected stuckCount(status: string): number {
    return this.applicationsByStatus(status).filter((application) => application.isStuck).length;
  }

  protected isSelected(applicationId: string): boolean {
    return this.selectedApplicationIds().includes(applicationId);
  }

  protected stripeFallbackItems(): RecruiterStripeFallbackItem[] {
    return this.invoiceHandoff()?.stripeFallback.items ?? [];
  }

  protected availableTemplateFields(): string[] {
    return this.emailTemplateCatalog()?.supportedMergeFields ?? [];
  }

  protected currentTemplate(): RecruiterEmailTemplate | null {
    const selectedId = this.selectedEmailTemplateId();
    if (!selectedId) {
      return null;
    }

    return this.emailTemplates().find((template) => template.id === selectedId) ?? null;
  }

  protected toggleSelection(applicationId: string, selected: boolean): void {
    this.selectedApplicationIds.update((current) => {
      const next = new Set(current);
      if (selected) {
        next.add(applicationId);
      } else {
        next.delete(applicationId);
      }

      return [...next];
    });
  }

  protected clearSelection(): void {
    this.selectedApplicationIds.set([]);
  }

  protected dragStart(applicationId: string): void {
    this.draggedApplicationId.set(applicationId);
    if (!this.isSelected(applicationId)) {
      this.selectedApplicationIds.set([applicationId]);
    }
  }

  protected dragEnd(): void {
    this.draggedApplicationId.set(null);
  }

  protected allowDrop(event: DragEvent): void {
    event.preventDefault();
  }

  protected dropOnColumn(status: string): void {
    const draggedId = this.draggedApplicationId();
    if (!draggedId) {
      return;
    }

    const selectedIds = this.selectedApplicationIds();
    const idsToMove = selectedIds.includes(draggedId) ? selectedIds : [draggedId];
    this.draggedApplicationId.set(null);
    this.moveMany(idsToMove, status);
  }

  protected formatUtc(value: string): string {
    return new Date(value).toLocaleString();
  }

  protected formatDateRange(startDate: string | null, endDate: string | null): string {
    if (!startDate && !endDate) {
      return 'Dates not detected';
    }

    const start = startDate ?? 'Unknown start';
    const end = endDate ?? 'Present';
    return `${start} - ${end}`;
  }

  protected timelineKind(eventType: string): string {
    const normalized = eventType.toLowerCase();

    if (normalized.includes('applied')) {
      return 'applied';
    }

    if (normalized.includes('interview')) {
      return 'interview';
    }

    if (normalized.includes('offer')) {
      return 'offer';
    }

    if (normalized.includes('hire')) {
      return 'hired';
    }

    if (normalized.includes('reject')) {
      return 'rejected';
    }

    return 'neutral';
  }

  protected createJob(): void {
    this.creatingJob.set(true);
    this.jobsError.set(null);

    this.recruiter.createJob({
      title: this.jobTitle,
      location: this.jobLocation,
      summary: this.jobSummary,
      description: this.jobDescription,
      isPublished: this.jobPublished,
      postedOnUtc: null,
    }).subscribe({
      next: (job) => {
        this.jobs.update((current) => [job, ...current]);
        this.creatingJob.set(false);
        this.jobTitle = '';
        this.jobLocation = '';
        this.jobSummary = '';
        this.jobDescription = '';
        this.jobPublished = true;
      },
      error: (error: unknown) => {
        this.creatingJob.set(false);
        this.jobsError.set(this.toErrorMessage(error));
      },
    });
  }

  protected move(application: RecruiterApplication, status: string): void {
    if (application.status === status) {
      return;
    }

    this.recruiter.moveApplication(application.id, status).subscribe({
      next: (updated) => {
        this.applications.update((items) =>
          items.map((item) => item.id === updated.id ? updated : item),
        );
      },
      error: (error: unknown) => {
        this.applicationsError.set(this.toErrorMessage(error));
      },
    });
  }

  protected bulkMoveSelected(): void {
    this.moveMany(this.selectedApplicationIds(), this.bulkMoveStatus);
  }

  protected onResumeFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedResumeFile.set(input.files?.item(0) ?? null);
    this.resumeParseError.set(null);
    this.resumeParseMessage.set(null);
  }

  protected parseResumePreview(): void {
    const file = this.selectedResumeFile();
    if (!file) {
      return;
    }

    this.parsingResume.set(true);
    this.resumeParseError.set(null);
    this.resumeParseMessage.set(null);

    this.recruiter.parseResume(file).subscribe({
      next: (parsed) => {
        this.parsedResume.set(parsed);
        this.parsingResume.set(false);
        this.resumeParseMessage.set('Resume parsed. Review the extracted details before creating the profile.');
      },
      error: (error: unknown) => {
        this.parsingResume.set(false);
        this.resumeParseError.set(this.toErrorMessage(error));
      },
    });
  }

  protected loadTemplatePreset(slug: string): void {
    this.templatePresetSlug = slug;
    const preset = this.emailTemplateCatalog()?.presets.find((item) => item.slug === slug);
    if (!preset) {
      return;
    }

    this.selectedEmailTemplateId.set(null);
    this.templateSlug = preset.slug;
    this.templateName = preset.name;
    this.templateSubject = preset.subject;
    this.templateBodyMarkdown = preset.bodyMarkdown;
    this.emailTemplatePreview.set(null);
    this.emailTemplateMessage.set(`Loaded starter template: ${preset.name}.`);
  }

  protected editEmailTemplate(templateId: string): void {
    const template = this.emailTemplates().find((item) => item.id === templateId);
    if (!template) {
      return;
    }

    this.selectedEmailTemplateId.set(template.id);
    this.templateSlug = template.slug;
    this.templateName = template.name;
    this.templateSubject = template.subject;
    this.templateBodyMarkdown = template.bodyMarkdown;
    this.emailTemplateMessage.set(`Editing template: ${template.name}.`);
  }

  protected saveEmailTemplate(): void {
    this.savingEmailTemplate.set(true);
    this.emailTemplateError.set(null);
    this.emailTemplateMessage.set(null);

    const selectedId = this.selectedEmailTemplateId();
    const request = {
      slug: this.templateSlug,
      name: this.templateName,
      subject: this.templateSubject,
      bodyMarkdown: this.templateBodyMarkdown,
    };

    const operation = selectedId
      ? this.recruiter.updateEmailTemplate(selectedId, request)
      : this.recruiter.createEmailTemplate(request);

    operation.subscribe({
      next: (template) => {
        this.savingEmailTemplate.set(false);
        this.selectedEmailTemplateId.set(template.id);
        this.emailTemplates.update((items) => {
          const existing = items.findIndex((item) => item.id === template.id);
          if (existing >= 0) {
            const next = [...items];
            next[existing] = template;
            return next.sort((a, b) => a.name.localeCompare(b.name));
          }

          return [template, ...items].sort((a, b) => a.name.localeCompare(b.name));
        });
        this.emailTemplateMessage.set(selectedId ? 'Template updated.' : 'Template created.');
      },
      error: (error: unknown) => {
        this.savingEmailTemplate.set(false);
        this.emailTemplateError.set(this.toErrorMessage(error));
      },
    });
  }

  protected deleteCurrentEmailTemplate(): void {
    const template = this.currentTemplate();
    if (!template) {
      return;
    }

    this.emailTemplateError.set(null);
    this.emailTemplateMessage.set(null);

    this.recruiter.deleteEmailTemplate(template.id).subscribe({
      next: () => {
        this.emailTemplates.update((items) => items.filter((item) => item.id !== template.id));
        this.selectedEmailTemplateId.set(null);
        this.emailTemplatePreview.set(null);
        this.loadTemplatePreset(this.templatePresetSlug);
        this.emailTemplateMessage.set(`Deleted template: ${template.name}.`);
      },
      error: (error: unknown) => {
        this.emailTemplateError.set(this.toErrorMessage(error));
      },
    });
  }

  protected previewCurrentEmailTemplate(): void {
    this.previewingEmailTemplate.set(true);
    this.emailTemplateError.set(null);
    this.emailTemplateMessage.set(null);

    this.recruiter.previewEmailTemplate({
      subject: this.templateSubject,
      bodyMarkdown: this.templateBodyMarkdown,
      mergeFields: this.previewMergeFields(),
    }).subscribe({
      next: (preview) => {
        this.previewingEmailTemplate.set(false);
        this.emailTemplatePreview.set(preview);
        this.emailTemplateMessage.set('Preview refreshed with current merge fields.');
      },
      error: (error: unknown) => {
        this.previewingEmailTemplate.set(false);
        this.emailTemplateError.set(this.toErrorMessage(error));
      },
    });
  }

  protected downloadQuickBooksCsv(): void {
    this.downloadingQuickBooks.set(true);
    this.handoffError.set(null);

    this.recruiter.quickBooksCsv().subscribe({
      next: (response) => {
        if (!response.body) {
          this.downloadingQuickBooks.set(false);
          this.handoffError.set('QuickBooks CSV export returned no file content.');
          return;
        }

        const url = URL.createObjectURL(response.body);
        const anchor = document.createElement('a');
        const disposition = response.headers.get('content-disposition');
        const fileName = disposition?.match(/filename="?([^";]+)"?/)?.[1] ?? 'qbo-timesheets.csv';

        anchor.href = url;
        anchor.download = fileName;
        anchor.click();
        URL.revokeObjectURL(url);
        this.downloadingQuickBooks.set(false);
      },
      error: (error: unknown) => {
        this.downloadingQuickBooks.set(false);
        this.handoffError.set(this.toErrorMessage(error));
      },
    });
  }

  private fetchJobs(): void {
    this.loadingJobs.set(true);
    this.recruiter.jobs().subscribe({
      next: (jobs) => {
        this.jobs.set(jobs);
        this.loadingJobs.set(false);
      },
      error: (error: unknown) => {
        this.loadingJobs.set(false);
        this.jobsError.set(this.toErrorMessage(error));
      },
    });
  }

  private fetchApplications(): void {
    this.loadingApplications.set(true);
    this.recruiter.applications().subscribe({
      next: (board: RecruiterApplicationsBoard) => {
        this.applications.set(board.items);
        this.loadingApplications.set(false);
      },
      error: (error: unknown) => {
        this.loadingApplications.set(false);
        this.applicationsError.set(this.toErrorMessage(error));
      },
    });
  }

  private moveMany(applicationIds: string[], status: string): void {
    const selectedIds = [...new Set(applicationIds)];
    if (selectedIds.length === 0) {
      return;
    }

    const movableIds = selectedIds.filter((id) => {
      const application = this.applications().find((item) => item.id === id);
      return application && application.status !== status;
    });

    if (movableIds.length === 0) {
      return;
    }

    this.movingApplications.set(true);
    this.applicationsError.set(null);

    this.recruiter.bulkMoveApplications({
      applicationIds: movableIds,
      status,
    }).subscribe({
      next: (response: RecruiterBulkStatusMoveResponse) => {
        const updatedMap = new Map(response.items.map((item) => [item.id, item]));
        this.applications.update((items) =>
          items.map((item) => updatedMap.get(item.id) ?? item),
        );
        this.movingApplications.set(false);
        this.selectedApplicationIds.set([]);
      },
      error: (error: unknown) => {
        this.movingApplications.set(false);
        this.applicationsError.set(this.toErrorMessage(error));
      },
    });
  }

  private fetchInvoiceReady(): void {
    this.loadingInvoiceReady.set(true);
    this.recruiter.invoiceReady().subscribe({
      next: (response) => {
        this.invoiceReady.set(response.items);
        this.loadingInvoiceReady.set(false);
      },
      error: (error: unknown) => {
        this.loadingInvoiceReady.set(false);
        this.invoiceReadyError.set(this.toErrorMessage(error));
      },
    });
  }

  private fetchEmailTemplateCatalog(): void {
    this.recruiter.emailTemplateCatalog().subscribe({
      next: (catalog) => {
        this.emailTemplateCatalog.set(catalog);
        const preset = catalog.presets.find((item) => item.slug === this.templatePresetSlug) ?? catalog.presets[0];
        if (preset && !this.selectedEmailTemplateId()) {
          this.templatePresetSlug = preset.slug;
          this.templateSlug = preset.slug;
          this.templateName = preset.name;
          this.templateSubject = preset.subject;
          this.templateBodyMarkdown = preset.bodyMarkdown;
        }
      },
      error: (error: unknown) => {
        this.emailTemplateError.set(this.toErrorMessage(error));
      },
    });
  }

  private fetchEmailTemplates(): void {
    this.loadingEmailTemplates.set(true);
    this.emailTemplateError.set(null);

    this.recruiter.emailTemplates().subscribe({
      next: (response) => {
        this.emailTemplates.set(response.items);
        this.loadingEmailTemplates.set(false);
      },
      error: (error: unknown) => {
        this.loadingEmailTemplates.set(false);
        this.emailTemplateError.set(this.toErrorMessage(error));
      },
    });
  }

  private previewMergeFields(): Record<string, string | null> {
    return {
      candidate_name: this.previewCandidateName,
      candidate_email: this.previewCandidateEmail,
      company: this.previewCompany,
      job_title: this.previewJobTitle,
      interview_date: this.previewInterviewDate,
      recruiter_name: this.previewRecruiterName,
      portal_link: this.previewPortalLink,
      offer_amount: this.previewOfferAmount,
      onboarding_due_date: this.previewOnboardingDueDate,
    };
  }

  private fetchCandidateActivity(): void {
    this.loadingActivity.set(true);
    this.activityError.set(null);

    this.recruiter.candidateActivity().subscribe({
      next: (response) => {
        this.candidateActivity.set(response.items);
        this.loadingActivity.set(false);
      },
      error: (error: unknown) => {
        this.candidateActivity.set([]);
        this.loadingActivity.set(false);
        this.activityError.set(this.toErrorMessage(error));
      },
    });
  }

  protected fetchNotifications(): void {
    this.loadingNotifications.set(true);
    this.notificationsError.set(null);

    this.recruiter.notifications().subscribe({
      next: (response) => {
        this.notifications.set(response.items);
        this.loadingNotifications.set(false);
      },
      error: (error: unknown) => {
        this.notifications.set([]);
        this.loadingNotifications.set(false);
        this.notificationsError.set(this.toErrorMessage(error));
      },
    });
  }

  protected fetchInvoiceHandoff(): void {
    this.loadingHandoff.set(true);
    this.handoffError.set(null);
    this.recruiter.invoiceHandoff().subscribe({
      next: (response) => {
        this.invoiceHandoff.set(response);
        this.loadingHandoff.set(false);
      },
      error: (error: unknown) => {
        this.loadingHandoff.set(false);
        this.handoffError.set(this.toErrorMessage(error));
      },
    });
  }

  private toErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      return error.error?.detail ?? error.error?.title ?? error.message;
    }

    return error instanceof Error ? error.message : 'Unexpected recruiter portal error.';
  }
}
