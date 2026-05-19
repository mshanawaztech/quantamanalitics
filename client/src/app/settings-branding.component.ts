import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  TenantBrandingResponse,
  TenantBrandingService,
  UpdateTenantBrandingRequest,
} from './core/tenant/branding.service';
import {
  QaAlertComponent,
  QaButtonComponent,
  QaInputComponent,
} from './core/ui';

/**
 * Tenant branding settings — letterhead identity, postal address, bank
 * remit-to, default invoice values, banner colors. Drives every invoice
 * PDF the tenant generates from /contractor/invoices.
 */
@Component({
  selector: 'app-settings-branding',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, QaAlertComponent, QaButtonComponent, QaInputComponent],
  template: `
    <main class="page">
      <header class="page__head">
        <p class="eyebrow">Settings</p>
        <h1>Invoice branding</h1>
        <p class="lede">
          Identity, payment details, and defaults applied to every invoice
          PDF you generate. Changes take effect on the next download.
        </p>
      </header>

      @if (loadError()) {
        <qa-alert tone="danger" role="alert">{{ loadError() }}</qa-alert>
      }
      @if (saveSuccess()) {
        <qa-alert tone="success" role="status">Branding saved.</qa-alert>
      }
      @if (formError()) {
        <qa-alert tone="danger" role="alert">{{ formError() }}</qa-alert>
      }

      @if (loading()) {
        <p class="hint">Loading…</p>
      } @else {
        <form class="card" (submit)="onSubmit($event)">
          <fieldset class="section">
            <legend>Identity</legend>
            <div class="grid grid--2">
              <qa-input
                label="Display name"
                [(ngModel)]="form.displayName"
                name="displayName"
                hint="Top of the invoice banner (e.g., 'Mohammed Khan')."
              ></qa-input>
              <qa-input
                label="Legal entity"
                [(ngModel)]="form.legalName"
                name="legalName"
                hint="Used for bank-account ownership (e.g., 'Quantamanalytics LLC')."
              ></qa-input>
              <qa-input
                label="Contact email"
                type="email"
                [(ngModel)]="form.contactEmail"
                name="contactEmail"
              ></qa-input>
              <qa-input
                label="Contact phone"
                [(ngModel)]="form.contactPhone"
                name="contactPhone"
              ></qa-input>
            </div>
          </fieldset>

          <fieldset class="section">
            <legend>Postal address</legend>
            <div class="grid grid--2">
              <qa-input label="Address line 1" [(ngModel)]="form.addressLine1" name="address1"></qa-input>
              <qa-input label="Address line 2" [(ngModel)]="form.addressLine2" name="address2"></qa-input>
              <qa-input label="City" [(ngModel)]="form.city" name="city"></qa-input>
              <qa-input label="State / Region" [(ngModel)]="form.stateRegion" name="state"></qa-input>
              <qa-input label="Postal code" [(ngModel)]="form.postalCode" name="postal"></qa-input>
              <qa-input label="Country" [(ngModel)]="form.country" name="country"></qa-input>
            </div>
          </fieldset>

          <fieldset class="section">
            <legend>Bank / ACH remit-to</legend>
            <p class="hint">Shown on every invoice so clients know exactly where to send funds.</p>
            <div class="grid grid--2">
              <qa-input label="Bank name" [(ngModel)]="form.bankName" name="bankName"></qa-input>
              <qa-input label="Account number" [(ngModel)]="form.bankAccountNumber" name="bankAccount"></qa-input>
              <qa-input label="Routing number" [(ngModel)]="form.bankRoutingNumber" name="bankRouting"></qa-input>
            </div>
          </fieldset>

          <fieldset class="section">
            <legend>Defaults</legend>
            <p class="hint">Used to pre-populate new invoices.</p>
            <div class="grid grid--3">
              <qa-input
                label="Default hourly rate"
                type="number"
                [(ngModel)]="form.defaultHourlyRate"
                name="defaultRate"
              ></qa-input>
              <qa-input
                label="Default currency"
                [(ngModel)]="form.defaultCurrency"
                name="defaultCurrency"
                hint="3-letter ISO code (USD, EUR, GBP…)."
              ></qa-input>
              <qa-input
                label="Payment terms (days)"
                type="number"
                [(ngModel)]="form.defaultPaymentTermsDays"
                name="defaultTerms"
                hint="Days from issue date the invoice is due."
              ></qa-input>
            </div>
          </fieldset>

          <fieldset class="section">
            <legend>Brand colors</legend>
            <div class="grid grid--2">
              <div class="field">
                <label class="field__label" for="primary">Banner color</label>
                <input
                  id="primary"
                  type="color"
                  class="field__color"
                  [(ngModel)]="form.primaryColorHex"
                  name="primaryColor"
                />
                <p class="field__hint">Top banner background on the invoice PDF.</p>
              </div>
              <div class="field">
                <label class="field__label" for="accent">Accent color</label>
                <input
                  id="accent"
                  type="color"
                  class="field__color"
                  [(ngModel)]="form.accentColorHex"
                  name="accentColor"
                />
                <p class="field__hint">Line items table header + banner stripe.</p>
              </div>
            </div>
          </fieldset>

          <div class="form__actions">
            <qa-button
              variant="primary"
              type="submit"
              [disabled]="saving()"
              [loading]="saving()"
            >{{ saving() ? 'Saving…' : 'Save branding' }}</qa-button>
          </div>
        </form>
      }
    </main>
  `,
  styles: `
    :host { display: block; }
    .page { width: min(900px, 100%); margin: 0 auto; padding: 1.5rem 1.5rem 4rem; }
    .eyebrow { color: var(--color-primary, #1a3a8f); font-size: 12px; font-weight: 700; letter-spacing: 0.12em; text-transform: uppercase; margin: 0 0 0.5rem; }
    .page__head { margin-bottom: 1.5rem; }
    h1 { margin: 0 0 0.5rem; font-size: 1.625rem; }
    .lede { color: var(--color-fg-muted, #5d6577); margin: 0; max-width: 60ch; }

    .card {
      background: var(--color-surface, #fff);
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 14px;
      padding: 1.5rem;
      display: grid;
      gap: 1.25rem;
    }

    .section { border: 0; padding: 0; margin: 0; }
    .section legend { font-size: 1rem; font-weight: 700; margin-bottom: 0.625rem; padding: 0; }
    .section .hint { color: var(--color-fg-muted, #5d6577); font-size: 0.85rem; margin: 0 0 0.625rem; }

    .grid { display: grid; gap: 0.75rem; }
    .grid--2 { grid-template-columns: 1fr 1fr; }
    .grid--3 { grid-template-columns: 1fr 1fr 1fr; }
    @media (max-width: 720px) { .grid--3, .grid--2 { grid-template-columns: 1fr; } }

    .field { display: flex; flex-direction: column; gap: 0.375rem; }
    .field__label { font-size: 0.85rem; font-weight: 600; }
    .field__color {
      width: 6rem; height: 2.25rem;
      border: 1px solid var(--color-border, #d8dde7);
      border-radius: 8px;
      padding: 0.25rem;
      background: var(--color-surface, #fff);
      cursor: pointer;
    }
    .field__hint { margin: 0; color: var(--color-fg-muted, #5d6577); font-size: 0.78rem; }

    .form__actions { display: flex; justify-content: flex-end; }
    .hint { color: var(--color-fg-muted, #5d6577); }
  `,
})
export class SettingsBrandingComponent {
  private svc = inject(TenantBrandingService);

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly formError = signal<string | null>(null);
  protected readonly saveSuccess = signal(false);

  // Backing object for the form. Two-way bound; submitted as-is.
  protected form: UpdateTenantBrandingRequest = emptyForm();

  constructor() {
    this.svc.get().subscribe({
      next: (b) => {
        this.form = applyToForm(b);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.loadError.set(toMessage(e));
        this.loading.set(false);
      },
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    this.saving.set(true);
    this.formError.set(null);
    this.saveSuccess.set(false);

    // Coerce empty number strings back to null so the server doesn't see "0"
    // and treat it as a real default.
    const payload: UpdateTenantBrandingRequest = {
      ...this.form,
      defaultHourlyRate: coerceNumber(this.form.defaultHourlyRate),
      defaultPaymentTermsDays: coerceNumber(this.form.defaultPaymentTermsDays),
    };

    this.svc.update(payload).subscribe({
      next: (b) => {
        this.form = applyToForm(b);
        this.saving.set(false);
        this.saveSuccess.set(true);
        setTimeout(() => this.saveSuccess.set(false), 3500);
      },
      error: (e: unknown) => {
        this.saving.set(false);
        this.formError.set(toMessage(e));
      },
    });
  }
}

function emptyForm(): UpdateTenantBrandingRequest {
  return {
    displayName: null,
    legalName: null,
    contactEmail: null,
    contactPhone: null,
    addressLine1: null,
    addressLine2: null,
    city: null,
    stateRegion: null,
    postalCode: null,
    country: null,
    bankName: null,
    bankAccountNumber: null,
    bankRoutingNumber: null,
    defaultHourlyRate: null,
    defaultCurrency: null,
    defaultPaymentTermsDays: null,
    primaryColorHex: '#1a2d5a',
    accentColorHex: '#e6c9a8',
  };
}

function applyToForm(b: TenantBrandingResponse): UpdateTenantBrandingRequest {
  return {
    displayName: b.displayName,
    legalName: b.legalName,
    contactEmail: b.contactEmail,
    contactPhone: b.contactPhone,
    addressLine1: b.addressLine1,
    addressLine2: b.addressLine2,
    city: b.city,
    stateRegion: b.stateRegion,
    postalCode: b.postalCode,
    country: b.country,
    bankName: b.bankName,
    bankAccountNumber: b.bankAccountNumber,
    bankRoutingNumber: b.bankRoutingNumber,
    defaultHourlyRate: b.defaultHourlyRate,
    defaultCurrency: b.defaultCurrency,
    defaultPaymentTermsDays: b.defaultPaymentTermsDays,
    primaryColorHex: b.primaryColorHex ?? '#1a2d5a',
    accentColorHex: b.accentColorHex ?? '#e6c9a8',
  };
}

function coerceNumber(value: number | string | null): number | null {
  if (value === null || value === '' || value === undefined) return null;
  const num = Number(value);
  return Number.isFinite(num) ? num : null;
}

function toMessage(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error as { detail?: string; title?: string } | null;
    return body?.detail ?? body?.title ?? error.message;
  }
  return error instanceof Error ? error.message : 'Unexpected error.';
}
