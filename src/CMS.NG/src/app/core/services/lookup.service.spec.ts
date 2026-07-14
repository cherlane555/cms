import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { environment } from '@env';
import { LookupService } from './lookup.service';
import { AppUserLookup } from '@core/models/app-role.model';

describe('LookupService', () => {
  let service: LookupService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [LookupService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(LookupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAppUsers GETs the app-users lookup', () => {
    const users: AppUserLookup[] = [
      { userId: 'helen', userName: 'helen', label: 'helen (helen)' },
    ];
    let result: AppUserLookup[] | undefined;
    service.getAppUsers().subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/lookups/app-users`);
    expect(req.request.method).toBe('GET');
    req.flush(users);

    expect(result?.[0].label).toBe('helen (helen)');
  });
});
