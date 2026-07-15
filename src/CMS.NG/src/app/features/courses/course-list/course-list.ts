import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { Course, CourseQuery, CourseRequest } from '../course.model';
import { CourseService } from '../course.service';
import { fromIsoDate, toIsoDate } from '../date.util';
import { PartnerService } from '../../partners/partner.service';
import { CourseGroupService } from '../../course-groups/course-group.service';
import { PublishStatusService } from '../../publish-statuses/publish-status.service';

const FILTERS_KEY = 'course-list-filters';
const SORT_KEY = 'course-list-sort';
const PAGE_KEY = 'course-list-page';

const EMPTY_FILTERS: CourseQuery = {
  keyword: null,
  partnerPkid: null,
  courseGroupPkid: null,
  publishStatusPkid: null,
  scheduleOnFrom: null,
  scheduleOnTo: null,
  scheduleOffFrom: null,
  scheduleOffTo: null,
  canRepeat: null
};

interface Option {
  label: string;
  value: number;
}

/** Columns editable in place on the list. pkid, partner and courseGroup are intentionally excluded. */
type EditableField =
  | 'title'
  | 'courseId'
  | 'prodCourseId'
  | 'displayOrder'
  | 'publishStatusPkid'
  | 'scheduleOn'
  | 'scheduleOff'
  | 'hour'
  | 'listPrice'
  | 'learningCredit'
  | 'canRepeat';

/** The single cell currently open for inline editing. */
interface CellEdit {
  pkid: number;
  field: EditableField;
  /** Editor-bound working value (Date for date columns, number for numeric/select, etc.). */
  value: string | number | boolean | Date | null;
  /** Inline validation error; when set the cell stays in edit mode. */
  error: string | null;
}

/** Outcome of validating an edited cell before persisting. */
interface ValidateResult {
  error: string | null;
  changed: boolean;
  patch?: Partial<CourseRequest>;
  apply?: (course: Course) => void;
}

const FIELD_LABELS: Record<EditableField, string> = {
  title: '課程名稱',
  courseId: '簡介代碼',
  prodCourseId: '科目代碼',
  displayOrder: '顯示順序',
  publishStatusPkid: '上架狀態',
  scheduleOn: '上架日期',
  scheduleOff: '下架日期',
  hour: '時數',
  listPrice: '定價',
  learningCredit: '點數',
  canRepeat: '允許重聽'
};

const TEXT_MAX: Record<'title' | 'courseId' | 'prodCourseId', number> = {
  title: 200,
  courseId: 50,
  prodCourseId: 50
};

@Component({
  selector: 'app-course-list',
  imports: [
    FormsModule,
    RouterLink,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    InputNumberModule,
    CheckboxModule,
    DatePickerModule,
    SelectModule,
    TableModule,
    TagModule,
    TooltipModule
  ],
  templateUrl: './course-list.html',
  styleUrl: './course-list.scss'
})
export class CourseList implements OnInit {
  private readonly service = inject(CourseService);
  private readonly partnerService = inject(PartnerService);
  private readonly courseGroupService = inject(CourseGroupService);
  private readonly publishStatusService = inject(PublishStatusService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly courses = signal<Course[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  protected readonly partnerOptions = signal<Option[]>([]);
  protected readonly courseGroupOptions = signal<Option[]>([]);
  protected readonly publishStatusOptions = signal<Option[]>([]);

  /** null = 不篩選. */
  protected readonly triStateOptions = [
    { label: '全部', value: null },
    { label: '是', value: true },
    { label: '否', value: false }
  ];

  protected filters: CourseQuery = { ...EMPTY_FILTERS };

  protected sortField = 'displayOrder';
  protected sortOrder = 1;
  protected first = 0;
  protected rows = 20;

  ngOnInit(): void {
    this.restoreState();
    this.loadLookups();
    this.load();
  }

  private loadLookups(): void {
    forkJoin({
      partners: this.partnerService.getAll(),
      courseGroups: this.courseGroupService.getAll(),
      publishStatuses: this.publishStatusService.getAll()
    }).subscribe({
      next: ({ partners, courseGroups, publishStatuses }) => {
        this.partnerOptions.set(partners.map(p => ({ label: p.name, value: p.pkid })));
        this.courseGroupOptions.set(courseGroups.map(g => ({ label: g.description, value: g.pkid })));
        this.publishStatusOptions.set(publishStatuses.map(s => ({ label: s.description, value: s.pkid })));
      },
      error: () =>
        this.messageService.add({ severity: 'warn', summary: '選項載入失敗', detail: '無法取得篩選下拉選項。' })
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.filters).subscribe({
      next: courses => {
        this.courses.set(courses);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '無法取得課程資料。' });
      }
    });
  }

  // p-datepicker binds a Date; the query carries yyyy-MM-dd strings. Bridge with local-component helpers.
  protected get scheduleOnFromDate(): Date | null { return fromIsoDate(this.filters.scheduleOnFrom); }
  protected set scheduleOnFromDate(d: Date | null) { this.filters.scheduleOnFrom = toIsoDate(d); }
  protected get scheduleOnToDate(): Date | null { return fromIsoDate(this.filters.scheduleOnTo); }
  protected set scheduleOnToDate(d: Date | null) { this.filters.scheduleOnTo = toIsoDate(d); }
  protected get scheduleOffFromDate(): Date | null { return fromIsoDate(this.filters.scheduleOffFrom); }
  protected set scheduleOffFromDate(d: Date | null) { this.filters.scheduleOffFrom = toIsoDate(d); }
  protected get scheduleOffToDate(): Date | null { return fromIsoDate(this.filters.scheduleOffTo); }
  protected set scheduleOffToDate(d: Date | null) { this.filters.scheduleOffTo = toIsoDate(d); }

  protected applyFilters(): void {
    this.first = 0;
    this.persistFilters();
    this.persistPage();
    this.filterDrawerVisible.set(false);
    this.load();
  }

  protected clearFilters(): void {
    this.filters = { ...EMPTY_FILTERS };
    this.applyFilters();
  }

  protected get hasActiveFilters(): boolean {
    return !!this.filters.keyword
      || this.filters.partnerPkid != null
      || this.filters.courseGroupPkid != null
      || this.filters.publishStatusPkid != null
      || this.filters.scheduleOnFrom != null
      || this.filters.scheduleOnTo != null
      || this.filters.scheduleOffFrom != null
      || this.filters.scheduleOffTo != null
      || this.filters.canRepeat != null;
  }

  protected onSort(event: { field?: string | string[] | null; order?: number | null }): void {
    const field = Array.isArray(event.field) ? event.field[0] : event.field;
    this.sortField = field ?? 'displayOrder';
    this.sortOrder = event.order ?? 1;
    sessionStorage.setItem(SORT_KEY, JSON.stringify({ sortField: this.sortField, sortOrder: this.sortOrder }));
  }

  protected onPage(event: { first?: number; rows?: number }): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 20;
    this.persistPage();
  }

  protected confirmDelete(course: Course): void {
    this.confirmationService.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${course.pkid}</b>「${course.title}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(course)
    });
  }

  private delete(course: Course): void {
    this.service.delete(course.pkid).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: '刪除成功',
          detail: `課程「${course.title}」已刪除。`
        });
        this.load();
      },
      error: err => {
        // 409 when the course still has child rows — surface the API's message, which names the offenders.
        const detail = err?.error?.message ?? '刪除課程時發生錯誤。';
        this.messageService.add({ severity: 'error', summary: '刪除失敗', detail });
      }
    });
  }

  protected view(course: Course): void {
    this.router.navigate(['/courses', course.pkid]);
  }

  protected edit(course: Course): void {
    this.router.navigate(['/courses', course.pkid, 'edit']);
  }

  // ---------------------------------------------------------------------------
  // Inline (in-place) editing — double-click a cell to edit, blur to persist.
  // ---------------------------------------------------------------------------

  /** The cell currently being edited (null = none). */
  protected readonly editing = signal<CellEdit | null>(null);
  /** True while an inline update is in flight — blocks starting a second edit. */
  protected readonly savingEdit = signal(false);

  /** Two-way bridge for the active editor's value (keeps the signal reactive). */
  protected get editValue(): CellEdit['value'] {
    return this.editing()?.value ?? null;
  }
  protected set editValue(value: CellEdit['value']) {
    const current = this.editing();
    if (current) {
      this.editing.set({ ...current, value });
    }
  }

  protected isEditing(course: Course, field: EditableField): boolean {
    const e = this.editing();
    return !!e && e.pkid === course.pkid && e.field === field;
  }

  /** Enter edit mode for a cell. Bound to (dblclick) — single click never fires this. */
  protected startEdit(course: Course, field: EditableField): void {
    if (this.savingEdit()) {
      return;
    }
    this.editing.set({ pkid: course.pkid, field, value: this.editorValue(course, field), error: null });
  }

  protected cancelEdit(): void {
    this.editing.set(null);
  }

  /**
   * The 上架狀態 dropdown persists on (onChange) — NOT on blur: its overlay is appended to `body`, so a
   * blur-to-commit fires as the option is clicked and tears the editor down before the pick lands. This
   * closes the editor only when the panel hides without a change (click-away / same value re-picked).
   */
  protected onSelectHide(course: Course): void {
    if (this.savingEdit()) {
      return;
    }
    if (this.isEditing(course, 'publishStatusPkid')) {
      this.cancelEdit();
    }
  }

  private editorValue(course: Course, field: EditableField): CellEdit['value'] {
    switch (field) {
      case 'scheduleOn':
        return fromIsoDate(course.scheduleOn);
      case 'scheduleOff':
        return fromIsoDate(course.scheduleOff);
      default:
        return course[field] as CellEdit['value'];
    }
  }

  /**
   * Persist the active edit (on blur). Validates first: on failure the cell stays in edit mode with an
   * inline error; on a server failure the row is left untouched (so the cell reverts) and a toast shows.
   */
  protected commit(course: Course): void {
    const edit = this.editing();
    if (!edit || edit.pkid !== course.pkid || this.savingEdit()) {
      return;
    }

    const outcome = this.validate(course, edit.field, edit.value);
    if (outcome.error) {
      this.editing.set({ ...edit, error: outcome.error });
      return;
    }

    if (!outcome.changed) {
      this.editing.set(null);
      return;
    }

    this.savingEdit.set(true);
    this.service.update(this.buildRequest(course, outcome.patch!)).subscribe({
      next: () => {
        // Only touch the displayed row once the server has accepted the change.
        outcome.apply!(course);
        this.savingEdit.set(false);
        this.editing.set(null);
      },
      error: err => {
        // The row was never mutated, so simply closing the editor reverts the cell.
        this.savingEdit.set(false);
        this.editing.set(null);
        const detail = err?.error?.message ?? '更新課程時發生錯誤。';
        this.messageService.add({ severity: 'error', summary: '更新失敗', detail });
      }
    });
  }

  private validate(course: Course, field: EditableField, raw: CellEdit['value']): ValidateResult {
    switch (field) {
      case 'title':
      case 'courseId':
      case 'prodCourseId': {
        const text = typeof raw === 'string' ? raw.trim() : '';
        if (!text) {
          return { error: `${FIELD_LABELS[field]}不可為空。`, changed: false };
        }
        const max = TEXT_MAX[field];
        if (text.length > max) {
          return { error: `${FIELD_LABELS[field]}長度不可超過 ${max} 字。`, changed: false };
        }
        return this.result(text === course[field], { [field]: text } as Partial<CourseRequest>, c => { (c as unknown as Record<string, unknown>)[field] = text; });
      }

      case 'displayOrder':
      case 'hour':
      case 'listPrice':
      case 'learningCredit': {
        if (typeof raw !== 'number' || Number.isNaN(raw)) {
          return { error: `${FIELD_LABELS[field]}必須為有效數字。`, changed: false };
        }
        if (raw < 0) {
          return { error: `${FIELD_LABELS[field]}不可小於 0。`, changed: false };
        }
        return this.result(raw === course[field], { [field]: raw } as Partial<CourseRequest>, c => { (c as unknown as Record<string, unknown>)[field] = raw; });
      }

      case 'publishStatusPkid': {
        if (typeof raw !== 'number') {
          return { error: '上架狀態為必填。', changed: false };
        }
        const option = this.publishStatusOptions().find(o => o.value === raw);
        return this.result(raw === course.publishStatusPkid, { publishStatusPkid: raw }, c => {
          c.publishStatusPkid = raw;
          if (option) {
            c.publishStatus = { pkid: option.value, description: option.label };
          }
        });
      }

      case 'scheduleOn':
      case 'scheduleOff': {
        const iso = raw instanceof Date ? toIsoDate(raw) : null;
        if (!iso) {
          return { error: `${FIELD_LABELS[field]}必須為有效日期。`, changed: false };
        }
        // yyyy-MM-dd strings compare correctly lexicographically.
        const onIso = field === 'scheduleOn' ? iso : course.scheduleOn;
        const offIso = field === 'scheduleOff' ? iso : course.scheduleOff;
        if (onIso && offIso && onIso > offIso) {
          return { error: '上架日期不可晚於下架日期。', changed: false };
        }
        return this.result(iso === course[field], { [field]: iso } as Partial<CourseRequest>, c => { (c as unknown as Record<string, unknown>)[field] = iso; });
      }

      case 'canRepeat': {
        const value = !!raw;
        return this.result(value === course.canRepeat, { canRepeat: value }, c => { c.canRepeat = value; });
      }
    }
  }

  private result(unchanged: boolean, patch: Partial<CourseRequest>, apply: (course: Course) => void): ValidateResult {
    return { error: null, changed: !unchanged, patch, apply };
  }

  /** Build a full update DTO from the row, overriding only the edited field(s). */
  private buildRequest(course: Course, overrides: Partial<CourseRequest>): CourseRequest {
    return {
      pkid: course.pkid,
      title: course.title,
      officialTitle: course.officialTitle,
      courseId: course.courseId,
      prodCourseId: course.prodCourseId,
      friendlyUrl: course.friendlyUrl,
      displayOrder: course.displayOrder,
      partnerPkid: course.partnerPkid,
      courseGroupPkid: course.courseGroupPkid,
      publishStatusPkid: course.publishStatusPkid,
      scheduleOn: course.scheduleOn,
      scheduleOff: course.scheduleOff,
      hour: course.hour,
      listPrice: course.listPrice,
      learningCredit: course.learningCredit,
      material: course.material,
      objective: course.objective,
      target: course.target,
      prerequisites: course.prerequisites,
      outline: course.outline,
      towardCertOrExam: course.towardCertOrExam,
      note: course.note,
      otherInfo: course.otherInfo,
      canRepeat: course.canRepeat,
      ...overrides
    };
  }

  private persistFilters(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.filters));
  }

  private persistPage(): void {
    sessionStorage.setItem(PAGE_KEY, JSON.stringify({ first: this.first, rows: this.rows }));
  }

  private restoreState(): void {
    const filters = this.readState<CourseQuery>(FILTERS_KEY);
    if (filters) {
      this.filters = { ...this.filters, ...filters };
    }

    const sort = this.readState<{ sortField: string; sortOrder: number }>(SORT_KEY);
    if (sort) {
      this.sortField = sort.sortField;
      this.sortOrder = sort.sortOrder;
    }

    const page = this.readState<{ first: number; rows: number }>(PAGE_KEY);
    if (page) {
      this.first = page.first;
      this.rows = page.rows;
    }
  }

  private readState<T>(key: string): T | null {
    const raw = sessionStorage.getItem(key);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as T;
    } catch {
      sessionStorage.removeItem(key);
      return null;
    }
  }
}
