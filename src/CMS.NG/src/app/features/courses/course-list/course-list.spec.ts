import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, Confirmation } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { CourseList } from './course-list';
import { CourseService } from '../course.service';
import { Course } from '../course.model';
import { PartnerService } from '../../partners/partner.service';
import { CourseGroupService } from '../../course-groups/course-group.service';
import { PublishStatusService } from '../../publish-statuses/publish-status.service';
import { Partner } from '../../partners/partner.model';
import { CourseGroup } from '../../course-groups/course-group.model';
import { PublishStatus } from '../../publish-statuses/publish-status.model';

function makeCourse(overrides: Partial<Course>): Course {
  return {
    pkid: 1,
    title: 'Azure 基礎',
    officialTitle: null,
    courseId: 'AZ-900',
    prodCourseId: 'PROD-AZ900',
    friendlyUrl: 'azure-fundamentals',
    displayOrder: 1,
    partnerPkid: 1,
    courseGroupPkid: 1,
    publishStatusPkid: 2,
    scheduleOn: '2026-01-01',
    scheduleOff: '2036-01-01',
    hour: 12,
    listPrice: 8000,
    learningCredit: 3.5,
    material: null,
    objective: null,
    target: null,
    prerequisites: null,
    outline: null,
    towardCertOrExam: null,
    note: null,
    otherInfo: null,
    canRepeat: true,
    partner: { pkid: 1, name: 'Microsoft' },
    courseGroup: { pkid: 1, description: '雲端服務' },
    publishStatus: { pkid: 2, description: '已上架' },
    courseFaqCount: 0,
    certificationCount: 0,
    jobCategoryCount: 0,
    relatedLinkCount: 0,
    hotCourseCount: 0,
    recommCount: 0,
    ...overrides
  };
}

const AZURE = makeCourse({ pkid: 1, title: 'Azure 基礎', displayOrder: 1 });
const CCNA = makeCourse({
  pkid: 2,
  title: 'CCNA 認證課程',
  courseId: 'CCNA',
  displayOrder: 2,
  canRepeat: false,
  partner: { pkid: 2, name: 'Cisco' },
  courseGroup: null,
  courseGroupPkid: null,
  publishStatus: { pkid: 1, description: '草稿' }
});

describe('CourseList', () => {
  let fixture: ComponentFixture<CourseList>;
  let component: CourseList;
  let service: jasmine.SpyObj<CourseService>;
  let confirmationService: ConfirmationService;
  let router: Router;

  beforeEach(async () => {
    sessionStorage.clear();

    service = jasmine.createSpyObj<CourseService>('CourseService', ['query', 'delete']);
    // Fresh copies per call — p-table sorts its bound array in place, and the default sort is
    // displayOrder (not pkid), so a shared array reference would be reordered under the spec.
    service.query.and.callFake(() => of([makeCourse({ ...AZURE }), makeCourse({ ...CCNA })]));
    service.delete.and.returnValue(of(void 0));

    const partnerService = jasmine.createSpyObj<PartnerService>('PartnerService', ['getAll']);
    partnerService.getAll.and.returnValue(of([
      { pkid: 1, name: 'Microsoft' },
      { pkid: 2, name: 'Cisco' }
    ] as unknown as Partner[]));

    const courseGroupService = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['getAll']);
    courseGroupService.getAll.and.returnValue(of([
      { pkid: 1, description: '雲端服務' }
    ] as unknown as CourseGroup[]));

    const publishStatusService = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['getAll']);
    publishStatusService.getAll.and.returnValue(of([
      { pkid: 1, description: '草稿' },
      { pkid: 2, description: '已上架' }
    ] as unknown as PublishStatus[]));

    await TestBed.configureTestingModule({
      imports: [CourseList],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        ConfirmationService,
        { provide: CourseService, useValue: service },
        { provide: PartnerService, useValue: partnerService },
        { provide: CourseGroupService, useValue: courseGroupService },
        { provide: PublishStatusService, useValue: publishStatusService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseList);
    component = fixture.componentInstance;
    confirmationService = TestBed.inject(ConfirmationService);
    router = TestBed.inject(Router);
  });

  afterEach(() => sessionStorage.clear());

  /** The component's members are `protected`; they exist at runtime. */
  function api(): any {
    return component as any;
  }

  it('loads courses on init', () => {
    fixture.detectChanges();

    expect(service.query).toHaveBeenCalledTimes(1);
    expect(api().courses().length).toBe(2);
    expect(api().loading()).toBeFalse();
  });

  it('renders one row per course with resolved FK labels', () => {
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(2);

    const azureRow = [...rows].find(r => r.textContent.includes('Azure 基礎'));
    expect(azureRow.textContent).toContain('Microsoft');
    expect(azureRow.textContent).toContain('雲端服務');
    expect(azureRow.textContent).toContain('已上架');
  });

  it('renders a dash for a course with no course group', () => {
    fixture.detectChanges();

    const ccnaRow = [...fixture.nativeElement.querySelectorAll('tbody tr')]
      .find(r => r.textContent.includes('CCNA'));
    expect(ccnaRow.textContent).toContain('—');
  });

  it('defaults the sort to displayOrder ascending', () => {
    fixture.detectChanges();

    expect(api().sortField).toBe('displayOrder');
    expect(api().sortOrder).toBe(1);
  });

  it('populates the FK filter options from the lookup services', () => {
    fixture.detectChanges();

    expect(api().partnerOptions()).toEqual([
      { label: 'Microsoft', value: 1 },
      { label: 'Cisco', value: 2 }
    ]);
    expect(api().publishStatusOptions().length).toBe(2);
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

    api().filters.keyword = 'Azure';
    api().first = 20;
    api().applyFilters();

    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ keyword: 'Azure' }));
    expect(api().first).toBe(0);
    expect(JSON.parse(sessionStorage.getItem('course-list-filters')!).keyword).toBe('Azure');
    expect(api().filterDrawerVisible()).toBeFalse();
  });

  it('applies the FK filters', () => {
    fixture.detectChanges();

    api().filters.partnerPkid = 2;
    api().filters.publishStatusPkid = 1;
    api().applyFilters();

    expect(service.query).toHaveBeenCalledWith(
      jasmine.objectContaining({ partnerPkid: 2, publishStatusPkid: 1 })
    );
  });

  it('bridges the date-range pickers to yyyy-MM-dd query strings', () => {
    fixture.detectChanges();

    api().scheduleOnFromDate = new Date(2026, 0, 15);
    expect(api().filters.scheduleOnFrom).toBe('2026-01-15');
    expect(api().scheduleOnFromDate instanceof Date).toBeTrue();
  });

  it('treats a canRepeat filter of false as active (not the same as unset)', () => {
    fixture.detectChanges();

    api().filters.canRepeat = false;

    expect(api().hasActiveFilters).toBeTrue();
  });

  it('clears all filters', () => {
    fixture.detectChanges();

    api().filters.keyword = 'Azure';
    api().filters.partnerPkid = 1;
    api().clearFilters();

    expect(api().filters.keyword).toBeNull();
    expect(api().filters.partnerPkid).toBeNull();
    expect(api().hasActiveFilters).toBeFalse();
  });

  it('restores filters, sort and page from session storage on init', () => {
    sessionStorage.setItem('course-list-filters', JSON.stringify({ keyword: 'Java' }));
    sessionStorage.setItem('course-list-sort', JSON.stringify({ sortField: 'title', sortOrder: -1 }));
    sessionStorage.setItem('course-list-page', JSON.stringify({ first: 20, rows: 50 }));

    fixture = TestBed.createComponent(CourseList);
    fixture.detectChanges();

    const restored = fixture.componentInstance as any;
    expect(restored.filters.keyword).toBe('Java');
    expect(restored.sortField).toBe('title');
    expect(restored.sortOrder).toBe(-1);
    expect(restored.first).toBe(20);
    expect(service.query).toHaveBeenCalledWith(jasmine.objectContaining({ keyword: 'Java' }));
  });

  it('ignores corrupt session storage instead of throwing', () => {
    sessionStorage.setItem('course-list-sort', '{not json');

    fixture = TestBed.createComponent(CourseList);
    expect(() => fixture.detectChanges()).not.toThrow();
    expect((fixture.componentInstance as any).sortField).toBe('displayOrder');
  });

  it('deletes an unreferenced course after confirmation and reloads the list', () => {
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    fixture.detectChanges();

    api().confirmDelete(CCNA);

    expect(service.delete).toHaveBeenCalledWith(2);
    expect(service.query).toHaveBeenCalledTimes(2);
  });

  it('shows the API message when a delete is rejected (course still referenced)', () => {
    const messageService = TestBed.inject(MessageService);
    spyOn(messageService, 'add');
    spyOn(confirmationService, 'confirm').and.callFake((c: Confirmation) => {
      c.accept?.();
      return confirmationService;
    });
    service.delete.and.returnValue(
      throwError(() => ({ error: { message: '課程「Azure 基礎」仍有 1 筆問答、2 筆認證關聯，無法刪除。' } }))
    );
    fixture.detectChanges();

    api().confirmDelete(AZURE);

    expect(messageService.add).toHaveBeenCalledWith(
      jasmine.objectContaining({
        severity: 'error',
        detail: '課程「Azure 基礎」仍有 1 筆問答、2 筆認證關聯，無法刪除。'
      })
    );
  });

  it('navigates to the detail and edit pages', () => {
    const navigate = spyOn(router, 'navigate');
    fixture.detectChanges();

    api().view(AZURE);
    expect(navigate).toHaveBeenCalledWith(['/courses', 1]);

    api().edit(CCNA);
    expect(navigate).toHaveBeenCalledWith(['/courses', 2, 'edit']);
  });
});
