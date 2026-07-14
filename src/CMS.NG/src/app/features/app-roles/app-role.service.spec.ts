import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { AppRoleService } from './app-role.service';
import { AppRole, AppRoleRequest } from './app-role.model';

describe('AppRoleService', () => {
  let service: AppRoleService;
  let httpMock: HttpTestingController;

  const baseUrl = `${environment.apiBaseUrl}/app-roles`;

  const admin: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 3
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(AppRoleService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs all roles', () => {
    let result: AppRole[] | undefined;
    service.getAll().subscribe(roles => (result = roles));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush([admin]);

    expect(result).toEqual([admin]);
  });

  it('POSTs the filter to /query', () => {
    let result: AppRole[] | undefined;
    service.query({ keyword: 'Admin', permissionLevelFrom: 1, permissionLevelTo: 50 }).subscribe(r => (result = r));

    const req = httpMock.expectOne(`${baseUrl}/query`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ keyword: 'Admin', permissionLevelFrom: 1, permissionLevelTo: 50 });
    req.flush([admin]);

    expect(result?.length).toBe(1);
  });

  it('GETs a single role by pkid', () => {
    let result: AppRole | undefined;
    service.getByPkid(1).subscribe(role => (result = role));

    const req = httpMock.expectOne(`${baseUrl}/1`);
    expect(req.request.method).toBe('GET');
    req.flush(admin);

    expect(result?.roleId).toBe('Admin');
  });

  it('POSTs a new role', () => {
    const request: AppRoleRequest = {
      pkid: 0,
      roleId: 'Editor',
      roleName: 'Content Editor',
      permissionLevel: 50,
      description: null
    };

    let created: AppRole | undefined;
    service.create(request).subscribe(role => (created = role));

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush({ ...admin, pkid: 3, roleId: 'Editor' });

    expect(created?.pkid).toBe(3);
  });

  it('PUTs an updated role with the pkid in the body (no route param)', () => {
    const request: AppRoleRequest = {
      pkid: 2,
      roleId: 'User',
      roleName: '一般使用者',
      permissionLevel: 200,
      description: '描述'
    };

    service.update(request).subscribe();

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.pkid).toBe(2);
    req.flush(null);
  });

  it('DELETEs a role by pkid', () => {
    service.delete(2).subscribe();

    const req = httpMock.expectOne(`${baseUrl}/2`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
