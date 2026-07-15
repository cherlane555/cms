import { ChangeDetectorRef, Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ToastModule } from 'primeng/toast';
import { ConfirmationService, MessageService } from 'primeng/api';
import { forkJoin, switchMap } from 'rxjs';
import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course, CourseQuery, CourseRequest } from '@core/models/course.model';
import { PartnerLookup } from '@core/models/partner.model';
import { CourseGroupLookup } from '@core/models/course-group.model';
import { PublishStatusLookup } from '@core/models/publish-status.model';

const FILTERS_KEY = 'course-list-filters';
const SORT_KEY = 'course-list-sort';
const PAGE_KEY = 'course-list-page';

/** Columns editable in place. pkid and the FK lookup columns (原廠, 課程群組) stay read-only. */
type EditableField =
  | 'displayOrder'
  | 'courseId'
  | 'prodCourseId'
  | 'title'
  | 'publishStatusPkid'
  | 'scheduleOn'
  | 'scheduleOff'
  | 'hour'
  | 'listPrice'
  | 'learningCredit'
  | 'canRepeat';

const FIELD_LABELS: Record<EditableField, string> = {
  displayOrder: '顯示順序',
  courseId: '簡介代碼',
  prodCourseId: '科目代碼',
  title: '課程名稱',
  publishStatusPkid: '上架狀態',
  scheduleOn: '上架日期',
  scheduleOff: '下架日期',
  hour: '時數',
  listPrice: '定價',
  learningCredit: '點數',
  canRepeat: '允許重聽',
};

@Component({
  selector: 'app-course-list',
  imports: [
    FormsModule,
    TableModule,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    DrawerModule,
    InputTextModule,
    SelectModule,
    TooltipModule,
    ConfirmDialogModule,
    ToastModule,
  ],
  providers: [ConfirmationService, MessageService],
  templateUrl: './course-list.html',
  styleUrl: './course-list.scss',
})
export class CourseList implements OnInit {
  private readonly service = inject(CourseService);
  private readonly lookups = inject(LookupService);
  private readonly router = inject(Router);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);
  private readonly cdr = inject(ChangeDetectorRef);

  protected readonly courses = signal<Course[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterOpen = signal(false);
  protected readonly partners = signal<PartnerLookup[]>([]);
  protected readonly courseGroups = signal<CourseGroupLookup[]>([]);
  protected readonly publishStatuses = signal<PublishStatusLookup[]>([]);

  // In-place edit state — one cell at a time, keyed `${pkid}:${field}`.
  // The row itself is not mutated until the save succeeds, so a failed save
  // reverts simply by discarding editValue.
  protected readonly editingKey = signal<string | null>(null);
  protected readonly editError = signal<string | null>(null);
  protected readonly savingCell = signal(false);
  protected editValue: unknown = null;
  private editOriginal: unknown = null;

  protected filter: CourseQuery = {
    keyword: null,
    partnerPkid: null,
    courseGroupPkid: null,
    publishStatusPkid: null,
  };

  // Restored from session storage.
  protected sortField = '';
  protected sortOrder = 1;
  protected first = 0;
  protected rows = 20;

  ngOnInit(): void {
    this.restoreState();
    forkJoin({
      partners: this.lookups.getPartners(),
      courseGroups: this.lookups.getCourseGroups(),
      publishStatuses: this.lookups.getPublishStatuses(),
    }).subscribe({
      next: ({ partners, courseGroups, publishStatuses }) => {
        this.partners.set(partners);
        this.courseGroups.set(courseGroups);
        this.publishStatuses.set(publishStatuses);
      },
      error: () =>
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得查詢條件清單' }),
    });
    this.load();
  }

  private restoreState(): void {
    const f = sessionStorage.getItem(FILTERS_KEY);
    if (f) {
      this.filter = { ...this.filter, ...JSON.parse(f) };
    }
    const s = sessionStorage.getItem(SORT_KEY);
    if (s) {
      const { sortField, sortOrder } = JSON.parse(s);
      this.sortField = sortField ?? '';
      this.sortOrder = sortOrder ?? 1;
    }
    const p = sessionStorage.getItem(PAGE_KEY);
    if (p) {
      const { first, rows } = JSON.parse(p);
      this.first = first ?? 0;
      this.rows = rows ?? 20;
    }
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.normalizedFilter()).subscribe({
      next: (data) => {
        this.courses.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messages.add({ severity: 'error', summary: '載入失敗', detail: '無法取得課程清單' });
      },
    });
  }

  private normalizedFilter(): CourseQuery {
    return {
      keyword: this.filter.keyword?.trim() || null,
      partnerPkid: this.filter.partnerPkid ?? null,
      courseGroupPkid: this.filter.courseGroupPkid ?? null,
      publishStatusPkid: this.filter.publishStatusPkid ?? null,
    };
  }

  protected dateOf(iso: string): string {
    return iso.slice(0, 10);
  }

  // --- In-place editing ---

  protected isArmed(pkid: number, field: EditableField): boolean {
    return this.editingKey() === `${pkid}:${field}`;
  }

  /**
   * Double-click arms the cell and re-fires a click so PrimeNG's pEditableColumn
   * (kept disabled while unarmed, which is what suppresses single-click editing)
   * opens its editor.
   */
  protected armCell(course: Course, field: EditableField, event: Event): void {
    if (this.isArmed(course.pkid, field)) {
      return;
    }
    this.editingKey.set(`${course.pkid}:${field}`);
    this.editValue = this.initialEditValue(course, field);
    this.editOriginal = this.editValue;
    this.editError.set(null);
    const cell = event.currentTarget as HTMLElement;
    this.cdr.detectChanges(); // propagate pEditableColumnDisabled=false before the click
    cell.click();
  }

  protected onEditValueChange(course: Course, field: EditableField, value: unknown): void {
    this.editValue = value;
    // Validate eagerly: the ng-invalid/ng-dirty classes this drives are what stop
    // PrimeNG from closing the cell editor on Enter or an outside click.
    this.editError.set(this.validateCell(course, field, value));
  }

  /** Persist the armed cell. Called on editor blur, and by the table's onEditComplete. */
  protected commit(course: Course, field: EditableField): void {
    if (!this.isArmed(course.pkid, field)) {
      return;
    }
    const error = this.validateCell(course, field, this.editValue);
    this.editError.set(error);
    if (error) {
      return; // cell stays in edit mode
    }
    const value = this.normalizedEditValue(field);
    if (value === this.normalizedOriginal(field)) {
      this.clearEditing();
      return;
    }
    if (this.savingCell()) {
      return; // a save for this edit is already in flight (blur + onEditComplete)
    }
    this.savingCell.set(true);
    // The list row lacks certificationPkids/jobCategoryPkids (populated by getById
    // only) and the update endpoint rewrites those junction tables from the request,
    // so fetch the full course first and merge the single edited field into it.
    this.service
      .getById(course.pkid)
      .pipe(switchMap((full) => this.service.update(this.toRequest(full, field, value))))
      .subscribe({
        next: () => {
          this.savingCell.set(false);
          this.applyLocal(course.pkid, field, value);
          this.clearEditing();
          this.messages.add({
            severity: 'success',
            summary: '儲存成功',
            detail: `${FIELD_LABELS[field]} 已更新`,
          });
        },
        error: () => {
          this.savingCell.set(false);
          this.clearEditing(); // row was never mutated → cell reverts to the previous value
          this.messages.add({
            severity: 'error',
            summary: '儲存失敗',
            detail: `${FIELD_LABELS[field]} 未儲存，已還原原值`,
          });
        },
      });
  }

  protected onEditComplete(event: { data?: unknown; field?: string }): void {
    if (event.data && event.field) {
      this.commit(event.data as Course, event.field as EditableField);
    }
  }

  protected onEditCancel(): void {
    this.clearEditing();
  }

  private clearEditing(): void {
    this.editingKey.set(null);
    this.editError.set(null);
    this.editValue = null;
    this.editOriginal = null;
  }

  private initialEditValue(course: Course, field: EditableField): unknown {
    if (field === 'scheduleOn' || field === 'scheduleOff') {
      return CourseList.parseDate(course[field]);
    }
    return course[field];
  }

  private validateCell(course: Course, field: EditableField, value: unknown): string | null {
    switch (field) {
      case 'title':
      case 'courseId':
      case 'prodCourseId':
        return typeof value === 'string' && value.trim() ? null : '此欄位為必填';
      case 'displayOrder':
        return typeof value === 'number' && Number.isFinite(value) ? null : '必須為有效數字';
      case 'hour':
      case 'listPrice':
      case 'learningCredit':
        if (typeof value !== 'number' || !Number.isFinite(value)) {
          return '必須為有效數字';
        }
        return value >= 0 ? null : '不可為負數';
      case 'publishStatusPkid':
        return value != null ? null : '此欄位為必填';
      case 'scheduleOn': {
        if (!(value instanceof Date) || isNaN(value.getTime())) {
          return '請輸入有效日期';
        }
        return CourseList.toDateStr(value) <= this.dateOf(course.scheduleOff)
          ? null
          : '上架日期不可晚於下架日期';
      }
      case 'scheduleOff': {
        if (!(value instanceof Date) || isNaN(value.getTime())) {
          return '請輸入有效日期';
        }
        return this.dateOf(course.scheduleOn) <= CourseList.toDateStr(value)
          ? null
          : '上架日期不可晚於下架日期';
      }
      case 'canRepeat':
        return null;
    }
  }

  /** Editor value in row/request representation (dates become 'yyyy-MM-dd' strings). */
  private normalizedEditValue(field: EditableField): unknown {
    if (field === 'scheduleOn' || field === 'scheduleOff') {
      return CourseList.toDateStr(this.editValue as Date);
    }
    return this.editValue;
  }

  private normalizedOriginal(field: EditableField): unknown {
    if (field === 'scheduleOn' || field === 'scheduleOff') {
      return CourseList.toDateStr(this.editOriginal as Date);
    }
    return this.editOriginal;
  }

  private toRequest(full: Course, field: EditableField, value: unknown): CourseRequest {
    const merged = { ...full, [field]: value } as Course;
    return {
      pkid: merged.pkid,
      title: merged.title,
      officialTitle: merged.officialTitle,
      courseId: merged.courseId,
      prodCourseId: merged.prodCourseId,
      friendlyUrl: merged.friendlyUrl,
      displayOrder: merged.displayOrder,
      partnerPkid: merged.partnerPkid,
      courseGroupPkid: merged.courseGroupPkid,
      publishStatusPkid: merged.publishStatusPkid,
      scheduleOn: merged.scheduleOn.slice(0, 10),
      scheduleOff: merged.scheduleOff.slice(0, 10),
      hour: merged.hour,
      listPrice: merged.listPrice,
      learningCredit: merged.learningCredit,
      material: merged.material,
      objective: merged.objective,
      target: merged.target,
      prerequisites: merged.prerequisites,
      outline: merged.outline,
      towardCertOrExam: merged.towardCertOrExam,
      note: merged.note,
      otherInfo: merged.otherInfo,
      canRepeat: merged.canRepeat,
      certificationPkids: merged.certificationPkids,
      jobCategoryPkids: merged.jobCategoryPkids,
    };
  }

  private applyLocal(pkid: number, field: EditableField, value: unknown): void {
    this.courses.update((list) =>
      list.map((c) => {
        if (c.pkid !== pkid) {
          return c;
        }
        const next = { ...c, [field]: value } as Course;
        if (field === 'publishStatusPkid') {
          next.publishStatusDescription =
            this.publishStatuses().find((p) => p.pkid === value)?.description ??
            next.publishStatusDescription;
        }
        return next;
      }),
    );
  }

  // ISO string <-> Date, in local time (avoid toISOString's UTC shift).
  private static parseDate(iso: string): Date {
    const [y, m, d] = iso.slice(0, 10).split('-').map(Number);
    return new Date(y, m - 1, d);
  }

  private static toDateStr(date: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  }

  // --- Filters / sort / paging / row actions (unchanged) ---

  protected applyFilter(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.filter));
    this.first = 0;
    this.filterOpen.set(false);
    this.load();
  }

  protected clearFilter(): void {
    this.filter = { keyword: null, partnerPkid: null, courseGroupPkid: null, publishStatusPkid: null };
    sessionStorage.removeItem(FILTERS_KEY);
    this.first = 0;
    this.load();
  }

  protected onSort(event: { field?: string; order?: number }): void {
    this.sortField = event.field ?? '';
    this.sortOrder = event.order ?? 1;
    sessionStorage.setItem(
      SORT_KEY,
      JSON.stringify({ sortField: this.sortField, sortOrder: this.sortOrder }),
    );
  }

  protected onPage(event: TableLazyLoadEvent): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 20;
    sessionStorage.setItem(PAGE_KEY, JSON.stringify({ first: this.first, rows: this.rows }));
  }

  protected add(): void {
    this.router.navigate(['/courses/new']);
  }

  protected view(course: Course): void {
    this.router.navigate(['/courses', course.pkid]);
  }

  protected edit(course: Course): void {
    this.router.navigate(['/courses', course.pkid, 'edit']);
  }

  protected remove(course: Course): void {
    this.confirm.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${course.pkid}</b>「${course.title}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.service.delete(course.pkid).subscribe({
          next: () => {
            this.messages.add({ severity: 'success', summary: '已刪除', detail: course.title });
            this.load();
          },
          error: (err: { status?: number }) =>
            this.messages.add({
              severity: 'error',
              summary: '刪除失敗',
              detail:
                err?.status === 409
                  ? '課程仍被 FAQ／相關連結／熱門課程引用，無法刪除'
                  : course.title,
            }),
        });
      },
    });
  }
}
