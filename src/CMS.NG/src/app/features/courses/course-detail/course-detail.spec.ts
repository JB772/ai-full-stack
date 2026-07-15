import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { CourseDetail } from './course-detail';
import { CourseService } from '../course.service';
import { Course } from '../course.model';

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
    courseFaqCount: 1,
    certificationCount: 2,
    jobCategoryCount: 0,
    relatedLinkCount: 0,
    hotCourseCount: 0,
    recommCount: 0,
    ...overrides
  };
}

describe('CourseDetail', () => {
  let fixture: ComponentFixture<CourseDetail>;
  let service: jasmine.SpyObj<CourseService>;
  let router: Router;

  async function setup(course: Course = makeCourse()) {
    service = jasmine.createSpyObj<CourseService>('CourseService', ['getByPkid']);
    service.getByPkid.and.returnValue(of(course));

    await TestBed.configureTestingModule({
      imports: [CourseDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: CourseService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['id', '1']]) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseDetail);
    router = TestBed.inject(Router);
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  it('loads the course and renders its FK labels', async () => {
    await setup();
    fixture.detectChanges();

    expect(service.getByPkid).toHaveBeenCalledWith(1);
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Azure 基礎');
    expect(text).toContain('Microsoft');
    expect(text).toContain('雲端服務');
    expect(text).toContain('已上架');
  });

  it('sums the child counts into a reference total', async () => {
    await setup();
    fixture.detectChanges();

    // 1 FAQ + 2 certifications = 3.
    expect(api().referenceCount()).toBe(3);
    expect(fixture.nativeElement.textContent).toContain('無法刪除');
  });

  it('shows a dash and zero reference count for a course with no children or group', async () => {
    await setup(makeCourse({
      courseGroup: null,
      courseGroupPkid: null,
      courseFaqCount: 0,
      certificationCount: 0
    }));
    fixture.detectChanges();

    expect(api().referenceCount()).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('—');
    expect(fixture.nativeElement.textContent).not.toContain('無法刪除');
  });

  it('navigates to the edit page', async () => {
    await setup();
    fixture.detectChanges();
    const navigate = spyOn(router, 'navigate');

    api().edit();

    expect(navigate).toHaveBeenCalledWith(['/courses', 1, 'edit']);
  });

  it('returns to the list when the course is missing', async () => {
    await setup();
    service.getByPkid.and.returnValue(throwError(() => new Error('404')));
    const navigate = spyOn(router, 'navigate');

    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/courses']);
  });
});
