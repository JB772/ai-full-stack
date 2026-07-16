import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { RowAuditBadgeComponent } from './row-audit-badge';
import { RowAuditService } from '../row-audit.service';
import { RowAuditEntry } from '../row-audit.model';

describe('RowAuditBadgeComponent', () => {
  let fixture: ComponentFixture<RowAuditBadgeComponent>;
  let service: jasmine.SpyObj<RowAuditService>;

  const trail: RowAuditEntry[] = [
    // Newest first — the API (and the service) already return them in this order.
    { dateTime: '2026-06-04T14:30:00', userName: 'alice', actionType: 'Update', actionDesc: 'Title, ListPrice' },
    { dateTime: '2026-03-20T11:15:00', userName: 'bob', actionType: 'Update', actionDesc: 'PublishStatus_pkid' },
    { dateTime: '2026-01-10T09:00:00', userName: 'system', actionType: 'Insert', actionDesc: '初版課程' }
  ];

  async function setup(entries: RowAuditEntry[] = trail, tableName = 'Course', pkid = 123) {
    service = jasmine.createSpyObj<RowAuditService>('RowAuditService', ['getForRecord']);
    service.getForRecord.and.returnValue(of(entries));

    await TestBed.configureTestingModule({
      imports: [RowAuditBadgeComponent],
      providers: [provideNoopAnimations(), { provide: RowAuditService, useValue: service }]
    }).compileComponents();

    fixture = TestBed.createComponent(RowAuditBadgeComponent);
    fixture.componentRef.setInput('tableName', tableName);
    fixture.componentRef.setInput('pkid', pkid);
    fixture.detectChanges(); // runs the effect → fetch (synchronous `of`) → entries set
    fixture.detectChanges(); // renders the resolved state
  }

  function api(): any {
    return fixture.componentInstance as any;
  }

  it('fetches this record’s history on load', async () => {
    await setup();
    expect(service.getForRecord).toHaveBeenCalledWith('Course', 123);
  });

  it('shows the latest record inline on the badge', async () => {
    await setup();

    const badge = fixture.nativeElement.querySelector('.row-audit-badge');
    expect(badge).toBeTruthy();
    const text = badge.textContent as string;
    // Latest = the first (newest) entry: Update by alice on 2026-06-04.
    expect(text).toContain('異動紀錄 History');
    expect(text).toContain('Update');
    expect(text).toContain('alice');
    expect(text).toContain('2026-06-04 14:30');
    // It must show the *latest*, not an older entry.
    expect(text).not.toContain('bob');
  });

  it('opens the dialog listing the full audit trail, newest first', async () => {
    await setup();

    expect(api().dialogVisible()).toBeFalse();
    fixture.nativeElement.querySelector('.row-audit-badge').click();
    fixture.detectChanges();

    expect(api().dialogVisible()).toBeTrue();

    const rows = fixture.nativeElement.querySelectorAll('.row-audit-table tbody tr');
    expect(rows.length).toBe(3);
    // Newest row first.
    expect(rows[0].textContent).toContain('alice');
    expect(rows[0].textContent).toContain('Title, ListPrice');
    expect(rows[2].textContent).toContain('system');
    expect(rows[2].textContent).toContain('Insert');
  });

  it('renders a neutral "no history" inline state when there is none', async () => {
    await setup([]);

    const badge = fixture.nativeElement.querySelector('.row-audit-badge');
    expect(badge.textContent).toContain('尚無異動紀錄 No history');
    expect(api().latest()).toBeNull();
  });

  it('shows a friendly empty state in the dialog when there is no history', async () => {
    await setup([]);

    fixture.nativeElement.querySelector('.row-audit-badge').click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.row-audit-table')).toBeNull();
    expect(fixture.nativeElement.querySelector('.row-audit-empty').textContent)
      .toContain('尚無任何異動紀錄 No history yet');
  });

  it('does not fetch when there is no record yet (pkid = 0)', async () => {
    await setup(trail, 'Course', 0);
    expect(service.getForRecord).not.toHaveBeenCalled();
    expect(api().latest()).toBeNull();
  });
});
