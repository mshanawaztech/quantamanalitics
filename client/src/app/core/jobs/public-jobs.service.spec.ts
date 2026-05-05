import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';

import { PublicJobsService } from './public-jobs.service';

describe('PublicJobsService', () => {
  let service: PublicJobsService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(PublicJobsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loads the public jobs list', () => {
    let response: unknown;
    service.list().subscribe((value) => (response = value));

    const req = httpMock.expectOne((request) => request.url.endsWith('/api/v1/jobs'));
    expect(req.request.method).toBe('GET');
    req.flush([]);

    expect(response).toEqual([]);
  });
});
