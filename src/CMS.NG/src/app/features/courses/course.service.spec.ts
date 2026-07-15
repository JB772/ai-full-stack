import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { CourseService } from './course.service';
import { Course, CourseRequest } from './course.model';

describe('CourseService', () => {
  let service: CourseService;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiBaseUrl}/courses`;

  const azure: Course = {
    pkid: 1,
    title: 'Azure 基礎',
    officialTitle: 'Microsoft Azure Fundamentals',
    courseId: 'AZ-900',
    prodCourseId: 'PROD-AZ900',
    friendlyUrl: 'azure-fundamentals',
    displayOrder: 1,
    partnerPkid: 1,
    courseGroupPkid: 1,
    publishStatusPkid: 2,
    scheduleOn: '2026-01-01',
    scheduleOff: '2036-01-01',
    hour: 12,
    listPrice: 8000,
    learningCredit: 3.5,
    material: null,
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: true,
    partner: { pkid: 1, name: 'Microsoft' },
    courseGroup: { pkid: 1, description: '雲端服務' },
    publishStatus: { pkid: 2, description: '已上架' },
    courseFaqCount: 1,
    certificationCount: 2,
    jobCategoryCount: 0,
    relatedLinkCount: 0,
    hotCourseCount: 0,
    recommCount: 0
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(CourseService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs all courses', () => {
    let result: Course[] | undefined;
    service.getAll().subscribe(courses => (result = courses));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([azure]);

    expect(result).toEqual([azure]);
  });

  it('POSTs the filter to /query', () => {
    let result: Course[] | undefined;
    service.query({ keyword: 'Azure', partnerPkid: 1, canRepeat: true }).subscribe(r => (result = r));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'Azure', partnerPkid: 1, canRepeat: true });
    req.flush([azure]);

    expect(result?.length).toBe(1);
  });

  it('GETs a single course by pkid', () => {
    let result: Course | undefined;
    service.getByPkid(1).subscribe(course => (result = course));

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(azure);

    expect(result?.title).toBe('Azure 基礎');
  });

  it('POSTs a new course (pkid 0 — the database generates the real key)', () => {
    const request: CourseRequest = {
      pkid: 0,
      title: '新課程',
      officialTitle: null,
      courseId: 'NEW-101',
      prodCourseId: 'PROD-NEW-101',
      friendlyUrl: 'new-101',
      displayOrder: 10,
      partnerPkid: 1,
      courseGroupPkid: null,
      publishStatusPkid: 2,
      scheduleOn: '2026-01-01',
      scheduleOff: '2036-01-01',
      hour: 8,
      listPrice: 5000,
      learningCredit: 2.5,
      material: null,
      objective: null,
      target: null,
      prerequisites: null,
      outline: null,
      towardCertOrExam: null,
      note: null,
      otherInfo: null,
      canRepeat: false
    };

    let created: Course | undefined;
    service.create(request).subscribe(course => (created = course));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.pkid).toBe(0);
    req.flush({ ...azure, pkid: 4, title: '新課程' });

    expect(created?.pkid).toBe(4);
  });

  it('PUTs an updated course with the pkid in the body (no route param)', () => {
    let done = false;
    service.update({ ...azure, pkid: 1 } as unknown as CourseRequest).subscribe(() => (done = true));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.pkid).toBe(1);
    req.flush(null);

    expect(done).toBeTrue();
  });

  it('DELETEs a course by pkid', () => {
    service.delete(1).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
