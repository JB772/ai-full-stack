import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { AppUserService } from './app-user.service';
import { AppUser, AppUserRequest } from './app-user.model';

describe('AppUserService', () => {
  let service: AppUserService;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiBaseUrl}/app-users`;

  const admin: AppUser = {
    pkid: 1,
    userId: 'admin',
    userName: '系統管理員',
    isActive: true,
    passwordUpdatedTime: null,
    roleCount: 2
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(AppUserService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs all users', () => {
    let result: AppUser[] | undefined;
    service.getAll().subscribe(users => (result = users));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([admin]);

    expect(result).toEqual([admin]);
  });

  it('POSTs the filter to /query', () => {
    let result: AppUser[] | undefined;
    service.query({ keyword: 'admin', isActive: true }).subscribe(r => (result = r));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'admin', isActive: true });
    req.flush([admin]);

    expect(result?.length).toBe(1);
  });

  it('GETs a single user by pkid', () => {
    let result: AppUser | undefined;
    service.getByPkid(1).subscribe(user => (result = user));

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(admin);

    expect(result?.userId).toBe('admin');
  });

  it('POSTs a new user (no password in the body)', () => {
    const request: AppUserRequest = { pkid: 0, userId: 'editor', userName: '編輯者', isActive: true };

    let created: AppUser | undefined;
    service.create(request).subscribe(user => (created = user));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    expect('passwordHash' in req.request.body).toBeFalse();
    req.flush({ ...admin, pkid: 3, userId: 'editor' });

    expect(created?.pkid).toBe(3);
  });

  it('PUTs an updated user with the pkid in the body (no route param)', () => {
    const request: AppUserRequest = { pkid: 2, userId: 'guest', userName: '訪客', isActive: false };

    service.update(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.pkid).toBe(2);
    req.flush(null);
  });

  it('DELETEs a user by pkid', () => {
    service.delete(2).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/2`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('POSTs to the reset-password endpoint with no password payload', () => {
    service.resetPassword(1).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/1/reset-password`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(null);
  });
});
