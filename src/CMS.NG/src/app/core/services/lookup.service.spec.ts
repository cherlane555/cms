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
import { PartnerLookup } from '@core/models/partner.model';
import { CourseGroupLookup } from '@core/models/course-group.model';
import {
  PromotionLookup,
  TrainingCenterLookup,
} from '@core/models/featured-promo-item.model';

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

  it('getPartners GETs the partners lookup', () => {
    const partners: PartnerLookup[] = [{ pkid: 1, name: '恆逸資訊' }];
    let result: PartnerLookup[] | undefined;
    service.getPartners().subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/lookups/partners`);
    expect(req.request.method).toBe('GET');
    req.flush(partners);

    expect(result?.[0].name).toBe('恆逸資訊');
  });

  it('getCourseGroups GETs the course-groups lookup', () => {
    const groups: CourseGroupLookup[] = [{ pkid: 1, description: '資料庫管理' }];
    let result: CourseGroupLookup[] | undefined;
    service.getCourseGroups().subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/lookups/course-groups`);
    expect(req.request.method).toBe('GET');
    req.flush(groups);

    expect(result?.[0].description).toBe('資料庫管理');
  });

  it('getTrainingCenters GETs the training-centers lookup', () => {
    const centers: TrainingCenterLookup[] = [
      { pkid: 1, name: '台北' },
      { pkid: 5, name: '線上研討會' },
    ];
    let result: TrainingCenterLookup[] | undefined;
    service.getTrainingCenters().subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/lookups/training-centers`);
    expect(req.request.method).toBe('GET');
    req.flush(centers);

    expect(result?.length).toBe(2);
    expect(result?.[0].name).toBe('台北');
  });

  it('getPromotionByCode GETs the promotion by its URL-encoded code', () => {
    const promo: PromotionLookup = {
      pkid: 100,
      promoCode: '20251215_n8n',
      topic: 'n8n自動化三部曲',
      description: '從自動化新手到企業級AI架構師學習路徑',
    };
    let result: PromotionLookup | undefined;
    service.getPromotionByCode('20251215_n8n').subscribe((r) => (result = r));

    const req = httpMock.expectOne(
      `${environment.apiBaseUrl}/api/lookups/promotions/by-code/20251215_n8n`,
    );
    expect(req.request.method).toBe('GET');
    req.flush(promo);

    expect(result?.pkid).toBe(100);
  });
});
