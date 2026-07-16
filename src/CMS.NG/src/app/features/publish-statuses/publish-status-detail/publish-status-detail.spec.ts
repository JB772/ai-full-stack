import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { of, throwError } from 'rxjs';
import { PublishStatusDetail } from './publish-status-detail';
import { PublishStatusService } from '../publish-status.service';
import { PublishStatus } from '../publish-status.model';
import { RowAuditService } from '../../row-audit/row-audit.service';

describe('PublishStatusDetail', () => {
  let fixture: ComponentFixture<PublishStatusDetail>;
  let service: jasmine.SpyObj<PublishStatusService>;
  let router: Router;

  const published: PublishStatus = {
    pkid: 2,
    description: '已發布',
    isDraft: false,
    isPublished: true,
    isDiscontinued: false,
    courseCount: 5,
    promotionCount: 2
  };

  async function setup(status: PublishStatus | null = published) {
    service = jasmine.createSpyObj<PublishStatusService>('PublishStatusService', ['getByPkid']);
    service.getByPkid.and.returnValue(status ? of(status) : throwError(() => new Error('404')));

    await TestBed.configureTestingModule({
      imports: [PublishStatusDetail],
      providers: [
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        { provide: PublishStatusService, useValue: service },
        { provide: RowAuditService, useValue: { getForRecord: () => of([]) } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: new Map([['id', '2']]) } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PublishStatusDetail);
    router = TestBed.inject(Router);
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  it('loads and renders the status', async () => {
    await setup();
    fixture.detectChanges();

    expect(service.getByPkid).toHaveBeenCalledWith(2);
    expect(api().status()).toEqual(published);

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('已發布');
    expect(text).toContain('5');
  });

  it('notes that a referenced status cannot be deleted', async () => {
    await setup();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('無法刪除');
  });

  it('omits the usage note when nothing references the status', async () => {
    await setup({ ...published, courseCount: 0, promotionCount: 0 });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('無法刪除');
  });

  it('navigates back to the list when the status is missing', async () => {
    await setup(null);
    const navigate = spyOn(TestBed.inject(Router), 'navigate');

    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/publish-statuses']);
    expect(api().loading()).toBeFalse();
  });

  it('navigates to the edit page', async () => {
    await setup();
    fixture.detectChanges();
    const navigate = spyOn(router, 'navigate');

    api().edit();

    expect(navigate).toHaveBeenCalledWith(['/publish-statuses', 2, 'edit']);
  });
});
