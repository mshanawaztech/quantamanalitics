import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

let nextId = 0;

/**
 * Accessible text input primitive. Renders a real <label> tied to the
 * input via id/for. Supports `type` for text / email / number / password,
 * a hint line, and an error line.
 *
 * For multi-line use <qa-textarea>. For dropdowns use <qa-select>.
 */
@Component({
  selector: 'qa-input',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="qa-field">
      <label [attr.for]="inputId" class="qa-field__label">
        {{ label }}
        @if (required) { <span class="qa-field__required" aria-hidden="true">*</span> }
      </label>

      <input
        [id]="inputId"
        [type]="type"
        [name]="name"
        [placeholder]="placeholder"
        [disabled]="disabled"
        [required]="required"
        [attr.aria-invalid]="error ? 'true' : null"
        [attr.aria-describedby]="describedBy()"
        [(ngModel)]="value"
        (ngModelChange)="valueChange.emit($event)"
        class="qa-field__input"
      />

      @if (hint && !error) {
        <p [id]="hintId" class="qa-field__hint">{{ hint }}</p>
      }
      @if (error) {
        <p [id]="errorId" class="qa-field__error" role="alert">{{ error }}</p>
      }
    </div>
  `,
  styles: `
    :host { display: block; }

    .qa-field {
      display: flex;
      flex-direction: column;
      gap: var(--space-2);
    }

    .qa-field__label {
      color: var(--color-ink-strong);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-semi);
    }

    .qa-field__required {
      color: var(--color-danger);
      margin-left: 2px;
    }

    .qa-field__input {
      padding: var(--space-2) var(--space-3);
      min-height: 2.5rem;
      background: var(--color-surface);
      border: 1px solid var(--color-border-strong);
      border-radius: var(--radius-md);
      color: var(--color-ink);
      font: inherit;
      font-size: var(--font-size-md);
      transition: border-color 160ms ease, box-shadow 160ms ease;
    }

    .qa-field__input::placeholder { color: var(--color-ink-muted); }

    .qa-field__input:hover:not(:disabled) {
      border-color: var(--color-primary);
    }

    .qa-field__input:focus {
      outline: none;
      border-color: var(--color-primary);
      box-shadow: 0 0 0 3px var(--color-primary-ring);
    }

    .qa-field__input[aria-invalid='true'] {
      border-color: var(--color-danger);
    }
    .qa-field__input[aria-invalid='true']:focus {
      box-shadow: 0 0 0 3px rgb(176 42 55 / 0.32);
    }

    .qa-field__input:disabled {
      background: var(--color-surface-alt);
      color: var(--color-ink-muted);
      cursor: not-allowed;
    }

    .qa-field__hint {
      margin: 0;
      color: var(--color-ink-muted);
      font-size: var(--font-size-xs);
    }
    .qa-field__error {
      margin: 0;
      color: var(--color-danger);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-semi);
    }
  `,
})
export class QaInputComponent {
  @Input() label = '';
  @Input() name?: string;
  @Input() type: 'text' | 'email' | 'password' | 'number' | 'tel' | 'url' = 'text';
  @Input() placeholder = '';
  @Input() value: string | number = '';
  @Input() hint?: string;
  @Input() error?: string;
  @Input() disabled = false;
  @Input() required = false;

  @Output() valueChange = new EventEmitter<string | number>();

  readonly inputId = `qa-input-${++nextId}`;
  readonly hintId = `${this.inputId}-hint`;
  readonly errorId = `${this.inputId}-err`;

  describedBy(): string | null {
    if (this.error) return this.errorId;
    if (this.hint) return this.hintId;
    return null;
  }
}
