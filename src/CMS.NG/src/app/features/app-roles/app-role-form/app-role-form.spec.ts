import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { AppRoleForm } from './app-role-form';
import { AppRoleService } from '../app-role.service';
import { AppRole } from '../app-role.model';

describe('AppRoleForm', () => {
  let fixture: ComponentFixture<AppRoleForm>;
  let service: jasmine.SpyObj<AppRoleService>;
  let router: Router;

  const user: AppRole = {
    pkid: 2,
    roleId: 'User',
    roleName: 'User',
    permissionLevel: 100,
    description: '一般使用者',
    userCount: 0
  };

  /** `id` = null → add mode; a value → edit mode. */
  async function setup(id: string | null) {
    service = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['getByPkid', 'create', 'update']);
    service.getByPkid.and.returnValue(of(user));
    service.create.and.returnValue(of({ ...user, pkid: 3, roleId: 'Editor' }));
    service.update.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppRoleForm],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: AppRoleService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map(id ? [['id', id]] : []) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleForm);
    router = TestBed.inject(Router);
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  describe('add mode', () => {
    beforeEach(async () => await setup(null));

    it('starts blank, editable and in add mode', () => {
      fixture.detectChanges();

      expect(api().isEdit()).toBeFalse();
      expect(service.getByPkid).not.toHaveBeenCalled();
      expect(api().form.controls.roleId.enabled).toBeTrue();
      expect(api().form.controls.permissionLevel.value).toBe(100);
    });

    it('does not submit an invalid form', () => {
      fixture.detectChanges();

      api().save();

      expect(service.create).not.toHaveBeenCalled();
      expect(api().form.controls.roleId.touched).toBeTrue();
    });

    it('creates the role and navigates to its detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().form.setValue({
        roleId: 'Editor',
        roleName: 'Content Editor',
        permissionLevel: 50,
        description: '內容編輯者'
      });
      api().save();

      expect(service.create).toHaveBeenCalledWith({
        pkid: 0,
        roleId: 'Editor',
        roleName: 'Content Editor',
        permissionLevel: 50,
        description: '內容編輯者'
      });
      expect(navigate).toHaveBeenCalledWith(['/app-roles', 3]);
    });

    it('sends a null description when it is left blank', () => {
      fixture.detectChanges();

      api().form.setValue({ roleId: 'Editor', roleName: 'Content Editor', permissionLevel: 50, description: '  ' });
      api().save();

      expect(service.create).toHaveBeenCalledWith(jasmine.objectContaining({ description: null }));
    });

    it('reports a duplicate RoleId conflict from the API', () => {
      fixture.detectChanges();
      const messageService = TestBed.inject(MessageService);
      spyOn(messageService, 'add');
      service.create.and.returnValue(throwError(() => ({ error: { message: '角色代碼「Admin」已存在。' } })));

      api().form.setValue({ roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1, description: '' });
      api().save();

      expect(messageService.add).toHaveBeenCalledWith(
        jasmine.objectContaining({ severity: 'error', detail: '角色代碼「Admin」已存在。' })
      );
      expect(api().saving()).toBeFalse();
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => await setup('2'));

    it('loads the role and locks RoleId', () => {
      fixture.detectChanges();

      expect(api().isEdit()).toBeTrue();
      expect(service.getByPkid).toHaveBeenCalledWith(2);
      expect(api().form.controls.roleId.disabled).toBeTrue();
      expect(api().form.getRawValue()).toEqual({
        roleId: 'User',
        roleName: 'User',
        permissionLevel: 100,
        description: '一般使用者'
      });
    });

    it('updates the role with the pkid and navigates to its detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().form.controls.roleName.setValue('一般使用者');
      api().form.controls.permissionLevel.setValue(200);
      api().save();

      expect(service.update).toHaveBeenCalledWith({
        pkid: 2,
        roleId: 'User',
        roleName: '一般使用者',
        permissionLevel: 200,
        description: '一般使用者'
      });
      expect(navigate).toHaveBeenCalledWith(['/app-roles', 2]);
    });

    it('cancels back to the detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().cancel();

      expect(navigate).toHaveBeenCalledWith(['/app-roles', 2]);
    });
  });
});
