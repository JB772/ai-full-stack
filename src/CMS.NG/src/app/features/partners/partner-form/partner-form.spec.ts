import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { PartnerForm } from './partner-form';
import { PartnerService } from '../partner.service';
import { Partner } from '../partner.model';
import { RowAuditService } from '../../row-audit/row-audit.service';

describe('PartnerForm', () => {
  let fixture: ComponentFixture<PartnerForm>;
  let service: jasmine.SpyObj<PartnerService>;
  let router: Router;

  const microsoft: Partner = {
    pkid: 1,
    name: 'Microsoft',
    appKey: 'MS',
    nameOnPartnerMenu: 'Microsoft 微軟',
    nameOnCourseDetailPage: '微軟',
    displayOrder: 1,
    imageFilename: 'microsoft.png',
    courseCount: 12,
    certificationCount: 3,
    courseGroupCount: 0,
    promotionCount: 0,
    seminarCount: 0
  };

  const validValue = {
    name: 'AWS',
    appKey: 'AWS',
    nameOnPartnerMenu: 'AWS 選單名稱',
    nameOnCourseDetailPage: 'AWS',
    displayOrder: 10,
    imageFilename: 'aws.png'
  };

  /** `id` = null → add mode; a value → edit mode. */
  async function setup(id: string | null) {
    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['getByPkid', 'create', 'update']);
    service.getByPkid.and.returnValue(of(microsoft));
    service.create.and.returnValue(of({ ...microsoft, pkid: 4, name: 'AWS' }));
    service.update.and.returnValue(of(void 0));

    await TestBed.configureTestingModule({
      imports: [PartnerForm],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: PartnerService, useValue: service },
        { provide: RowAuditService, useValue: { getForRecord: () => of([]) } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map(id ? [['id', id]] : []) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerForm);
    router = TestBed.inject(Router);
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  describe('add mode', () => {
    beforeEach(async () => await setup(null));

    it('starts blank and never asks the API for a record', () => {
      fixture.detectChanges();

      expect(api().isEdit()).toBeFalse();
      expect(service.getByPkid).not.toHaveBeenCalled();
    });

    it('has no pkid control — the key is IDENTITY-generated', () => {
      fixture.detectChanges();

      expect(api().form.controls['pkid']).toBeUndefined();
    });

    it('does not submit an invalid form', () => {
      fixture.detectChanges();

      api().save();

      expect(service.create).not.toHaveBeenCalled();
      expect(api().form.controls.name.touched).toBeTrue();
      expect(api().form.controls.appKey.touched).toBeTrue();
    });

    it('rejects an appKey longer than 10 characters', () => {
      fixture.detectChanges();

      api().form.setValue({ ...validValue, appKey: 'TOOLONGAPPKEY' });
      api().save();

      expect(api().form.controls.appKey.invalid).toBeTrue();
      expect(service.create).not.toHaveBeenCalled();
    });

    it('creates the partner with pkid 0 and navigates to the generated detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().form.setValue(validValue);
      api().save();

      expect(service.create).toHaveBeenCalledWith({
        pkid: 0,
        name: 'AWS',
        appKey: 'AWS',
        nameOnPartnerMenu: 'AWS 選單名稱',
        nameOnCourseDetailPage: 'AWS',
        displayOrder: 10,
        imageFilename: 'aws.png'
      });
      // The API's generated pkid, not anything the form supplied.
      expect(navigate).toHaveBeenCalledWith(['/partners', 4]);
    });

    it('sends a blank imageFilename as null so the column stays NULL', () => {
      fixture.detectChanges();

      api().form.setValue({ ...validValue, imageFilename: '   ' });
      api().save();

      expect(service.create).toHaveBeenCalledWith(
        jasmine.objectContaining({ imageFilename: null })
      );
    });

    it('trims whitespace off the text fields', () => {
      fixture.detectChanges();

      api().form.setValue({ ...validValue, name: '  AWS  ', appKey: ' AWS ' });
      api().save();

      expect(service.create).toHaveBeenCalledWith(
        jasmine.objectContaining({ name: 'AWS', appKey: 'AWS' })
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

    it('loads the partner into the form', () => {
      fixture.detectChanges();

      expect(api().isEdit()).toBeTrue();
      expect(service.getByPkid).toHaveBeenCalledWith(1);
      expect(api().form.getRawValue()).toEqual({
        name: 'Microsoft',
        appKey: 'MS',
        nameOnPartnerMenu: 'Microsoft 微軟',
        nameOnCourseDetailPage: '微軟',
        displayOrder: 1,
        imageFilename: 'microsoft.png'
      });
    });

    it('updates with the pkid in the request and navigates to the detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().form.controls.name.setValue('Microsoft 微軟');
      api().save();

      expect(service.update).toHaveBeenCalledWith({
        pkid: 1,
        name: 'Microsoft 微軟',
        appKey: 'MS',
        nameOnPartnerMenu: 'Microsoft 微軟',
        nameOnCourseDetailPage: '微軟',
        displayOrder: 1,
        imageFilename: 'microsoft.png'
      });
      expect(navigate).toHaveBeenCalledWith(['/partners', 1]);
    });

    it('allows the appKey to be changed (nothing foreign-keys to it)', () => {
      fixture.detectChanges();

      expect(api().form.controls.appKey.enabled).toBeTrue();

      api().form.controls.appKey.setValue('MSFT');
      api().save();

      expect(service.update).toHaveBeenCalledWith(jasmine.objectContaining({ appKey: 'MSFT' }));
    });

    it('can clear the image filename', () => {
      fixture.detectChanges();

      api().form.controls.imageFilename.setValue('');
      api().save();

      expect(service.update).toHaveBeenCalledWith(jasmine.objectContaining({ imageFilename: null }));
    });

    it('cancels back to the detail page', () => {
      fixture.detectChanges();
      const navigate = spyOn(router, 'navigate');

      api().cancel();

      expect(navigate).toHaveBeenCalledWith(['/partners', 1]);
    });

    it('navigates back to the list when the partner is missing', async () => {
      service.getByPkid.and.returnValue(throwError(() => new Error('404')));
      const navigate = spyOn(router, 'navigate');

      fixture.detectChanges();

      expect(navigate).toHaveBeenCalledWith(['/partners']);
    });
  });
});
