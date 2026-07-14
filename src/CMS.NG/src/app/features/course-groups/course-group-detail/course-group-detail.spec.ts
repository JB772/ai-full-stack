import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { CourseGroupDetail } from './course-group-detail';
import { CourseGroupService } from '../course-group.service';
import { CourseGroup } from '../course-group.model';

describe('CourseGroupDetail', () => {
  let fixture: ComponentFixture<CourseGroupDetail>;
  let service: jasmine.SpyObj<CourseGroupService>;
  let router: Router;

  const database: CourseGroup = {
    pkid: 2,
    description: '資料庫',
    courseCount: 12,
    partnerCourseGroupCount: 3
  };

  async function setup(group: CourseGroup | null = database) {
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['getByPkid']);
    service.getByPkid.and.returnValue(group ? of(group) : throwError(() => new Error('404')));

    await TestBed.configureTestingModule({
      imports: [CourseGroupDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: CourseGroupService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['id', '2']]) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseGroupDetail);
    router = TestBed.inject(Router);
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  it('loads and renders the group with both counts', async () => {
    await setup();
    fixture.detectChanges();

    expect(service.getByPkid).toHaveBeenCalledWith(2);
    expect(api().group()).toEqual(database);

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('資料庫');
    expect(text).toContain('12');
    expect(text).toContain('3');
  });

  it('notes that a referenced group cannot be deleted', async () => {
    await setup();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('無法刪除');
  });

  it('shows the usage note when only PartnerCourseGroup rows reference the group', async () => {
    await setup({ ...database, courseCount: 0 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('無法刪除');
  });

  it('omits the usage note when nothing references the group', async () => {
    await setup({ ...database, courseCount: 0, partnerCourseGroupCount: 0 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('無法刪除');
  });

  it('navigates back to the list when the group is missing', async () => {
    await setup(null);
    const navigate = spyOn(TestBed.inject(Router), 'navigate');

    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/course-groups']);
    expect(api().loading()).toBeFalse();
  });

  it('navigates to the edit page', async () => {
    await setup();
    fixture.detectChanges();
    const navigate = spyOn(router, 'navigate');

    api().edit();

    expect(navigate).toHaveBeenCalledWith(['/course-groups', 2, 'edit']);
  });
});
