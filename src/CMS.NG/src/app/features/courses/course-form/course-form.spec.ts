import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { CourseForm } from './course-form';
import { CourseService } from '../course.service';
import { Course } from '../course.model';
import { PartnerService } from '../../partners/partner.service';
import { CourseGroupService } from '../../course-groups/course-group.service';
import { PublishStatusService } from '../../publish-statuses/publish-status.service';
import { Partner } from '../../partners/partner.model';
import { CourseGroup } from '../../course-groups/course-group.model';
import { PublishStatus } from '../../publish-statuses/publish-status.model';

function makeCourse(overrides: Partial<Course> = {}): Course {
  return {
    pkid: 1,
    title: 'Azure 基礎',
    officialTitle: 'Microsoft Azure Fundamentals',
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

describe('CourseForm', () => {
  let fixture: ComponentFixture<CourseForm>;
  let service: jasmine.SpyObj<CourseService>;
  let router: Router;

  const validValue = {
    title: '新課程',
    officialTitle: null as string | null,
    courseId: 'NEW-101',
    prodCourseId: 'PROD-NEW-101',
    friendlyUrl: 'new-101',
    displayOrder: 10,
    partnerPkid: 1,
    courseGroupPkid: null as number | null,
    publishStatusPkid: 2,
    scheduleOn: new Date(2026, 0, 1),
    scheduleOff: new Date(2036, 0, 1),
    hour: 8,
    listPrice: 5000,
    learningCredit: 2.5,
    material: null as string | null,
    objective: null as string | null,
    target: null as string | null,
    prerequisites: null as string | null,
    outline: null as string | null,
    towardCertOrExam: null as string | null,
    note: null as string | null,
    otherInfo: null as string | null,
    canRepeat: false
  };

  /** `id` = null → add mode; a value → edit mode. */
  async function setup(id: string | null, course: Course = makeCourse()) {
    service = jasmine.createSpyObj<CourseService>('CourseService', ['getByPkid', 'create', 'update']);
    service.getByPkid.and.returnValue(of(course));
    service.create.and.returnValue(of(makeCourse({ pkid: 4, title: '新課程' })));
    service.update.and.returnValue(of(void 0));

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
      imports: [CourseForm],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: CourseService, useValue: service },
        { provide: PartnerService, useValue: partnerService },
        { provide: CourseGroupService, useValue: courseGroupService },
        { provide: PublishStatusService, useValue: publishStatusService },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map(id ? [['id', id]] : []) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseForm);
    router = TestBed.inject(Router);
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  /** The action toolbar must stay pinned to the top of the form while the body scrolls,
      and keep Save/Cancel reachable, on both New and Edit. */
  function assertStickyToolbarWithActions(): void {
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;

    const toolbar = el.querySelector('.page-toolbar.sticky-toolbar') as HTMLElement;
    expect(toolbar).withContext('action toolbar with sticky styling').toBeTruthy();
    expect(getComputedStyle(toolbar).position).toBe('sticky');

    const actions = toolbar.querySelector('.page-actions') as HTMLElement;
    expect(actions.textContent).toContain('儲存');
    expect(actions.textContent).toContain('取消');
  }

  describe('add mode', () => {
    beforeEach(async () => await setup(null));

    it('starts blank and never asks the API for a record', () => {
      fixture.detectChanges();

      expect(api().isEdit()).toBeFalse();
      expect(service.getByPkid).not.toHaveBeenCalled();
    });

    it('pins the action toolbar and keeps Save/Cancel present', () => {
      assertStickyToolbarWithActions();
    });

    it('has no pkid control — the key is IDENTITY-generated', () => {
      fixture.detectChanges();

      expect(api().form.controls['pkid']).toBeUndefined();
    });

    it('populates FK dropdown options from the lookup services', () => {
      fixture.detectChanges();

      expect(api().partnerOptions().length).toBe(2);
      expect(api().courseGroupOptions()).toEqual([{ label: '雲端服務', value: 1 }]);
      expect(api().publishStatusOptions().length).toBe(2);
    });

    it('does not submit an invalid form', () => {
      fixture.detectChanges();

      api().save();

      expect(service.create).not.toHaveBeenCalled();
      expect(api().form.controls.title.touched).toBeTrue();
      expect(api().form.controls.partnerPkid.touched).toBeTrue();
    });

    it('creates the course with pkid 0, iso dates and a null course group', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().form.setValue(validValue);
      api().save();

      expect(service.create).toHaveBeenCalledWith(jasmine.objectContaining({
        pkid: 0,
        title: '新課程',
        partnerPkid: 1,
        courseGroupPkid: null,
        publishStatusPkid: 2,
        scheduleOn: '2026-01-01',
        scheduleOff: '2036-01-01',
        canRepeat: false
      }));
      // The API's generated pkid, not anything the form supplied.
      expect(navigate).toHaveBeenCalledWith(['/courses', 4]);
    });

    it('sends a blank optional text field as null so the column stays NULL', () => {
      fixture.detectChanges();

      api().form.setValue({ ...validValue, material: '   ' });
      api().save();

      expect(service.create).toHaveBeenCalledWith(jasmine.objectContaining({ material: null }));
    });

    it('trims whitespace off the text fields', () => {
      fixture.detectChanges();

      api().form.setValue({ ...validValue, title: '  新課程  ', courseId: ' NEW-101 ' });
      api().save();

      expect(service.create).toHaveBeenCalledWith(
        jasmine.objectContaining({ title: '新課程', courseId: 'NEW-101' })
      );
    });

    it('surfaces a save failure from the API', () => {
      fixture.detectChanges();
      const messageService = TestBed.inject(MessageService);
      spyOn(messageService, 'add');
      service.create.and.returnValue(throwError(() => ({ error: { message: '儲存失敗。' } })));

      api().form.setValue(validValue);
      api().save();

      expect(messageService.add).toHaveBeenCalledWith(
        jasmine.objectContaining({ severity: 'error', detail: '儲存失敗。' })
      );
      expect(api().saving()).toBeFalse();
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => await setup('1'));

    it('pins the action toolbar and keeps Save/Cancel present', () => {
      assertStickyToolbarWithActions();
    });

    it('loads the course into the form, parsing iso dates to Date objects', () => {
      fixture.detectChanges();

      expect(api().isEdit()).toBeTrue();
      expect(service.getByPkid).toHaveBeenCalledWith(1);
      expect(api().form.controls.title.value).toBe('Azure 基礎');
      expect(api().form.controls.partnerPkid.value).toBe(1);
      expect(api().form.controls.scheduleOn.value instanceof Date).toBeTrue();
      expect(api().form.controls.scheduleOn.value.getFullYear()).toBe(2026);
    });

    it('updates with the pkid in the request and navigates to the detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().form.controls.title.setValue('Azure 進階');
      api().save();

      expect(service.update).toHaveBeenCalledWith(jasmine.objectContaining({
        pkid: 1,
        title: 'Azure 進階',
        scheduleOn: '2026-01-01'
      }));
      expect(navigate).toHaveBeenCalledWith(['/courses', 1]);
    });

    it('can reassign the course group to null', () => {
      fixture.detectChanges();

      api().form.controls.courseGroupPkid.setValue(null);
      api().save();

      expect(service.update).toHaveBeenCalledWith(jasmine.objectContaining({ courseGroupPkid: null }));
    });

    it('cancels back to the detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().cancel();

      expect(navigate).toHaveBeenCalledWith(['/courses', 1]);
    });

    it('navigates back to the list when the course is missing', () => {
      service.getByPkid.and.returnValue(throwError(() => new Error('404')));
      const navigate = spyOn(router, 'navigate');

      fixture.detectChanges();

      expect(navigate).toHaveBeenCalledWith(['/courses']);
    });
  });
});
