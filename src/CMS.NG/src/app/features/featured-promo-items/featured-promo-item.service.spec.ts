import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { FeaturedPromoItemService } from './featured-promo-item.service';
import { FeaturedPromoItem, FeaturedPromoItemRequest } from './featured-promo-item.model';

describe('FeaturedPromoItemService', () => {
  let service: FeaturedPromoItemService;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiBaseUrl}/featured-promo-items`;
  const lookupsUrl = `${environment.apiBaseUrl}/lookups`;

  const item: FeaturedPromoItem = {
    pkid: 1,
    scheduleOn: '2026-03-16',
    trainingCenterPkid: 1,
    slot: 1,
    promotionPkid: 10,
    topic: '成為能AI協作的程式設計師',
    description: '轉職就業養成班',
    trainingCenter: { pkid: 1, name: '台北' },
    promotion: { pkid: 10, promoCode: '20251204_SkillTrainAI' }
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(FeaturedPromoItemService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs all items', () => {
    let result: FeaturedPromoItem[] | undefined;
    service.getAll().subscribe(items => (result = items));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([item]);

    expect(result).toEqual([item]);
  });

  it('POSTs the one-week + training-centre filter to /query', () => {
    service.query({
      trainingCenterPkid: 1,
      scheduleOnFrom: '2026-03-16',
      scheduleOnTo: '2026-03-22'
    }).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      trainingCenterPkid: 1,
      scheduleOnFrom: '2026-03-16',
      scheduleOnTo: '2026-03-22'
    });
    req.flush([item]);
  });

  it('GETs a single item by pkid', () => {
    let result: FeaturedPromoItem | undefined;
    service.getByPkid(1).subscribe(i => (result = i));

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(item);

    expect(result?.topic).toBe('成為能AI協作的程式設計師');
  });

  it('POSTs a new item (pkid 0 — the database generates the real key)', () => {
    const request: FeaturedPromoItemRequest = {
      pkid: 0,
      scheduleOn: '2026-03-18',
      trainingCenterPkid: 1,
      slot: 1,
      promotionPkid: 10,
      topic: '新標題',
      description: '新說明'
    };

    let created: FeaturedPromoItem | undefined;
    service.create(request).subscribe(i => (created = i));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.pkid).toBe(0);
    req.flush({ ...item, pkid: 7 });

    expect(created?.pkid).toBe(7);
  });

  it('PUTs an updated item with the pkid in the body (no route param)', () => {
    let done = false;
    service.update({ ...item } as unknown as FeaturedPromoItemRequest).subscribe(() => (done = true));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.pkid).toBe(1);
    req.flush(null);

    expect(done).toBeTrue();
  });

  it('DELETEs an item by pkid', () => {
    service.delete(1).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('POSTs a slot move with the direction in the body', () => {
    service.move(1, 'down').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/1/move`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ direction: 'down' });
    req.flush(null);
  });

  it('GETs the training-centre tabs', () => {
    service.getTrainingCenters().subscribe();

    const req = httpMock.expectOne(`${lookupsUrl}/training-centers`);
    expect(req.request.method).toBe('GET');
    req.flush([{ pkid: 1, name: '台北', displayOrder: 1 }]);
  });

  it('GETs the PromoCode lookup list', () => {
    let result: unknown;
    service.getPromoCodes().subscribe(r => (result = r));

    const req = httpMock.expectOne(`${lookupsUrl}/promo-codes`);
    expect(req.request.method).toBe('GET');
    req.flush([{ pkid: 10, promoCode: '20251204_SkillTrainAI', topic: 'T', description: 'D' }]);

    expect((result as unknown[]).length).toBe(1);
  });
});
