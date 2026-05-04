import { Component, inject } from '@angular/core';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login',
  template: `
    @if (!auth.isConfigured()) {
      <main class="login-shell">
        <section class="login-card">
          <h1>Auth0 not configured</h1>
          <p>
            Set the Auth0 domain, client ID, and audience before using the login
            route.
          </p>
        </section>
      </main>
    } @else {
      <main class="login-shell" aria-live="polite">
        <section class="login-card">
          <h1>Redirecting to sign in…</h1>
          <p>If nothing happens, refresh and try again from the home page.</p>
        </section>
      </main>
    }
  `,
  styles: `
    .login-shell {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 2rem;
      background: #f7f7fb;
    }

    .login-card {
      width: min(100%, 32rem);
      padding: 2rem;
      border: 1px solid #d8d8e2;
      border-radius: 1rem;
      background: #fff;
      text-align: center;
      box-shadow: 0 0.75rem 2rem rgb(17 24 39 / 0.08);
    }

    h1 {
      margin: 0 0 0.75rem;
      font-size: 1.75rem;
    }

    p {
      margin: 0;
      color: #4b5563;
      line-height: 1.5;
    }
  `,
})
export class LoginComponent {
  protected auth = inject(AuthService);

  constructor() {
    if (this.auth.isConfigured()) {
      this.auth.loginWithRedirect();
    }
  }
}
