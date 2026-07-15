import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { Course, CourseQuery } from '../course.model';
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

@Component({
  selector: 'app-course-list',
  imports: [
    FormsModule,
    RouterLink,
    ButtonModule,
    DrawerModule,
    InputTextModule,
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
