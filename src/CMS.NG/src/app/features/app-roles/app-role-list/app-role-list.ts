import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DrawerModule } from 'primeng/drawer';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AppRole, AppRoleQuery } from '../app-role.model';
import { AppRoleService } from '../app-role.service';

const FILTERS_KEY = 'app-role-list-filters';
const SORT_KEY = 'app-role-list-sort';
const PAGE_KEY = 'app-role-list-page';

@Component({
  selector: 'app-app-role-list',
  imports: [
    FormsModule,
    RouterLink,
    ButtonModule,
    DrawerModule,
    InputTextModule,
    InputNumberModule,
    TableModule,
    TooltipModule
  ],
  templateUrl: './app-role-list.html',
  styleUrl: './app-role-list.scss'
})
export class AppRoleList implements OnInit {
  private readonly service = inject(AppRoleService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);

  protected readonly roles = signal<AppRole[]>([]);
  protected readonly loading = signal(false);
  protected readonly filterDrawerVisible = signal(false);

  protected filters: AppRoleQuery = { keyword: null, permissionLevelFrom: null, permissionLevelTo: null };

  protected sortField = 'roleId';
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
      next: roles => {
        this.roles.set(roles);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '無法取得角色資料。' });
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
    this.filters = { keyword: null, permissionLevelFrom: null, permissionLevelTo: null };
    this.applyFilters();
  }

  protected get hasActiveFilters(): boolean {
    return !!this.filters.keyword
      || this.filters.permissionLevelFrom != null
      || this.filters.permissionLevelTo != null;
  }

  protected onSort(event: { field?: string | string[] | null; order?: number | null }): void {
    const field = Array.isArray(event.field) ? event.field[0] : event.field;
    this.sortField = field ?? 'roleId';
    this.sortOrder = event.order ?? 1;
    sessionStorage.setItem(SORT_KEY, JSON.stringify({ sortField: this.sortField, sortOrder: this.sortOrder }));
  }

  protected onPage(event: { first?: number; rows?: number }): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 20;
    this.persistPage();
  }

  protected confirmDelete(role: AppRole): void {
    this.confirmationService.confirm({
      header: '刪除確認',
      message: `確定要刪除主代碼 <b>${role.pkid}</b>「${role.roleId}」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '刪除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.delete(role)
    });
  }

  private delete(role: AppRole): void {
    this.service.delete(role.pkid).subscribe({
      next: () => {
        this.messageService.add({ severity: 'success', summary: '刪除成功', detail: `角色「${role.roleId}」已刪除。` });
        this.load();
      },
      error: err => {
        const detail = err?.error?.message ?? '刪除角色時發生錯誤。';
        this.messageService.add({ severity: 'error', summary: '刪除失敗', detail });
      }
    });
  }

  protected view(role: AppRole): void {
    this.router.navigate(['/app-roles', role.pkid]);
  }

  protected edit(role: AppRole): void {
    this.router.navigate(['/app-roles', role.pkid, 'edit']);
  }

  private persistFilters(): void {
    sessionStorage.setItem(FILTERS_KEY, JSON.stringify(this.filters));
  }

  private persistPage(): void {
    sessionStorage.setItem(PAGE_KEY, JSON.stringify({ first: this.first, rows: this.rows }));
  }

  private restoreState(): void {
    const filters = this.readState<AppRoleQuery>(FILTERS_KEY);
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
