import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { AppRoleDetail } from './app-role-detail';
import { AppRoleService } from '../app-role.service';
import { AppRole } from '../app-role.model';
import { RowAuditService } from '../../row-audit/row-audit.service';

describe('AppRoleDetail', () => {
  let fixture: ComponentFixture<AppRoleDetail>;
  let service: jasmine.SpyObj<AppRoleService>;
  let router: Router;

  const admin: AppRole = {
    pkid: 1,
    roleId: 'Admin',
    roleName: 'Administrator',
    permissionLevel: 1,
    description: '系統管理員',
    userCount: 3
  };

  async function setup(id = '1') {
    service = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['getByPkid']);
    service.getByPkid.and.returnValue(of(admin));

    await TestBed.configureTestingModule({
      imports: [AppRoleDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: AppRoleService, useValue: service },
        { provide: RowAuditService, useValue: { getForRecord: () => of([]) } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['id', id]]) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleDetail);
    router = TestBed.inject(Router);
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  it('loads the role identified by the route param', async () => {
    await setup('1');
    fixture.detectChanges();

    expect(service.getByPkid).toHaveBeenCalledWith(1);
    expect(api().role()).toEqual(admin);
  });

  it('renders every field of the role', async () => {
    await setup();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Admin');
    expect(text).toContain('Administrator');
    expect(text).toContain('系統管理員');
    expect(text).toContain('3');
  });

  it('navigates to the edit page', async () => {
    await setup();
    fixture.detectChanges();
    const navigate = spyOn(router, 'navigate');

    api().edit();

    expect(navigate).toHaveBeenCalledWith(['/app-roles', 1, 'edit']);
  });

  it('returns to the list when the role is not found', async () => {
    await setup('999');
    service.getByPkid.and.returnValue(throwError(() => new Error('404')));
    const navigate = spyOn(router, 'navigate');
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');

    fixture.detectChanges();

    expect(messageService.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error' }));
    expect(navigate).toHaveBeenCalledWith(['/app-roles']);
  });
});
