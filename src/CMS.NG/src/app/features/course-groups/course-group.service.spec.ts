import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { CourseGroupService } from './course-group.service';
import { CourseGroup, CourseGroupRequest } from './course-group.model';

describe('CourseGroupService', () => {
  let service: CourseGroupService;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiBaseUrl}/course-groups`;

  const cloud: CourseGroup = {
    pkid: 1,
    description: '雲端',
    courseCount: 0,
    partnerCourseGroupCount: 0
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(CourseGroupService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs all groups', () => {
    let result: CourseGroup[] | undefined;
    service.getAll().subscribe(groups => (result = groups));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([cloud]);

    expect(result).toEqual([cloud]);
  });

  it('POSTs the filter to /query', () => {
    let result: CourseGroup[] | undefined;
    service.query({ keyword: '雲端' }).subscribe(r => (result = r));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: '雲端' });
    req.flush([cloud]);

    expect(result?.length).toBe(1);
  });

  it('GETs a single group by pkid', () => {
    let result: CourseGroup | undefined;
    service.getByPkid(1).subscribe(group => (result = group));

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(cloud);

    expect(result?.description).toBe('雲端');
  });

  it('POSTs a new group with pkid 0 — the DB generates the real key', () => {
    const request: CourseGroupRequest = { pkid: 0, description: '人工智慧' };

    let created: CourseGroup | undefined;
    service.create(request).subscribe(group => (created = group));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.pkid).toBe(0);
    req.flush({ ...cloud, pkid: 4, description: '人工智慧' });

    expect(created?.pkid).toBe(4);
  });

  it('PUTs an updated group with the pkid in the body (no route param)', () => {
    const request: CourseGroupRequest = { pkid: 2, description: '資料庫' };

    service.update(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.pkid).toBe(2);
    req.flush(null);
  });

  it('DELETEs a group by pkid', () => {
    service.delete(2).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/2`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
