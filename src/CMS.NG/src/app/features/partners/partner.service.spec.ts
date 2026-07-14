import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { PartnerService } from './partner.service';
import { Partner, PartnerRequest } from './partner.model';

describe('PartnerService', () => {
  let service: PartnerService;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiBaseUrl}/partners`;

  const microsoft: Partner = {
    pkid: 1,
    name: 'Microsoft',
    appKey: 'MS',
    nameOnPartnerMenu: 'Microsoft 微軟',
    nameOnCourseDetailPage: '微軟',
    displayOrder: 1,
    imageFilename: 'microsoft.png',
    courseCount: 12,
    certificationCount: 3,
    courseGroupCount: 0,
    promotionCount: 0,
    seminarCount: 0
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(PartnerService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs all partners', () => {
    let result: Partner[] | undefined;
    service.getAll().subscribe(partners => (result = partners));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([microsoft]);

    expect(result).toEqual([microsoft]);
  });

  it('POSTs the filter to /query', () => {
    let result: Partner[] | undefined;
    service.query({ keyword: 'Cisco', displayOrderFrom: 1, displayOrderTo: 5, hasImage: false })
      .subscribe(r => (result = r));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      keyword: 'Cisco',
      displayOrderFrom: 1,
      displayOrderTo: 5,
      hasImage: false
    });
    req.flush([microsoft]);

    expect(result?.length).toBe(1);
  });

  it('GETs a single partner by pkid', () => {
    let result: Partner | undefined;
    service.getByPkid(1).subscribe(partner => (result = partner));

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(microsoft);

    expect(result?.name).toBe('Microsoft');
  });

  it('POSTs a new partner (pkid 0 — the database generates the real key)', () => {
    const request: PartnerRequest = {
      pkid: 0,
      name: 'AWS',
      appKey: 'AWS',
      nameOnPartnerMenu: 'AWS 選單名稱',
      nameOnCourseDetailPage: 'AWS',
      displayOrder: 10,
      imageFilename: null
    };

    let created: Partner | undefined;
    service.create(request).subscribe(partner => (created = partner));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.pkid).toBe(0);
    req.flush({ ...microsoft, pkid: 4, name: 'AWS' });

    expect(created?.pkid).toBe(4);
  });

  it('PUTs an updated partner with the pkid in the body (no route param)', () => {
    const request: PartnerRequest = {
      pkid: 1,
      name: 'Microsoft',
      appKey: 'MS',
      nameOnPartnerMenu: 'Microsoft 微軟',
      nameOnCourseDetailPage: '微軟',
      displayOrder: 1,
      imageFilename: 'microsoft.png'
    };

    service.update(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.pkid).toBe(1);
    req.flush(null);
  });

  it('DELETEs a partner by pkid', () => {
    service.delete(1).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
