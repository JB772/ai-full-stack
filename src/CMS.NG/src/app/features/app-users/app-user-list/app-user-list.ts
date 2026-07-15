import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { DatePipe } from '@angular/common';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AppUser, AppUserQuery } from '../app-user.model';
import { AppUserService } from '../app-user.service';

const FILTERS_KEY = 'app-user-list-filters';
const SORT_KEY = 'app-user-list-sort';
const PAGE_KEY = 'app-user-list-page';

const EMPTY_FILTERS: AppUserQuery = { keyword: null, isActive: null };

@Component({
  selector: 'app-app-user-list',
  imports: [
    FormsModule,
    RouterLink,
    DatePipe,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    TooltipModule
  ],
  templateUrl: './app-user-list.html',
  styleUrl: './app-user-list.scss'
})
export class AppUserList implements OnInit {
  private readonly service = inject(AppUserService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly users = signal<AppUser[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  /** null = 全部. */
  protected readonly activeOptions = [
    { label: '全部', value: null },
    { label: '啟用', value: true },
    { label: '停用', value: false }
  ];

  protected filters: AppUserQuery = { ...EMPTY_FILTERS };

  protected sortField = 'userId';
  protected sortOrder = 1;
  protected first = 0;
  protected rows = 20;

  ngOnInit(): void {
    this.restoreState();
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.service.query(this.filters).subscribe({
      next: users => {
        this.users.set(users);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '無法取得使用者資料。' });
      }
    });
  }

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
    return !!this.filters.keyword || this.filters.isActive != null;
  }

  protected onSort(event: { field?: string | string[] | null; order?: number | null }): void {
    const field = Array.isArray(event.field) ? event.field[0] : event.field;
    this.sortField = field ?? 'userId';
    this.sortOrder = event.order ?? 1;
    sessionStorage.setItem(SORT_KEY, JSON.stringify({ sortField: this.sortField, sortOrder: this.sortOrder }));
  }

  protected onPage(event: { first?: number; rows?: number }): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 20;
    this.persistPage();
  }

  protected confirmDelete(user: AppUser): void {
    this.confirmationService.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${user.pkid}</b>「${user.userId}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(user)
    });
  }

  private delete(user: AppUser): void {
    this.service.delete(user.pkid).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: '刪除成功', detail: `使用者「${user.userId}」已刪除。` });
        this.load();
      },
      error: err => {
        // 409 when the user still has role assignments — surface the API's message.
        const detail = err?.error?.message ?? '刪除使用者時發生錯誤。';
        this.messageService.add({ severity: 'error', summary: '刪除失敗', detail });
      }
    });
  }

  protected view(user: AppUser): void {
    this.router.navigate(['/app-users', user.pkid]);
  }

  protected edit(user: AppUser): void {
    this.router.navigate(['/app-users', user.pkid, 'edit']);
  }

  private persistFilters(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.filters));
  }

  private persistPage(): void {
    sessionStorage.setItem(PAGE_KEY, JSON.stringify({ first: this.first, rows: this.rows }));
  }

  private restoreState(): void {
    const filters = this.readState<AppUserQuery>(FILTERS_KEY);
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
