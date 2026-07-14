import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { AppRoleList } from './app-role-list';
import { AppRoleService } from '../app-role.service';
import { AppRole } from '../app-role.model';

describe('AppRoleList', () => {
  let fixture: ComponentFixture<AppRoleList>;
  let component: AppRoleList;
  let service: jasmine.SpyObj<AppRoleService>;
  let confirmationService: ConfirmationService;
  let router: Router;

  const roles: AppRole[] = [
    { pkid: 1, roleId: 'Admin', roleName: 'Administrator', permissionLevel: 1, description: '系統管理員', userCount: 3 },
    { pkid: 2, roleId: 'User', roleName: 'User', permissionLevel: 100, description: '一般使用者', userCount: 0 }
  ];

  beforeEach(async () => {
    sessionStorage.clear();

    service = jasmine.createSpyObj<AppRoleService>('AppRoleService', ['query', 'delete']);
    service.query.and.returnValue(of(roles));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [AppRoleList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: AppRoleService, useValue: service }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AppRoleList);
    component = fixture.componentInstance;
    confirmationService = TestBed.inject(ConfirmationService);
    router = TestBed.inject(Router);
  });

  /** The component's members are `protected`, which TS blocks from outside the class but exists at runtime. */
  function api(): any {
    return component as any;
  }

  afterEach(() => sessionStorage.clear());

  it('loads roles on init', () => {
    fixture.detectChanges();

    expect(service.query).toHaveBeenCalledTimes(1);
    expect(api().roles()).toEqual(roles);
    expect(api().loading()).toBeFalse();
  });

  it('renders one row per role', () => {
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Admin');
    expect(rows[1].textContent).toContain('User');
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

    api().filters.keyword = 'Admin';
    api().first = 20;
    api().applyFilters();

    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ keyword: 'Admin' }));
    expect(api().first).toBe(0);
    expect(JSON.parse(sessionStorage.getItem('app-role-list-filters')!).keyword).toBe('Admin');
    expect(api().filterDrawerVisible()).toBeFalse();
  });

  it('applies the permission level range filter', () => {
    fixture.detectChanges();

    api().filters.permissionLevelFrom = 50;
    api().filters.permissionLevelTo = 100;
    api().applyFilters();

    expect(service.query).toHaveBeenCalledWith(
      jasmine.objectContaining({ permissionLevelFrom: 50, permissionLevelTo: 100 })
    );
  });

  it('clears all filters', () => {
    fixture.detectChanges();

    api().filters.keyword = 'Admin';
    api().clearFilters();

    expect(api().filters).toEqual({ keyword: null, permissionLevelFrom: null, permissionLevelTo: null });
    expect(api().hasActiveFilters).toBeFalse();
  });

  it('restores filters, sort and page from session storage on init', () => {
    sessionStorage.setItem('app-role-list-filters', JSON.stringify({ keyword: 'User' }));
    sessionStorage.setItem('app-role-list-sort', JSON.stringify({ sortField: 'permissionLevel', sortOrder: -1 }));
    sessionStorage.setItem('app-role-list-page', JSON.stringify({ first: 20, rows: 50 }));

    fixture = TestBed.createComponent(AppRoleList);
    fixture.detectChanges();

    const restored = fixture.componentInstance as any;
    expect(restored.filters.keyword).toBe('User');
    expect(restored.sortField).toBe('permissionLevel');
    expect(restored.sortOrder).toBe(-1);
    expect(restored.first).toBe(20);
    expect(restored.rows).toBe(50);
    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ keyword: 'User' }));
  });

  it('ignores corrupt session storage instead of throwing', () => {
    sessionStorage.setItem('app-role-list-sort', '{not json');

    fixture = TestBed.createComponent(AppRoleList);
    expect(() => fixture.detectChanges()).not.toThrow();
    expect((fixture.componentInstance as any).sortField).toBe('roleId');
  });

  it('persists sort state', () => {
    fixture.detectChanges();

    api().onSort({ field: 'roleName', order: -1 });

    expect(JSON.parse(sessionStorage.getItem('app-role-list-sort')!)).toEqual({
      sortField: 'roleName',
      sortOrder: -1
    });
  });

  it('persists page state', () => {
    fixture.detectChanges();

    api().onPage({ first: 40, rows: 10 });

    expect(JSON.parse(sessionStorage.getItem('app-role-list-page')!)).toEqual({ first: 40, rows: 10 });
  });

  it('deletes a role after confirmation and reloads the list', () => {
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    fixture.detectChanges();

    api().confirmDelete(roles[1]);

    expect(service.delete).toHaveBeenCalledWith(2);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('shows the API message when a delete is rejected (role still has users)', () => {
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    service.delete.and.returnValue(throwError(() => ({ error: { message: '角色「Admin」仍有 3 位使用者，無法刪除。' } })));
    fixture.detectChanges();

    api().confirmDelete(roles[0]);

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', detail: '角色「Admin」仍有 3 位使用者，無法刪除。' })
    );
  });

  it('navigates to the detail and edit pages', () => {
    const navigate = spyOn(router, 'navigate');
    fixture.detectChanges();

    api().view(roles[0]);
    expect(navigate).toHaveBeenCalledWith(['/app-roles', 1]);

    api().edit(roles[1]);
    expect(navigate).toHaveBeenCalledWith(['/app-roles', 2, 'edit']);
  });
});
