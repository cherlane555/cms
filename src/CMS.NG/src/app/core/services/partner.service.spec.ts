import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { environment } from '@env';
import { PartnerService } from './partner.service';
import { Partner, PartnerQuery, PartnerRequest } from '@core/models/partner.model';

describe('PartnerService', () => {
  let service: PartnerService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/partners`;

  const sample: Partner = {
    pkid: 1,
    name: '恆逸資訊',
    appKey: 'uwa',
    nameOnPartnerMenu: '恆逸資訊教育訓練中心',
    nameOnCourseDetailPage: '恆逸',
    displayOrder: 10,
    imageFilename: 'uwa.png',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PartnerService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PartnerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll issues GET and returns partners', () => {
    let result: Partner[] | undefined;
    service.getAll().subscribe((r) => (result = r));

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('GET');
    req.flush([sample]);

    expect(result?.length).toBe(1);
    expect(result?.[0].name).toBe('恆逸資訊');
  });

  it('query POSTs the filter to /query', () => {
    const query: PartnerQuery = { keyword: '恆逸' };
    service.query(query).subscribe();

    const req = httpMock.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(query);
    req.flush([sample]);
  });

  it('getById GETs a single partner by numeric pkid', () => {
    service.getById(1).subscribe();
    const req = httpMock.expectOne(`${base}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('create POSTs the request', () => {
    const request: PartnerRequest = {
      pkid: 0,
      name: '新廠商',
      appKey: 'new',
      nameOnPartnerMenu: '新廠商選單名',
      nameOnCourseDetailPage: '新廠商',
      displayOrder: 20,
      imageFilename: null,
    };
    service.create(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...sample, pkid: 5, name: '新廠商' });
  });

  it('update PUTs the request', () => {
    const request: PartnerRequest = {
      pkid: 1,
      name: '恆逸資訊',
      appKey: 'uwa',
      nameOnPartnerMenu: '選單名',
      nameOnCourseDetailPage: '恆逸',
      displayOrder: 10,
      imageFilename: 'uwa.png',
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
