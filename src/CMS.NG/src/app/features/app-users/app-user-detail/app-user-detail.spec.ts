import { WritableSignal, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { AppUserDetail } from './app-user-detail';
import { AppUserService } from '../app-user.service';
import { AppRoleService } from '../../app-roles/app-role.service';
import { AuthService } from '../../auth/auth.service';
import { AppUser } from '../app-user.model';
import { RowAuditService } from '../../row-audit/row-audit.service';

describe('AppUserDetail', () => {
  let fixture: ComponentFixture<AppUserDetail>;
  let service: jasmine.SpyObj<AppUserService>;
  let roleService: jasmine.SpyObj<AppRoleService>;
  let auth: { isAdmin: WritableSignal<boolean>; resetPasswordToDefault: jasmine.Spy };
  let confirmationService: ConfirmationService;
  let router: Router;

  const admin: AppUser = {
    pkid: 1,
    userId: 'admin',
    userName: '系統管理員',
    isActive: true,
    passwordUpdatedTime: null,
    roleCount: 2
  };

  async function setup(id = '1', isAdmin = true) {
    service = jasmine.createSpyObj<AppUserService>('AppUserService',
      ['getByPkid', 'getRoles', 'assignRole', 'removeRole']);
    service.getByPkid.and.returnValue(of(admin));
    service.getRoles.and.returnValue(of([{ roleId: 'Admin', roleName: 'Administrator' }, { roleId: 'User', roleName: 'User' }]));
    service.assignRole.and.returnValue(of([{ roleId: 'Admin', roleName: 'Administrator' }]));
    service.removeRole.and.returnValue(of(void 0));

    roleService = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['getAll']);
    roleService.getAll.and.returnValue(of([
      { pkid: 1, roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1, description: null, userCount: 2 },
      { pkid: 2, roleId: 'User', roleName: 'User', permissionLevel: 100, description: null, userCount: 1 },
      { pkid: 3, roleId: 'browser', roleName: 'browser', permissionLevel: 77, description: null, userCount: 0 }
    ]));

    auth = {
      isAdmin: signal(isAdmin),
      resetPasswordToDefault: jasmine.createSpy('resetPasswordToDefault').and.returnValue(of(void 0))
    };

    await TestBed.configureTestingModule({
      imports: [AppUserDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: AppUserService, useValue: service },
        { provide: AppRoleService, useValue: roleService },
        { provide: AuthService, useValue: auth },
        { provide: RowAuditService, useValue: { getForRecord: () => of([]) } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['id', id]]) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppUserDetail);
    confirmationService = TestBed.inject(ConfirmationService);
    router = TestBed.inject(Router);
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  it('loads the user identified by the route param', async () => {
    await setup('1');
    fixture.detectChanges();

    expect(service.getByPkid).toHaveBeenCalledWith(1);
    expect(api().user()).toEqual(admin);
  });

  it('renders the user fields', async () => {
    await setup();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('admin');
    expect(text).toContain('系統管理員');
    expect(text).toContain('啟用');
  });

  it('navigates to the edit page', async () => {
    await setup();
    fixture.detectChanges();
    const navigate = spyOn(router, 'navigate');

    api().edit();

    expect(navigate).toHaveBeenCalledWith(['/app-users', 1, 'edit']);
  });

  it('shows the reset-password button for Admins', async () => {
    await setup('1', true);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('重設密碼');
  });

  it('hides the reset-password button for non-Admins', async () => {
    await setup('1', false);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('重設密碼');
  });

  it('resets the password (by userId) after confirmation and reloads', async () => {
    await setup();
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    fixture.detectChanges();

    api().confirmResetPassword();

    expect(auth.resetPasswordToDefault).toHaveBeenCalledWith('admin');
    // once on init + once on reload after reset.
    expect(service.getByPkid).toHaveBeenCalledTimes(2);
    expect(messageService.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success' }));
  });

  it('surfaces a reset-password failure', async () => {
    await setup();
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    auth.resetPasswordToDefault.and.returnValue(throwError(() => ({ error: { message: '重設密碼時發生錯誤。' } })));
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    fixture.detectChanges();

    api().confirmResetPassword();

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', detail: '重設密碼時發生錯誤。' })
    );
    expect(api().resetting()).toBeFalse();
  });

  it('returns to the list when the user is not found', async () => {
    await setup('999');
    service.getByPkid.and.returnValue(throwError(() => new Error('404')));
    const navigate = spyOn(router, 'navigate');
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');

    fixture.detectChanges();

    expect(messageService.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error' }));
    expect(navigate).toHaveBeenCalledWith(['/app-users']);
  });

  // ---------- Role management (Admin-only N-N editor) ----------

  it('loads current + available roles on init for Admins', async () => {
    await setup('1', true);
    fixture.detectChanges();

    expect(service.getRoles).toHaveBeenCalledWith(1);
    expect(roleService.getAll).toHaveBeenCalled();
    expect(api().roles().map((r: any) => r.roleId)).toEqual(['Admin', 'User']);
  });

  it('does not load roles for non-Admins', async () => {
    await setup('1', false);
    fixture.detectChanges();

    expect(service.getRoles).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).not.toContain('角色管理');
  });

  it('shows the roles card for Admins', async () => {
    await setup('1', true);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('角色管理');
  });

  it('offers only unassigned roles as options', async () => {
    await setup('1', true);
    fixture.detectChanges();

    // admin already has Admin + User → only "browser" is assignable.
    expect(api().assignableRoles().map((o: any) => o.value)).toEqual(['browser']);
  });

  it('assigns the selected role and refreshes', async () => {
    await setup('1', true);
    fixture.detectChanges();
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');

    api().selectedRoleId.set('browser');
    api().assignRole();

    expect(service.assignRole).toHaveBeenCalledWith(1, 'browser');
    expect(api().selectedRoleId()).toBeNull();
    expect(messageService.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success' }));
  });

  it('removes a role after confirmation', async () => {
    await setup('1', true);
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    fixture.detectChanges();

    api().confirmRemoveRole({ roleId: 'User', roleName: 'User' });

    expect(service.removeRole).toHaveBeenCalledWith(1, 'User');
    expect(messageService.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'success' }));
  });

  it('surfaces an assign-role failure', async () => {
    await setup('1', true);
    service.assignRole.and.returnValue(throwError(() => ({ error: { message: '指派角色時發生錯誤。' } })));
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    fixture.detectChanges();

    api().selectedRoleId.set('browser');
    api().assignRole();

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', detail: '指派角色時發生錯誤。' })
    );
    expect(api().savingRole()).toBeFalse();
  });
});
