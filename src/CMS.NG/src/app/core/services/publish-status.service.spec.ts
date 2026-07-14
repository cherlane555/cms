import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { environment } from '@env';
import { PublishStatusService } from './publish-status.service';
import {
  PublishStatus,
  PublishStatusQuery,
  PublishStatusRequest,
} from '@core/models/publish-status.model';

describe('PublishStatusService', () => {
  let service: PublishStatusService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/publish-statuses`;

  const sample: PublishStatus = {
    pkid: 1,
    description: '草稿',
    isDraft: true,
    isPublished: false,
    isDiscontinued: false,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PublishStatusService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PublishStatusService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll issues GET and returns statuses', () => {
    let result: PublishStatus[] | undefined;
    service.getAll().subscribe((r) => (result = r));

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);

    expect(result?.length).toBe(1);
    expect(result?.[0].pkid).toBe(1);
  });

  it('query POSTs the filter to /query', () => {
    const query: PublishStatusQuery = { keyword: '草', isPublished: false };
    service.query(query).subscribe();

    const req = httpMock.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(query);
    req.flush([sample]);
  });

  it('getById GETs a single status by numeric pkid', () => {
    service.getById(1).subscribe();
    const req = httpMock.expectOne(`${base}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('create POSTs the request', () => {
    const request: PublishStatusRequest = {
      pkid: 3,
      description: '已發布',
      isDraft: false,
      isPublished: true,
      isDiscontinued: false,
    };
    service.create(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...sample, pkid: 3, description: '已發布' });
  });

  it('update PUTs the request', () => {
    const request: PublishStatusRequest = {
      pkid: 1,
      description: '草稿',
      isDraft: true,
      isPublished: false,
      isDiscontinued: false,
    };
    service.update(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(null);
  });

  it('delete DELETEs by numeric pkid', () => {
    service.delete(1).subscribe();
    const req = httpMock.expectOne(`${base}/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
