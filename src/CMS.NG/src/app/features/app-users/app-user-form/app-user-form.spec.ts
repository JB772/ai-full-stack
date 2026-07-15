import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { AppUserForm } from './app-user-form';
import { AppUserService } from '../app-user.service';
import { AppUser } from '../app-user.model';

describe('AppUserForm', () => {
  let fixture: ComponentFixture<AppUserForm>;
  let service: jasmine.SpyObj<AppUserService>;
  let router: Router;

  const guest: AppUser = {
    pkid: 2,
    userId: 'guest',
    userName: '訪客',
    isActive: false,
    passwordUpdatedTime: null,
    roleCount: 0
  };

  /** `id` = null → add mode; a value → edit mode. */
  async function setup(id: string | null) {
    service = jasmine.createSpyObj<AppUserService>('AppUserService', ['getByPkid', 'create', 'update']);
    service.getByPkid.and.returnValue(of(guest));
    service.create.and.returnValue(of({ ...guest, pkid: 3, userId: 'editor' }));
    service.update.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppUserForm],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: AppUserService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map(id ? [['id', id]] : []) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppUserForm);
    router = TestBed.inject(Router);
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  describe('add mode', () => {
    beforeEach(async () => await setup(null));

    it('starts blank, editable, active by default and in add mode', () => {
      fixture.detectChanges();

      expect(api().isEdit()).toBeFalse();
      expect(service.getByPkid).not.toHaveBeenCalled();
      expect(api().form.controls.userId.enabled).toBeTrue();
      expect(api().form.controls.isActive.value).toBeTrue();
    });

    it('has no password control', () => {
      fixture.detectChanges();

      expect(Object.keys(api().form.controls)).toEqual(['userId', 'userName', 'isActive']);
    });

    it('does not submit an invalid form', () => {
      fixture.detectChanges();

      api().save();

      expect(service.create).not.toHaveBeenCalled();
      expect(api().form.controls.userId.touched).toBeTrue();
    });

    it('creates the user (no password in the request) and navigates to its detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().form.setValue({ userId: 'editor', userName: '編輯者', isActive: true });
      api().save();

      expect(service.create).toHaveBeenCalledWith({
        pkid: 0,
        userId: 'editor',
        userName: '編輯者',
        isActive: true
      });
      expect(navigate).toHaveBeenCalledWith(['/app-users', 3]);
    });

    it('reports a duplicate UserId conflict from the API', () => {
      fixture.detectChanges();
      const messageService = TestBed.inject(MessageService);
      spyOn(messageService, 'add');
      service.create.and.returnValue(throwError(() => ({ error: { message: '帳號「admin」已存在。' } })));

      api().form.setValue({ userId: 'admin', userName: '系統管理員', isActive: true });
      api().save();

      expect(messageService.add).toHaveBeenCalledWith(
        jasmine.objectContaining({ severity: 'error', detail: '帳號「admin」已存在。' })
      );
      expect(api().saving()).toBeFalse();
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => await setup('2'));

    it('loads the user and locks UserId', () => {
      fixture.detectChanges();

      expect(api().isEdit()).toBeTrue();
      expect(service.getByPkid).toHaveBeenCalledWith(2);
      expect(api().form.controls.userId.disabled).toBeTrue();
      expect(api().form.getRawValue()).toEqual({
        userId: 'guest',
        userName: '訪客',
        isActive: false
      });
    });

    it('updates the user with the pkid and navigates to its detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().form.controls.userName.setValue('訪客帳號');
      api().form.controls.isActive.setValue(true);
      api().save();

      expect(service.update).toHaveBeenCalledWith({
        pkid: 2,
        userId: 'guest',
        userName: '訪客帳號',
        isActive: true
      });
      expect(navigate).toHaveBeenCalledWith(['/app-users', 2]);
    });

    it('cancels back to the detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().cancel();

      expect(navigate).toHaveBeenCalledWith(['/app-users', 2]);
    });
  });
});
