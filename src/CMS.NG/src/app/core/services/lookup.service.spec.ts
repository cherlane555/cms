import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { environment } from '@env';
import { LookupService } from './lookup.service';
import { AppUserLookup } from '@core/models/app-role.model';
import { PublishStatusLookup } from '@core/models/publish-status.model';

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

  it('getPublishStatuses GETs the publish-statuses lookup', () => {
    const statuses: PublishStatusLookup[] = [{ pkid: 1, description: '草稿' }];
    let result: PublishStatusLookup[] | undefined;
    service.getPublishStatuses().subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/lookups/publish-statuses`);
    expect(req.request.method).toBe('GET');
    req.flush(statuses);

    expect(result?.[0].description).toBe('草稿');
  });
});
