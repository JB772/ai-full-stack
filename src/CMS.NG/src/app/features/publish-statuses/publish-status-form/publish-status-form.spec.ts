import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { PublishStatusForm } from './publish-status-form';
import { PublishStatusService } from '../publish-status.service';
import { PublishStatus } from '../publish-status.model';

describe('PublishStatusForm', () => {
  let fixture: ComponentFixture<PublishStatusForm>;
  let service: jasmine.SpyObj<PublishStatusService>;
  let router: Router;

  const discontinued: PublishStatus = {
    pkid: 3,
    description: '已下架',
    isDraft: false,
    isPublished: false,
    isDiscontinued: true,
    courseCount: 0,
    promotionCount: 0
  };

  /** `id` = null → add mode; a value → edit mode. */
  async function setup(id: string | null) {
    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['getByPkid', 'create', 'update']);
    service.getByPkid.and.returnValue(of(discontinued));
    service.create.and.returnValue(of({ ...discontinued, pkid: 10, description: '審核中' }));
    service.update.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PublishStatusForm],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: PublishStatusService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map(id ? [['id', id]] : []) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusForm);
    router = TestBed.inject(Router);
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  describe('add mode', () => {
    beforeEach(async () => await setup(null));

    it('starts blank with an editable pkid', () => {
      fixture.detectChanges();

      expect(api().isEdit()).toBeFalse();
      expect(service.getByPkid).not.toHaveBeenCalled();
      // pkid is caller-supplied on this table, so it must be enterable when adding.
      expect(api().form.controls.pkid.enabled).toBeTrue();
    });

    it('does not submit an invalid form', () => {
      fixture.detectChanges();

      api().save();

      expect(service.create).not.toHaveBeenCalled();
      expect(api().form.controls.description.touched).toBeTrue();
    });

    it('rejects a pkid beyond the tinyint range', () => {
      fixture.detectChanges();

      api().form.setValue({
        pkid: 300,
        description: '超出範圍',
        isDraft: false,
        isPublished: false,
        isDiscontinued: false
      });
      api().save();

      expect(api().form.controls.pkid.invalid).toBeTrue();
      expect(service.create).not.toHaveBeenCalled();
    });

    it('creates the status with the entered pkid and navigates to its detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().form.setValue({
        pkid: 10,
        description: '審核中',
        isDraft: true,
        isPublished: false,
        isDiscontinued: false
      });
      api().save();

      expect(service.create).toHaveBeenCalledWith({
        pkid: 10,
        description: '審核中',
        isDraft: true,
        isPublished: false,
        isDiscontinued: false
      });
      expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 10]);
    });

    it('allows any combination of the three flags (no CHECK constraint in the schema)', () => {
      fixture.detectChanges();

      api().form.setValue({
        pkid: 11,
        description: '全部皆是',
        isDraft: true,
        isPublished: true,
        isDiscontinued: true
      });
      api().save();

      expect(api().form.valid).toBeTrue();
      expect(service.create).toHaveBeenCalledWith(
        jasmine.objectContaining({ isDraft: true, isPublished: true, isDiscontinued: true })
      );
    });

    it('reports a duplicate pkid conflict from the API', () => {
      fixture.detectChanges();
      const messageService = TestBed.inject(MessageService);
      spyOn(messageService, 'add');
      service.create.and.returnValue(throwError(() => ({ error: { message: '主代碼「1」已存在。' } })));

      api().form.setValue({
        pkid: 1,
        description: '重複',
        isDraft: false,
        isPublished: false,
        isDiscontinued: false
      });
      api().save();

      expect(messageService.add).toHaveBeenCalledWith(
        jasmine.objectContaining({ severity: 'error', detail: '主代碼「1」已存在。' })
      );
      expect(api().saving()).toBeFalse();
    });
  });

  describe('edit mode', () => {
    beforeEach(async () => await setup('3'));

    it('loads the status and locks the pkid', () => {
      fixture.detectChanges();

      expect(api().isEdit()).toBeTrue();
      expect(service.getByPkid).toHaveBeenCalledWith(3);
      expect(api().form.controls.pkid.disabled).toBeTrue();
      expect(api().form.getRawValue()).toEqual({
        pkid: 3,
        description: '已下架',
        isDraft: false,
        isPublished: false,
        isDiscontinued: true
      });
    });

    it('updates the status with the pkid and navigates to its detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().form.controls.description.setValue('已封存');
      api().save();

      expect(service.update).toHaveBeenCalledWith({
        pkid: 3,
        description: '已封存',
        isDraft: false,
        isPublished: false,
        isDiscontinued: true
      });
      expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 3]);
    });

    it('cancels back to the detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().cancel();

      expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 3]);
    });
  });
});
