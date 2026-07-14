import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { CourseGroupList } from './course-group-list';
import { CourseGroupService } from '../course-group.service';
import { CourseGroup } from '../course-group.model';

describe('CourseGroupList', () => {
  let fixture: ComponentFixture<CourseGroupList>;
  let component: CourseGroupList;
  let service: jasmine.SpyObj<CourseGroupService>;
  let confirmationService: ConfirmationService;
  let router: Router;

  const database: CourseGroup = { pkid: 2, description: '資料庫', courseCount: 12, partnerCourseGroupCount: 3 };
  const cloud: CourseGroup = { pkid: 1, description: '雲端', courseCount: 0, partnerCourseGroupCount: 0 };

  beforeEach(async () => {
    sessionStorage.clear();

    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['query', 'delete']);
    // p-table sorts its bound array IN PLACE, and this component defaults to sorting by description — so a
    // shared array would be reordered under the spec's feet. Hand over a fresh copy on every call instead,
    // and assert against the named consts rather than by index.
    service.query.and.callFake(() => of([{ ...database }, { ...cloud }]));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [CourseGroupList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: CourseGroupService, useValue: service }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseGroupList);
    component = fixture.componentInstance;
    confirmationService = TestBed.inject(ConfirmationService);
    router = TestBed.inject(Router);
  });

  /** The component's members are `protected`, which TS blocks from outside the class but exists at runtime. */
  function api(): any {
    return component as any;
  }

  afterEach(() => sessionStorage.clear());

  it('loads groups on init', () => {
    fixture.detectChanges();

    expect(service.query).toHaveBeenCalledTimes(1);
    // Order-independent: the rendered order is the browser's collation of 群組說明, not something to pin here.
    expect(api().groups()).toEqual(jasmine.arrayWithExactContents([database, cloud]));
    expect(api().loading()).toBeFalse();
  });

  it('defaults the sort to description, not pkid', () => {
    fixture.detectChanges();

    expect(api().sortField).toBe('description');
    expect(api().sortOrder).toBe(1);
  });

  /** Locates a row by its 群組說明, so the assertion does not depend on the rendered sort order. */
  function rowFor(description: string): HTMLElement {
    const rows: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('tbody tr'));
    const row = rows.find(r => r.textContent?.includes(description));
    expect(row).withContext(`no row for ${description}`).toBeDefined();
    return row!;
  }

  it('renders one row per group', () => {
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(2);
    expect(rowFor('資料庫')).toBeDefined();
    expect(rowFor('雲端')).toBeDefined();
  });

  it('renders both child counts', () => {
    fixture.detectChanges();

    const text = rowFor('資料庫').textContent!;
    expect(text).toContain('12');
    expect(text).toContain('3');
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

    api().filters.keyword = '資料庫';
    api().first = 20;
    api().applyFilters();

    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ keyword: '資料庫' }));
    expect(api().first).toBe(0);
    expect(JSON.parse(sessionStorage.getItem('course-group-list-filters')!).keyword).toBe('資料庫');
    expect(api().filterDrawerVisible()).toBeFalse();
  });

  it('clears the filter', () => {
    fixture.detectChanges();

    api().filters.keyword = '資料庫';
    expect(api().hasActiveFilters).toBeTrue();

    api().clearFilters();

    expect(api().filters).toEqual({ keyword: null });
    expect(api().hasActiveFilters).toBeFalse();
  });

  it('restores filters, sort and page from session storage on init', () => {
    sessionStorage.setItem('course-group-list-filters', JSON.stringify({ keyword: '雲端' }));
    sessionStorage.setItem('course-group-list-sort', JSON.stringify({ sortField: 'pkid', sortOrder: -1 }));
    sessionStorage.setItem('course-group-list-page', JSON.stringify({ first: 20, rows: 50 }));

    fixture = TestBed.createComponent(CourseGroupList);
    fixture.detectChanges();

    const restored = fixture.componentInstance as any;
    expect(restored.filters.keyword).toBe('雲端');
    expect(restored.sortField).toBe('pkid');
    expect(restored.sortOrder).toBe(-1);
    expect(restored.first).toBe(20);
    expect(restored.rows).toBe(50);
    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ keyword: '雲端' }));
  });

  it('ignores corrupt session storage instead of throwing', () => {
    sessionStorage.setItem('course-group-list-sort', '{not json');

    fixture = TestBed.createComponent(CourseGroupList);
    expect(() => fixture.detectChanges()).not.toThrow();
    expect((fixture.componentInstance as any).sortField).toBe('description');
  });

  it('persists sort state', () => {
    fixture.detectChanges();

    api().onSort({ field: 'courseCount', order: -1 });

    expect(JSON.parse(sessionStorage.getItem('course-group-list-sort')!)).toEqual({
      sortField: 'courseCount',
      sortOrder: -1
    });
  });

  it('persists page state', () => {
    fixture.detectChanges();

    api().onPage({ first: 40, rows: 10 });

    expect(JSON.parse(sessionStorage.getItem('course-group-list-page')!)).toEqual({ first: 40, rows: 10 });
  });

  it('deletes an unused group after confirmation and reloads the list', () => {
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    fixture.detectChanges();

    api().confirmDelete(cloud);

    expect(service.delete).toHaveBeenCalledWith(1);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('shows the API message when a delete is rejected (group still has courses)', () => {
    // The 409 is what stops the ON DELETE CASCADE from wiping those 12 courses — the user must see why.
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    service.delete.and.returnValue(
      throwError(() => ({ error: { message: '課程群組「資料庫」仍有 12 筆課程、3 筆夥伴課程群組，無法刪除。' } }))
    );
    fixture.detectChanges();

    api().confirmDelete(database);

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({
        severity: 'error',
        detail: '課程群組「資料庫」仍有 12 筆課程、3 筆夥伴課程群組，無法刪除。'
      })
    );
  });

  it('navigates to the detail and edit pages', () => {
    const navigate = spyOn(router, 'navigate');
    fixture.detectChanges();

    api().view(database);
    expect(navigate).toHaveBeenCalledWith(['/course-groups', 2]);

    api().edit(cloud);
    expect(navigate).toHaveBeenCalledWith(['/course-groups', 1, 'edit']);
  });
});
