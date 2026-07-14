import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { PartnerList } from './partner-list';
import { PartnerService } from '../partner.service';
import { Partner } from '../partner.model';

describe('PartnerList', () => {
  let fixture: ComponentFixture<PartnerList>;
  let component: PartnerList;
  let service: jasmine.SpyObj<PartnerService>;
  let confirmationService: ConfirmationService;
  let router: Router;

  const partners: Partner[] = [
    {
      pkid: 1, name: 'Microsoft', appKey: 'MS',
      nameOnPartnerMenu: 'Microsoft 微軟', nameOnCourseDetailPage: '微軟',
      displayOrder: 1, imageFilename: 'microsoft.png',
      courseCount: 12, certificationCount: 3, courseGroupCount: 0, promotionCount: 0, seminarCount: 0
    },
    {
      pkid: 2, name: 'Cisco', appKey: 'CSCO',
      nameOnPartnerMenu: 'Cisco 思科', nameOnCourseDetailPage: '思科',
      displayOrder: 2, imageFilename: null,
      courseCount: 0, certificationCount: 0, courseGroupCount: 0, promotionCount: 0, seminarCount: 4
    }
  ];

  beforeEach(async () => {
    sessionStorage.clear();

    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['query', 'delete']);
    service.query.and.returnValue(of(partners));
    service.delete.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PartnerList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: PartnerService, useValue: service }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerList);
    component = fixture.componentInstance;
    confirmationService = TestBed.inject(ConfirmationService);
    router = TestBed.inject(Router);
  });

  /** The component's members are `protected`, which TS blocks from outside the class but exists at runtime. */
  function api(): any {
    return component as any;
  }

  afterEach(() => sessionStorage.clear());

  it('loads partners on init', () => {
    fixture.detectChanges();

    expect(service.query).toHaveBeenCalledTimes(1);
    expect(api().partners()).toEqual(partners);
    expect(api().loading()).toBeFalse();
  });

  it('renders one row per partner', () => {
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Microsoft');
    expect(rows[1].textContent).toContain('Cisco');
  });

  it('renders a dash for a partner with no image', () => {
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows[1].textContent).toContain('—');
  });

  it('defaults the sort to displayOrder ascending', () => {
    fixture.detectChanges();

    expect(api().sortField).toBe('displayOrder');
    expect(api().sortOrder).toBe(1);
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

    api().filters.keyword = 'Cisco';
    api().first = 20;
    api().applyFilters();

    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ keyword: 'Cisco' }));
    expect(api().first).toBe(0);
    expect(JSON.parse(sessionStorage.getItem('partner-list-filters')!).keyword).toBe('Cisco');
    expect(api().filterDrawerVisible()).toBeFalse();
  });

  it('applies the display-order range filter', () => {
    fixture.detectChanges();

    api().filters.displayOrderFrom = 2;
    api().filters.displayOrderTo = 5;
    api().applyFilters();

    expect(service.query).toHaveBeenCalledWith(
      jasmine.objectContaining({ displayOrderFrom: 2, displayOrderTo: 5 })
    );
  });

  it('applies the tri-state hasImage filter', () => {
    fixture.detectChanges();

    api().filters.hasImage = false;
    api().applyFilters();

    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ hasImage: false }));
  });

  it('treats a false hasImage filter as active (not the same as unset)', () => {
    fixture.detectChanges();

    api().filters.hasImage = false;

    expect(api().hasActiveFilters).toBeTrue();
  });

  it('treats a displayOrderFrom of 0 as active (not the same as unset)', () => {
    fixture.detectChanges();

    api().filters.displayOrderFrom = 0;

    expect(api().hasActiveFilters).toBeTrue();
  });

  it('clears all filters', () => {
    fixture.detectChanges();

    api().filters.keyword = 'Cisco';
    api().filters.hasImage = true;
    api().clearFilters();

    expect(api().filters).toEqual({
      keyword: null,
      displayOrderFrom: null,
      displayOrderTo: null,
      hasImage: null
    });
    expect(api().hasActiveFilters).toBeFalse();
  });

  it('restores filters, sort and page from session storage on init', () => {
    sessionStorage.setItem('partner-list-filters', JSON.stringify({ keyword: 'Microsoft' }));
    sessionStorage.setItem('partner-list-sort', JSON.stringify({ sortField: 'name', sortOrder: -1 }));
    sessionStorage.setItem('partner-list-page', JSON.stringify({ first: 20, rows: 50 }));

    fixture = TestBed.createComponent(PartnerList);
    fixture.detectChanges();

    const restored = fixture.componentInstance as any;
    expect(restored.filters.keyword).toBe('Microsoft');
    expect(restored.sortField).toBe('name');
    expect(restored.sortOrder).toBe(-1);
    expect(restored.first).toBe(20);
    expect(restored.rows).toBe(50);
    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ keyword: 'Microsoft' }));
  });

  it('ignores corrupt session storage instead of throwing', () => {
    sessionStorage.setItem('partner-list-sort', '{not json');

    fixture = TestBed.createComponent(PartnerList);
    expect(() => fixture.detectChanges()).not.toThrow();
    expect((fixture.componentInstance as any).sortField).toBe('displayOrder');
  });

  it('persists sort state', () => {
    fixture.detectChanges();

    api().onSort({ field: 'name', order: -1 });

    expect(JSON.parse(sessionStorage.getItem('partner-list-sort')!)).toEqual({
      sortField: 'name',
      sortOrder: -1
    });
  });

  it('persists page state', () => {
    fixture.detectChanges();

    api().onPage({ first: 40, rows: 10 });

    expect(JSON.parse(sessionStorage.getItem('partner-list-page')!)).toEqual({ first: 40, rows: 10 });
  });

  it('deletes an unreferenced partner after confirmation and reloads the list', () => {
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    fixture.detectChanges();

    api().confirmDelete(partners[1]);

    expect(service.delete).toHaveBeenCalledWith(2);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('shows the API message when a delete is rejected (partner still referenced)', () => {
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    service.delete.and.returnValue(
      throwError(() => ({ error: { message: '合作夥伴「Microsoft」仍有 12 筆課程、3 筆認證，無法刪除。' } }))
    );
    fixture.detectChanges();

    api().confirmDelete(partners[0]);

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({
        severity: 'error',
        detail: '合作夥伴「Microsoft」仍有 12 筆課程、3 筆認證，無法刪除。'
      })
    );
  });

  it('navigates to the detail and edit pages', () => {
    const navigate = spyOn(router, 'navigate');
    fixture.detectChanges();

    api().view(partners[0]);
    expect(navigate).toHaveBeenCalledWith(['/partners', 1]);

    api().edit(partners[1]);
    expect(navigate).toHaveBeenCalledWith(['/partners', 2, 'edit']);
  });
});
