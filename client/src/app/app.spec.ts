import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService as Auth0Service } from '@auth0/auth0-angular';
import { EMPTY, of } from 'rxjs';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    // Stub Auth0 SDK so the component can construct without provideAuth0().
    // Real Auth0 wiring lives in app.config.ts and is exercised by the
    // built bundle, not unit tests. loginWithRedirect / logout return
    // Observable<void>, not Promise<void> — EMPTY (Observable<never>) is
    // structurally assignable.
    const auth0Stub: Partial<Auth0Service> = {
      isAuthenticated$: of(false),
      isLoading$: of(false),
      user$: of(null),
      loginWithRedirect: () => EMPTY,
      logout: () => EMPTY,
    };

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Auth0Service, useValue: auth0Stub },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the project title', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Quantam Analytics');
  });
});
