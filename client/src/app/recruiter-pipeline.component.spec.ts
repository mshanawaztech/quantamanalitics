import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { AuthService as Auth0Service } from '@auth0/auth0-angular';
import { EMPTY, of } from 'rxjs';

import { RecruiterPipelineComponent } from './recruiter-pipeline.component';
import { MeService } from './core/auth/me.service';
import { RecruiterApplication } from './core/recruiter/recruiter-portal.service';
import { environment } from '../environments/environment';

/**
 * The TestBed runner here is vitest (see tsconfig.spec.json) — the global
 * describe/it/expect/beforeEach API is structurally identical to Jasmine,
 * which is what the PR brief asked for. We use HttpTestingController to
 * assert the optimistic move and rollback on error.
 */

const APPLICATIONS_URL = `${environment.apiBase}/api/v1/recruiter/applications`;

function statusUrl(id: string) {
  return `${APPLICATIONS_URL}/${id}/status`;
}

function makeApplication(
  partial: Partial<RecruiterApplication> & Pick<RecruiterApplication, 'id' | 'status'>,
): RecruiterApplication {
  return {
    jobId: 'job-1',
    jobTitle: 'Senior Engineer',
    candidateName: 'Pat Candidate',
    candidateEmail: 'pat@example.com',
    note: null,
    appliedAtUtc: '2026-04-30T00:00:00Z',
    updatedAtUtc: '2026-05-01T00:00:00Z',
    daysInStage: 1,
    isStuck: false,
    tags: [],
    ...partial,
  };
}

describe('RecruiterPipelineComponent', () => {
  let httpMock: HttpTestingController;
  let meSignal: ReturnType<typeof signal<{ roles: string[] } | null>>;

  beforeEach(() => {
    meSignal = signal<{ roles: string[] } | null>({ roles: ['Recruiter'] });

    const auth0Stub: Partial<Auth0Service> = {
      isAuthenticated$: of(true),
      isLoading$: of(false),
      user$: of(null),
      loginWithRedirect: () => EMPTY,
      logout: () => EMPTY,
    };

    TestBed.configureTestingModule({
      imports: [RecruiterPipelineComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Auth0Service, useValue: auth0Stub },
        // Override MeService so the component thinks the user has the
        // recruiter role without going through the live /me round-trip.
        {
          provide: MeService,
          useValue: {
            data: () => meSignal(),
            loading: () => false,
            error: () => null,
            hasResult: () => true,
          },
        },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('groups loaded applications by status into the right columns', () => {
    const fixture = TestBed.createComponent(RecruiterPipelineComponent);
    fixture.detectChanges();

    const req = httpMock.expectOne(APPLICATIONS_URL);
    req.flush({
      items: [
        makeApplication({ id: 'a1', status: 'Applied' }),
        makeApplication({ id: 'a2', status: 'Interviewing', candidateName: 'Sam Two' }),
        makeApplication({ id: 'a3', status: 'Hired', candidateName: 'Kai Three' }),
      ],
      availableTags: [],
      availableLocations: [],
      savedFilters: [],
    });
    fixture.detectChanges();

    const dom = fixture.nativeElement as HTMLElement;
    const appliedCards = dom.querySelectorAll('[data-status="Applied"] .card');
    const interviewingCards = dom.querySelectorAll('[data-status="Interviewing"] .card');
    const hiredCards = dom.querySelectorAll('[data-status="Hired"] .card');
    const offerCards = dom.querySelectorAll('[data-status="OfferSent"] .card');

    expect(appliedCards.length).toBe(1);
    expect(interviewingCards.length).toBe(1);
    expect(hiredCards.length).toBe(1);
    expect(offerCards.length).toBe(0);
    // Empty stages render the qa-empty-state component.
    expect(dom.querySelector('[data-status="OfferSent"] qa-empty-state')).toBeTruthy();
  });

  it('optimistically moves a card on drop and persists the API response', () => {
    const fixture = TestBed.createComponent(RecruiterPipelineComponent);
    fixture.detectChanges();

    httpMock
      .expectOne(APPLICATIONS_URL)
      .flush({
        items: [makeApplication({ id: 'a1', status: 'Applied' })],
        availableTags: [],
        availableLocations: [],
        savedFilters: [],
      });
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      onDrop(event: DragEvent, status: string): void;
      applications: () => RecruiterApplication[];
    };

    // Build a minimal DragEvent stub — DragEvent isn't constructable in
    // jsdom, so we cast a plain object that exposes the surface the
    // component touches.
    const fakeEvent = {
      preventDefault: () => undefined,
      dataTransfer: {
        getData: () => 'a1',
      },
    } as unknown as DragEvent;

    component.onDrop(fakeEvent, 'Interviewing');

    // Optimistic update applied immediately.
    expect(component.applications()[0].status).toBe('Interviewing');

    const move = httpMock.expectOne(statusUrl('a1'));
    expect(move.request.method).toBe('POST');
    expect(move.request.body).toEqual({ status: 'Interviewing' });

    move.flush({
      ...makeApplication({ id: 'a1', status: 'Interviewing' }),
      updatedAtUtc: '2026-05-08T12:00:00Z',
    });

    httpMock.expectOne(APPLICATIONS_URL).flush({
      items: [
        {
          ...makeApplication({ id: 'a1', status: 'Interviewing' }),
          updatedAtUtc: '2026-05-08T12:00:00Z',
        },
      ],
      availableTags: [],
      availableLocations: [],
      savedFilters: [],
    });

    fixture.detectChanges();
    expect(component.applications()[0].status).toBe('Interviewing');
    expect(component.applications()[0].updatedAtUtc).toBe('2026-05-08T12:00:00Z');
  });

  it('rolls back to the previous status when the API rejects the move', () => {
    const fixture = TestBed.createComponent(RecruiterPipelineComponent);
    fixture.detectChanges();

    httpMock
      .expectOne(APPLICATIONS_URL)
      .flush({
        items: [makeApplication({ id: 'a1', status: 'Applied' })],
        availableTags: [],
        availableLocations: [],
        savedFilters: [],
      });
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      onDrop(event: DragEvent, status: string): void;
      applications: () => RecruiterApplication[];
      errorMessage: () => string | null;
    };

    const fakeEvent = {
      preventDefault: () => undefined,
      dataTransfer: { getData: () => 'a1' },
    } as unknown as DragEvent;

    component.onDrop(fakeEvent, 'Hired');
    expect(component.applications()[0].status).toBe('Hired');

    const move = httpMock.expectOne(statusUrl('a1'));
    move.flush(
      { detail: 'Status transition not permitted' },
      { status: 422, statusText: 'Unprocessable Entity' },
    );

    fixture.detectChanges();
    expect(component.applications()[0].status).toBe('Applied');
    expect(component.errorMessage()).toBe('Status transition not permitted');
  });

  it('exposes a keyboard "Move…" menu that fires the same status PATCH', () => {
    const fixture = TestBed.createComponent(RecruiterPipelineComponent);
    fixture.detectChanges();

    httpMock
      .expectOne(APPLICATIONS_URL)
      .flush({
        items: [makeApplication({ id: 'a1', status: 'Applied' })],
        availableTags: [],
        availableLocations: [],
        savedFilters: [],
      });
    fixture.detectChanges();

    const dom = fixture.nativeElement as HTMLElement;
    const moveButton = dom.querySelector<HTMLButtonElement>('.card__move');
    expect(moveButton).toBeTruthy();
    moveButton!.click();
    fixture.detectChanges();

    const menu = dom.querySelector<HTMLUListElement>('.card__menu');
    expect(menu).toBeTruthy();
    expect(menu!.getAttribute('role')).toBe('menu');

    const menuItems = Array.from(menu!.querySelectorAll<HTMLButtonElement>('button'));
    // Menu lists every column except the current one (4 of 5).
    expect(menuItems.length).toBe(4);

    const offerSent = menuItems.find((b) => b.textContent?.trim() === 'Offer sent');
    expect(offerSent).toBeTruthy();
    offerSent!.click();
    fixture.detectChanges();

    const move = httpMock.expectOne(statusUrl('a1'));
    expect(move.request.method).toBe('POST');
    expect(move.request.body).toEqual({ status: 'OfferSent' });
    move.flush(makeApplication({ id: 'a1', status: 'OfferSent' }));

    httpMock.expectOne(APPLICATIONS_URL).flush({
      items: [makeApplication({ id: 'a1', status: 'OfferSent' })],
      availableTags: [],
      availableLocations: [],
      savedFilters: [],
    });
  });

  it('sends recruiter filter params when applying search controls', () => {
    const fixture = TestBed.createComponent(RecruiterPipelineComponent);
    fixture.detectChanges();

    httpMock.expectOne(APPLICATIONS_URL).flush({
      items: [],
      availableTags: ['urgent'],
      availableLocations: ['Dallas, TX · Hybrid'],
      savedFilters: [],
    });
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      searchTerm: ReturnType<typeof signal<string>>;
      statusFilter: ReturnType<typeof signal<string>>;
      tagFilter: ReturnType<typeof signal<string>>;
      locationFilter: ReturnType<typeof signal<string>>;
      stuckOnly: ReturnType<typeof signal<boolean>>;
      applyFilters(): void;
    };

    component.searchTerm.set('cloud');
    component.statusFilter.set('Interviewing');
    component.tagFilter.set('urgent');
    component.locationFilter.set('Dallas, TX · Hybrid');
    component.stuckOnly.set(true);
    component.applyFilters();

    const req = httpMock.expectOne((request) =>
      request.url === APPLICATIONS_URL &&
      request.params.get('search') === 'cloud' &&
      request.params.get('status') === 'Interviewing' &&
      request.params.get('tag') === 'urgent' &&
      request.params.get('location') === 'Dallas, TX · Hybrid' &&
      request.params.get('stuckOnly') === 'true');

    req.flush({
      items: [],
      availableTags: ['urgent'],
      availableLocations: ['Dallas, TX · Hybrid'],
      savedFilters: [],
    });
  });

  it('posts bulk tag updates for the selected cards', () => {
    const fixture = TestBed.createComponent(RecruiterPipelineComponent);
    fixture.detectChanges();

    httpMock.expectOne(APPLICATIONS_URL).flush({
      items: [
        makeApplication({ id: 'a1', status: 'Applied' }),
        makeApplication({ id: 'a2', status: 'Applied' }),
      ],
      availableTags: [],
      availableLocations: [],
      savedFilters: [],
    });
    fixture.detectChanges();

    const component = fixture.componentInstance as unknown as {
      toggleSelection(id: string, selected: boolean): void;
      bulkTagText: ReturnType<typeof signal<string>>;
      bulkTagSelected(operation: 'Add' | 'Remove'): void;
    };

    component.toggleSelection('a1', true);
    component.toggleSelection('a2', true);
    component.bulkTagText.set('urgent, referred');
    component.bulkTagSelected('Add');

    const req = httpMock.expectOne(`${APPLICATIONS_URL}/bulk-tags`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      applicationIds: ['a1', 'a2'],
      tags: ['urgent', 'referred'],
      operation: 'Add',
    });

    req.flush({
      requestedCount: 2,
      operation: 'Add',
      tags: ['urgent', 'referred'],
      items: [
        makeApplication({ id: 'a1', status: 'Applied', tags: ['urgent', 'referred'] }),
        makeApplication({ id: 'a2', status: 'Applied', tags: ['urgent', 'referred'] }),
      ],
    });

    httpMock.expectOne(APPLICATIONS_URL).flush({
      items: [
        makeApplication({ id: 'a1', status: 'Applied', tags: ['urgent', 'referred'] }),
        makeApplication({ id: 'a2', status: 'Applied', tags: ['urgent', 'referred'] }),
      ],
      availableTags: ['urgent', 'referred'],
      availableLocations: [],
      savedFilters: [],
    });
  });
});
