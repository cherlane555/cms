import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { RowAuditBadge } from './row-audit-badge';
import { RowAuditService } from '@core/services/row-audit.service';
import { RowAuditEntry } from '@core/models/row-audit.model';

describe('RowAuditBadge', () => {
  let fixture: ComponentFixture<RowAuditBadge>;
  let serviceSpy: jasmine.SpyObj<RowAuditService>;

  const entries: RowAuditEntry[] = [
    { dateTime: '2026-06-04T14:30:00', userName: 'alice', actionType: 'Update', actionDesc: 'Title,ListPrice' },
    { dateTime: '2026-06-01T09:00:00', userName: 'bob', actionType: 'Insert', actionDesc: '課程A' },
  ];

  beforeEach(() => {
    serviceSpy = jasmine.createSpyObj('RowAuditService', ['getForRecord']);
  });

  async function create(pkid: number | null, rows: RowAuditEntry[] = entries): Promise<void> {
    serviceSpy.getForRecord.and.returnValue(of(rows));

    await TestBed.configureTestingModule({
      imports: [RowAuditBadge],
      providers: [provideNoopAnimations(), { provide: RowAuditService, useValue: serviceSpy }],
    }).compileComponents();

    fixture = TestBed.createComponent(RowAuditBadge);
    fixture.componentRef.setInput('tableName', 'Course');
    fixture.componentRef.setInput('pkid', pkid);
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('fetches the record history and shows the latest entry inline', async () => {
    await create(123);

    expect(serviceSpy.getForRecord).toHaveBeenCalledWith('Course', 123);
    expect(text()).toContain('異動紀錄 History');
    expect(text()).toContain('Update by alice');
    expect(text()).toContain('2026-06-04 14:30');
    // Only the latest entry is inline — the older one appears in the dialog only.
    expect(text()).not.toContain('bob');
  });

  it('opens the dialog with the full trail, newest first', async () => {
    await create(123);

    (fixture.nativeElement as HTMLElement).querySelector('button')!.click();
    fixture.detectChanges();

    const rows = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.audit-table tbody tr'),
    );
    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('alice');
    expect(rows[0].textContent).toContain('Update');
    expect(rows[0].textContent).toContain('Title,ListPrice');
    expect(rows[1].textContent).toContain('bob');
    expect(rows[1].textContent).toContain('課程A');
  });

  it('shows the neutral no-history state when the record has no audit rows', async () => {
    await create(123, []);

    expect(text()).toContain('尚無紀錄 No history');

    (fixture.nativeElement as HTMLElement).querySelector('button')!.click();
    fixture.detectChanges();

    expect(text()).toContain('此筆資料尚無異動紀錄 No history yet.');
  });

  it('does not call the API for an unsaved record (no pkid)', async () => {
    await create(null);

    expect(serviceSpy.getForRecord).not.toHaveBeenCalled();
    expect(text()).toContain('尚無紀錄 No history');
  });
});
