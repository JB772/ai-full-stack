import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { AppUserList } from './app-user-list';
import { AppUserService } from '../app-user.service';
import { AppUser } from '../app-user.model';

describe('AppUserList', () => {
  let fixture: ComponentFixture<AppUserList>;
  let component: AppUserList;
  let service: jasmine.SpyObj<AppUserService>;
  let confirmationService: ConfirmationService;
  let router: Router;

  const users: AppUser[] = [
    { pkid: 1, userId: 'admin', userName: '系統管理員', isActive: true, passwordUpdatedTime: null, roleCount: 2 },
    { pkid: 2, userId: 'guest', userName: '訪客', isActive: false, passwordUpdatedTime: null, roleCount: 0 }
  ];

  beforeEach(async () => {
    sessionStorage.clear();

    service = jasmine.createSpyObj<AppUserService>('AppUserService', ['query', 'delete']);
    // Fresh copy per call — p-table sorts its bound array in place.
    service.query.and.callFake(() => of(users.map(u => ({ ...u }))));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppUserList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: AppUserService, useValue: service }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppUserList);
    component = fixture.componentInstance;
    confirmationService = TestBed.inject(ConfirmationService);
    router = TestBed.inject(Router);
  });

  function api(): any {
    return component as any;
  }

  afterEach(() => sessionStorage.clear());

  it('loads users on init', () => {
    fixture.detectChanges();

    expect(service.query).toHaveBeenCalledTimes(1);
    expect(api().users().length).toBe(2);
    expect(api().loading()).toBeFalse();
  });

  it('renders one row per user, locating by text (not index)', () => {
    fixture.detectChanges();

    const rows = Array.from(fixture.nativeElement.querySelectorAll('tbody tr')) as HTMLElement[];
    expect(rows.length).toBe(2);
    expect(rows.some(r => r.textContent!.includes('admin'))).toBeTrue();
    expect(rows.some(r => r.textContent!.includes('guest'))).toBeTrue();
  });

  /**
   * FE-01 regression guard — see the twin in app-user-detail.spec.ts for the full rationale.
   * `SYSDATETIME()` writes server LOCAL time and it serialises offset-less; the template used to
   * append `'Z'`, so DatePipe read it as UTC and shifted the display by the browser's offset (+8h
   * here). Only distinguishes the two behaviours on a non-UTC runner.
   */
  it('renders 密碼更新時間 as local wall-clock, not shifted by the browser timezone', () => {
    service.query.and.returnValue(of([{ ...users[0], passwordUpdatedTime: '2026-07-16T14:30:00' }]));

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('2026-07-16 14:30');
  });

  it('surfaces a load failure without leaving the spinner on', () => {
    service.query.and.returnValue(throwError(() => new Error('boom')));
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');

    fixture.detectChanges();

    expect(api().loading()).toBeFalse();
    expect(messageService.add).toHaveBeenCalledWith(jasmine.objectContaining({ severity: 'error' }));
  });

  it('applies the keyword filter, resets paging and persists it to session storage', () => {
    fixture.detectChanges();

    api().filters.keyword = 'admin';
    api().first = 20;
    api().applyFilters();

    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ keyword: 'admin' }));
    expect(api().first).toBe(0);
    expect(JSON.parse(sessionStorage.getItem('app-user-list-filters')!).keyword).toBe('admin');
    expect(api().filterDrawerVisible()).toBeFalse();
  });

  it('applies the IsActive tri-state filter', () => {
    fixture.detectChanges();

    api().filters.isActive = false;
    api().applyFilters();

    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ isActive: false }));
  });

  it('clears all filters', () => {
    fixture.detectChanges();

    api().filters.keyword = 'admin';
    api().filters.isActive = true;
    api().clearFilters();

    expect(api().filters).toEqual({ keyword: null, isActive: null });
    expect(api().hasActiveFilters).toBeFalse();
  });

  it('restores filters, sort and page from session storage on init', () => {
    sessionStorage.setItem('app-user-list-filters', JSON.stringify({ keyword: 'guest' }));
    sessionStorage.setItem('app-user-list-sort', JSON.stringify({ sortField: 'userName', sortOrder: -1 }));
    sessionStorage.setItem('app-user-list-page', JSON.stringify({ first: 20, rows: 50 }));

    fixture = TestBed.createComponent(AppUserList);
    fixture.detectChanges();

    const restored = fixture.componentInstance as any;
    expect(restored.filters.keyword).toBe('guest');
    expect(restored.sortField).toBe('userName');
    expect(restored.sortOrder).toBe(-1);
    expect(restored.first).toBe(20);
    expect(restored.rows).toBe(50);
    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ keyword: 'guest' }));
  });

  it('ignores corrupt session storage instead of throwing', () => {
    sessionStorage.setItem('app-user-list-sort', '{not json');

    fixture = TestBed.createComponent(AppUserList);
    expect(() => fixture.detectChanges()).not.toThrow();
    expect((fixture.componentInstance as any).sortField).toBe('userId');
  });

  it('persists sort state', () => {
    fixture.detectChanges();

    api().onSort({ field: 'userName', order: -1 });

    expect(JSON.parse(sessionStorage.getItem('app-user-list-sort')!)).toEqual({
      sortField: 'userName',
      sortOrder: -1
    });
  });

  it('persists page state', () => {
    fixture.detectChanges();

    api().onPage({ first: 40, rows: 10 });

    expect(JSON.parse(sessionStorage.getItem('app-user-list-page')!)).toEqual({ first: 40, rows: 10 });
  });

  it('deletes a user after confirmation and reloads the list', () => {
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    fixture.detectChanges();

    api().confirmDelete(users[1]);

    expect(service.delete).toHaveBeenCalledWith(2);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('shows the API message when a delete is rejected (user still has roles)', () => {
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    service.delete.and.returnValue(throwError(() => ({ error: { message: '使用者「admin」仍有 2 個角色關聯，無法刪除。' } })));
    fixture.detectChanges();

    api().confirmDelete(users[0]);

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', detail: '使用者「admin」仍有 2 個角色關聯，無法刪除。' })
    );
  });

  it('navigates to the detail and edit pages', () => {
    const navigate = spyOn(router, 'navigate');
    fixture.detectChanges();

    api().view(users[0]);
    expect(navigate).toHaveBeenCalledWith(['/app-users', 1]);

    api().edit(users[1]);
    expect(navigate).toHaveBeenCalledWith(['/app-users', 2, 'edit']);
  });
});
