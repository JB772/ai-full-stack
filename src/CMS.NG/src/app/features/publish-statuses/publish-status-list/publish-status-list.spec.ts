import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { PublishStatusList } from './publish-status-list';
import { PublishStatusService } from '../publish-status.service';
import { PublishStatus } from '../publish-status.model';

describe('PublishStatusList', () => {
  let fixture: ComponentFixture<PublishStatusList>;
  let component: PublishStatusList;
  let service: jasmine.SpyObj<PublishStatusService>;
  let confirmationService: ConfirmationService;
  let router: Router;

  const statuses: PublishStatus[] = [
    { pkid: 1, description: '草稿', isDraft: true, isPublished: false, isDiscontinued: false, courseCount: 0, promotionCount: 0 },
    { pkid: 2, description: '已發布', isDraft: false, isPublished: true, isDiscontinued: false, courseCount: 5, promotionCount: 2 }
  ];

  beforeEach(async () => {
    sessionStorage.clear();

    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['query', 'delete']);
    service.query.and.returnValue(of(statuses));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PublishStatusList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: PublishStatusService, useValue: service }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusList);
    component = fixture.componentInstance;
    confirmationService = TestBed.inject(ConfirmationService);
    router = TestBed.inject(Router);
  });

  /** The component's members are `protected`, which TS blocks from outside the class but exists at runtime. */
  function api(): any {
    return component as any;
  }

  afterEach(() => sessionStorage.clear());

  it('loads statuses on init', () => {
    fixture.detectChanges();

    expect(service.query).toHaveBeenCalledTimes(1);
    expect(api().statuses()).toEqual(statuses);
    expect(api().loading()).toBeFalse();
  });

  it('renders one row per status', () => {
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('草稿');
    expect(rows[1].textContent).toContain('已發布');
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

    api().filters.keyword = '草稿';
    api().first = 20;
    api().applyFilters();

    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ keyword: '草稿' }));
    expect(api().first).toBe(0);
    expect(JSON.parse(sessionStorage.getItem('publish-status-list-filters')!).keyword).toBe('草稿');
    expect(api().filterDrawerVisible()).toBeFalse();
  });

  it('applies the tri-state bool filters', () => {
    fixture.detectChanges();

    api().filters.isDraft = true;
    api().filters.isPublished = false;
    api().applyFilters();

    expect(service.query).toHaveBeenCalledWith(
      jasmine.objectContaining({ isDraft: true, isPublished: false })
    );
  });

  it('treats a false bool filter as active (not the same as unset)', () => {
    fixture.detectChanges();

    api().filters.isPublished = false;

    expect(api().hasActiveFilters).toBeTrue();
  });

  it('clears all filters', () => {
    fixture.detectChanges();

    api().filters.keyword = '草稿';
    api().filters.isDraft = true;
    api().clearFilters();

    expect(api().filters).toEqual({ keyword: null, isDraft: null, isPublished: null, isDiscontinued: null });
    expect(api().hasActiveFilters).toBeFalse();
  });

  it('restores filters, sort and page from session storage on init', () => {
    sessionStorage.setItem('publish-status-list-filters', JSON.stringify({ keyword: '已發布' }));
    sessionStorage.setItem('publish-status-list-sort', JSON.stringify({ sortField: 'description', sortOrder: -1 }));
    sessionStorage.setItem('publish-status-list-page', JSON.stringify({ first: 20, rows: 50 }));

    fixture = TestBed.createComponent(PublishStatusList);
    fixture.detectChanges();

    const restored = fixture.componentInstance as any;
    expect(restored.filters.keyword).toBe('已發布');
    expect(restored.sortField).toBe('description');
    expect(restored.sortOrder).toBe(-1);
    expect(restored.first).toBe(20);
    expect(restored.rows).toBe(50);
    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ keyword: '已發布' }));
  });

  it('ignores corrupt session storage instead of throwing', () => {
    sessionStorage.setItem('publish-status-list-sort', '{not json');

    fixture = TestBed.createComponent(PublishStatusList);
    expect(() => fixture.detectChanges()).not.toThrow();
    expect((fixture.componentInstance as any).sortField).toBe('pkid');
  });

  it('persists sort state', () => {
    fixture.detectChanges();

    api().onSort({ field: 'description', order: -1 });

    expect(JSON.parse(sessionStorage.getItem('publish-status-list-sort')!)).toEqual({
      sortField: 'description',
      sortOrder: -1
    });
  });

  it('persists page state', () => {
    fixture.detectChanges();

    api().onPage({ first: 40, rows: 10 });

    expect(JSON.parse(sessionStorage.getItem('publish-status-list-page')!)).toEqual({ first: 40, rows: 10 });
  });

  it('deletes an unused status after confirmation and reloads the list', () => {
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    fixture.detectChanges();

    api().confirmDelete(statuses[0]);

    expect(service.delete).toHaveBeenCalledWith(1);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('shows the API message when a delete is rejected (status still referenced)', () => {
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    service.delete.and.returnValue(
      throwError(() => ({ error: { message: '發布狀態「已發布」仍有 5 筆課程、2 筆促銷活動，無法刪除。' } }))
    );
    fixture.detectChanges();

    api().confirmDelete(statuses[1]);

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({
        severity: 'error',
        detail: '發布狀態「已發布」仍有 5 筆課程、2 筆促銷活動，無法刪除。'
      })
    );
  });

  it('navigates to the detail and edit pages', () => {
    const navigate = spyOn(router, 'navigate');
    fixture.detectChanges();

    api().view(statuses[0]);
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 1]);

    api().edit(statuses[1]);
    expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 2, 'edit']);
  });
});
