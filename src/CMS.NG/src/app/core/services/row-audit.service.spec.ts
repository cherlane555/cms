import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { environment } from '@env';
import { RowAuditService } from './row-audit.service';
import { RowAuditEntry } from '@core/models/row-audit.model';

describe('RowAuditService', () => {
  let service: RowAuditService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/rowaudit`;

  const entries: RowAuditEntry[] = [
    { dateTime: '2026-06-04T14:30:00', userName: 'alice', actionType: 'Update', actionDesc: 'Title' },
    { dateTime: '2026-06-01T09:00:00', userName: 'bob', actionType: 'Insert', actionDesc: '課程A' },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [RowAuditService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(RowAuditService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getForRecord GETs with tableName + pkid query params', () => {
    let result: RowAuditEntry[] | undefined;
    service.getForRecord('Course', 123).subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${base}?tableName=Course&pkid=123`);
    expect(req.request.method).toBe('GET');
    req.flush(entries);

    expect(result?.length).toBe(2);
    expect(result?.[0].userName).toBe('alice');
  });
});
