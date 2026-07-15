import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { AppUserDetail } from './app-user-detail';
import { AppUserService } from '../app-user.service';
import { AppUser } from '../app-user.model';

describe('AppUserDetail', () => {
  let fixture: ComponentFixture<AppUserDetail>;
  let service: jasmine.SpyObj<AppUserService>;
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

  async function setup(id = '1') {
    service = jasmine.createSpyObj<AppUserService>('AppUserService', ['getByPkid', 'resetPassword']);
    service.getByPkid.and.returnValue(of(admin));
    service.resetPassword.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppUserDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: AppUserService, useValue: service },
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

  it('resets the password after confirmation and reloads', async () => {
    await setup();
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    fixture.detectChanges();

    api().confirmResetPassword();

    expect(service.resetPassword).toHaveBeenCalledWith(1);
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
    service.resetPassword.and.returnValue(throwError(() => ({ error: { message: '重設密碼時發生錯誤。' } })));
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
});
