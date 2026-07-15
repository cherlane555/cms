import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { environment } from '@env';
import { FeaturedPromoItemService } from './featured-promo-item.service';
import {
  FeaturedPromoItem,
  FeaturedPromoItemQuery,
  FeaturedPromoItemRequest,
} from '@core/models/featured-promo-item.model';

describe('FeaturedPromoItemService', () => {
  let service: FeaturedPromoItemService;
  let httpMock: HttpTestingController;
  const base = `${environment.apiBaseUrl}/api/featured-promo-items`;

  const sample: FeaturedPromoItem = {
    pkid: 1,
    scheduleOn: '2026-07-13T00:00:00',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 100,
    topic: 'n8n自動化三部曲',
    description: '從自動化新手到企業級AI架構師學習路徑',
    promoCode: '20251215_n8n',
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [FeaturedPromoItemService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(FeaturedPromoItemService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('query POSTs the TrainingCenter + week window filter to /query', () => {
    const query: FeaturedPromoItemQuery = {
      trainingCenterPkid: 1,
      scheduleFrom: '2026-07-13',
      scheduleTo: '2026-07-19',
    };
    let result: FeaturedPromoItem[] | undefined;
    service.query(query).subscribe((r) => (result = r));

    const req = httpMock.expectOne(`${base}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(query);
    req.flush([sample]);

    expect(result?.length).toBe(1);
    expect(result?.[0].promoCode).toBe('20251215_n8n');
  });

  it('getById GETs a single item', () => {
    service.getById(1).subscribe();
    const req = httpMock.expectOne(`${base}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(sample);
  });

  it('create POSTs the request', () => {
    const request: FeaturedPromoItemRequest = {
      pkid: 0,
      scheduleOn: '2026-07-13',
      trainingCenterPkid: 1,
      slot: 1,
      promotionPkid: 100,
      topic: 'n8n自動化三部曲',
      description: '從自動化新手到企業級AI架構師學習路徑',
    };
    service.create(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...sample, pkid: 5 });
  });

  it('update PUTs the request with the pkid in the body', () => {
    const request: FeaturedPromoItemRequest = {
      pkid: 1,
      scheduleOn: '2026-07-13',
      trainingCenterPkid: 1,
      slot: 2,
      promotionPkid: 100,
      topic: 'n8n自動化三部曲',
      description: '更新後描述',
    };
    service.update(request).subscribe();

    const req = httpMock.expectOne(base);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(null);
  });

  it('delete DELETEs by pkid', () => {
    service.delete(1).subscribe();
    const req = httpMock.expectOne(`${base}/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('move POSTs the direction to /{id}/move', () => {
    service.move(1, 'down').subscribe();
    const req = httpMock.expectOne(`${base}/1/move`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ direction: 'down' });
    req.flush(null);
  });

  it('copy stores the item values on the clipboard; clearClipboard empties it', () => {
    expect(service.clipboard()).toBeNull();

    service.copy(sample);

    expect(service.clipboard()).toEqual({
      promotionPkid: 100,
      promoCode: '20251215_n8n',
      topic: 'n8n自動化三部曲',
      description: '從自動化新手到企業級AI架構師學習路徑',
    });

    service.clearClipboard();
    expect(service.clipboard()).toBeNull();
  });
});
