import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { CourseGroupForm } from './course-group-form';
import { CourseGroupService } from '../course-group.service';
import { CourseGroup } from '../course-group.model';
import { RowAuditService } from '../../row-audit/row-audit.service';

describe('CourseGroupForm', () => {
  let fixture: ComponentFixture<CourseGroupForm>;
  let service: jasmine.SpyObj<CourseGroupService>;
  let router: Router;

  const database: CourseGroup = {
    pkid: 2,
    description: '資料庫',
    courseCount: 12,
    partnerCourseGroupCount: 3
  };

  /** `id` = null → add mode; a value → edit mode. */
  async function setup(id: string | null) {
    service = jasmine.createSpyObj<CourseGroupService>('CourseGroupService', ['getByPkid', 'create', 'update']);
    service.getByPkid.and.returnValue(of(database));
    service.create.and.returnValue(of({ ...database, pkid: 4, description: '人工智慧', courseCount: 0, partnerCourseGroupCount: 0 }));
    service.update.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [CourseGroupForm],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: CourseGroupService, useValue: service },
        { provide: RowAuditService, useValue: { getForRecord: () => of([]) } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map(id ? [['id', id]] : []) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CourseGroupForm);
    router = TestBed.inject(Router);
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  describe('add mode', () => {
    beforeEach(async () => await setup(null));

    it('starts blank and never asks for a pkid', () => {
      fixture.detectChanges();

      expect(api().isEdit()).toBeFalse();
      expect(service.getByPkid).not.toHaveBeenCalled();
      // pkid is a smallint IDENTITY — unknown until the DB assigns it, so it has no form control.
      expect(api().form.contains('pkid')).toBeFalse();
      expect(api().form.getRawValue()).toEqual({ description: '' });
    });

    it('does not submit an empty description', () => {
      fixture.detectChanges();

      api().save();

      expect(service.create).not.toHaveBeenCalled();
      expect(api().form.controls.description.touched).toBeTrue();
    });

    it('rejects a description longer than 100 characters', () => {
      fixture.detectChanges();

      api().form.controls.description.setValue('雲'.repeat(101));
      api().save();

      expect(api().form.controls.description.invalid).toBeTrue();
      expect(service.create).not.toHaveBeenCalled();
    });

    it('creates the group with pkid 0 and navigates to the generated detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().form.controls.description.setValue('人工智慧');
      api().save();

      // pkid 0 tells the API "generate one" — the response carries the real key.
      expect(service.create).toHaveBeenCalledWith({ pkid: 0, description: '人工智慧' });
      expect(navigate).toHaveBeenCalledWith(['/course-groups', 4]);
    });

    it('trims the description before sending it', () => {
      fixture.detectChanges();

      api().form.controls.description.setValue('  人工智慧  ');
      api().save();

      expect(service.create).toHaveBeenCalledWith({ pkid: 0, description: '人工智慧' });
    });

    it('surfaces a save failure from the API', () => {
      fixture.detectChanges();
      const messageService = TestBed.inject(MessageService);
      spyOn(messageService, 'add');
      service.create.and.returnValue(throwError(() => ({ error: { message: '儲存失敗。' } })));

      api().form.controls.description.setValue('人工智慧');
      api().save();

      expect(messageService.add).toHaveBeenCalledWith(
        jasmine.objectContaining({ severity: 'error', detail: '儲存失敗。' })
      );
      expect(api().saving()).toBeFalse();
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => await setup('2'));

    it('loads the group and shows the pkid read-only', () => {
      fixture.detectChanges();

      expect(api().isEdit()).toBeTrue();
      expect(service.getByPkid).toHaveBeenCalledWith(2);
      expect(api().pkidDisplay).toBe(2);
      expect(api().form.getRawValue()).toEqual({ description: '資料庫' });
      expect(fixture.nativeElement.textContent).toContain('不可修改');
    });

    it('updates the group with the pkid and navigates to its detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().form.controls.description.setValue('資料庫管理');
      api().save();

      expect(service.update).toHaveBeenCalledWith({ pkid: 2, description: '資料庫管理' });
      expect(navigate).toHaveBeenCalledWith(['/course-groups', 2]);
    });

    it('cancels back to the detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().cancel();

      expect(navigate).toHaveBeenCalledWith(['/course-groups', 2]);
    });
  });
});
