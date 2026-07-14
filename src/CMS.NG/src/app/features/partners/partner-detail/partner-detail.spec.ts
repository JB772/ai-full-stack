import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { PartnerDetail } from './partner-detail';
import { PartnerService } from '../partner.service';
import { Partner } from '../partner.model';

describe('PartnerDetail', () => {
  let fixture: ComponentFixture<PartnerDetail>;
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

  const unreferenced: Partner = {
    ...microsoft,
    courseCount: 0,
    certificationCount: 0,
    imageFilename: null
  };

  async function setup(partner: Partner | null = microsoft) {
    service = jasmine.createSpyObj<PartnerService>('PartnerService', ['getByPkid']);
    service.getByPkid.and.returnValue(partner ? of(partner) : throwError(() => new Error('404')));

    await TestBed.configureTestingModule({
      imports: [PartnerDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: PartnerService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['id', '1']]) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PartnerDetail);
    router = TestBed.inject(Router);
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  it('loads and renders the partner', async () => {
    await setup();
    fixture.detectChanges();

    expect(service.getByPkid).toHaveBeenCalledWith(1);
    expect(api().partner()).toEqual(microsoft);

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Microsoft');
    expect(text).toContain('MS');
    expect(text).toContain('微軟');
    expect(text).toContain('microsoft.png');
  });

  it('sums every child count into the reference total', async () => {
    await setup({ ...microsoft, courseGroupCount: 1, promotionCount: 2, seminarCount: 4 });
    fixture.detectChanges();

    // 12 courses + 3 certifications + 1 course group + 2 promotions + 4 seminars
    expect(api().referenceCount()).toBe(22);
  });

  it('notes that a referenced partner cannot be deleted', async () => {
    await setup();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('無法刪除');
  });

  it('counts a seminar-only reference as blocking, even though no FK enforces it', async () => {
    await setup({ ...unreferenced, seminarCount: 4 });
    fixture.detectChanges();

    expect(api().referenceCount()).toBe(4);
    expect(fixture.nativeElement.textContent).toContain('無法刪除');
  });

  it('omits the usage note when nothing references the partner', async () => {
    await setup(unreferenced);
    fixture.detectChanges();

    expect(api().referenceCount()).toBe(0);
    expect(fixture.nativeElement.textContent).not.toContain('無法刪除');
  });

  it('renders a dash when there is no image', async () => {
    await setup(unreferenced);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('—');
  });

  it('navigates back to the list when the partner is missing', async () => {
    await setup(null);
    const navigate = spyOn(TestBed.inject(Router), 'navigate');

    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/partners']);
    expect(api().loading()).toBeFalse();
  });

  it('navigates to the edit page', async () => {
    await setup();
    fixture.detectChanges();
    const navigate = spyOn(router, 'navigate');

    api().edit();

    expect(navigate).toHaveBeenCalledWith(['/partners', 1, 'edit']);
  });
});
