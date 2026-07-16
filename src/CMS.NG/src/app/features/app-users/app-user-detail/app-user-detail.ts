import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { SelectModule } from 'primeng/select';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AppUser, UserRole } from '../app-user.model';
import { AppUserService } from '../app-user.service';
import { AppRoleService } from '../../app-roles/app-role.service';
import { AuthService } from '../../auth/auth.service';
import { RowAuditBadgeComponent } from '../../row-audit/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-app-user-detail',
  imports: [RouterLink, DatePipe, FormsModule, ButtonModule, TagModule, SelectModule, RowAuditBadgeComponent],
  templateUrl: './app-user-detail.html',
  styleUrl: './app-user-detail.scss'
})
export class AppUserDetail implements OnInit {
  private readonly service = inject(AppUserService);
  private readonly roleService = inject(AppRoleService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly authService = inject(AuthService);

  protected readonly user = signal<AppUser | null>(null);
  protected readonly loading = signal(false);
  protected readonly resetting = signal(false);

  // Role management (AppUserRole N-N editor) is Admin-only — both the UI and the backend enforce it.
  protected readonly roles = signal<UserRole[]>([]);
  protected readonly allRoleIds = signal<{ roleId: string; roleName: string }[]>([]);
  protected readonly selectedRoleId = signal<string | null>(null);
  protected readonly savingRole = signal(false);

  // Reset-Password-to-Default is Admin-only; the button is hidden for non-Admins (the backend enforces
  // the Admin role regardless).
  protected readonly isAdmin = this.authService.isAdmin;

  /** Roles the user does not yet have — the assignable options for the add-role picker. */
  protected readonly assignableRoles = computed(() => {
    const assigned = new Set(this.roles().map(r => r.roleId));
    return this.allRoleIds()
      .filter(r => !assigned.has(r.roleId))
      .map(r => ({ label: `${r.roleName} (${r.roleId})`, value: r.roleId }));
  });

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    this.loading.set(true);

    this.service.getByPkid(pkid).subscribe({
      next: user => {
        this.user.set(user);
        this.loading.set(false);
        if (this.isAdmin()) {
          this.loadRoles(pkid);
          this.loadAllRoles();
        }
      },
      error: () => {
        this.loading.set(false);
        this.messageService.add({ severity: 'error', summary: '載入失敗', detail: '找不到該使用者。' });
        this.router.navigate(['/app-users']);
      }
    });
  }

  protected edit(): void {
    const user = this.user();
    if (user) {
      this.router.navigate(['/app-users', user.pkid, 'edit']);
    }
  }

  private loadRoles(pkid: number): void {
    this.service.getRoles(pkid).subscribe({
      next: roles => this.roles.set(roles)
    });
  }

  private loadAllRoles(): void {
    this.roleService.getAll().subscribe({
      next: all => this.allRoleIds.set(all.map(r => ({ roleId: r.roleId, roleName: r.roleName })))
    });
  }

  protected assignRole(): void {
    const user = this.user();
    const roleId = this.selectedRoleId();
    if (!user || !roleId) {
      return;
    }

    this.savingRole.set(true);
    this.service.assignRole(user.pkid, roleId).subscribe({
      next: roles => {
        this.roles.set(roles);
        this.selectedRoleId.set(null);
        this.savingRole.set(false);
        this.reloadUser(user.pkid);
        this.messageService.add({ severity: 'success', summary: '指派成功', detail: `已指派角色「${roleId}」。` });
      },
      error: err => {
        this.savingRole.set(false);
        const detail = err?.error?.message ?? '指派角色時發生錯誤。';
        this.messageService.add({ severity: 'error', summary: '指派失敗', detail });
      }
    });
  }

  protected confirmRemoveRole(role: UserRole): void {
    const user = this.user();
    if (!user) {
      return;
    }

    this.confirmationService.confirm({
      header: '移除角色確認',
      message: `確定要移除使用者「${user.userId}」的角色「${role.roleName} (${role.roleId})」？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '移除',
      rejectLabel: '取消',
      accept: () => this.removeRole(user.pkid, role.roleId)
    });
  }

  private removeRole(pkid: number, roleId: string): void {
    this.savingRole.set(true);
    this.service.removeRole(pkid, roleId).subscribe({
      next: () => {
        this.roles.update(rs => rs.filter(r => r.roleId !== roleId));
        this.savingRole.set(false);
        this.reloadUser(pkid);
        this.messageService.add({ severity: 'success', summary: '移除成功', detail: `已移除角色「${roleId}」。` });
      },
      error: err => {
        this.savingRole.set(false);
        const detail = err?.error?.message ?? '移除角色時發生錯誤。';
        this.messageService.add({ severity: 'error', summary: '移除失敗', detail });
      }
    });
  }

  protected confirmResetPassword(): void {
    const user = this.user();
    if (!user) {
      return;
    }

    this.confirmationService.confirm({
      header: '重設密碼確認',
      message: `確定要將使用者「${user.userId}」的密碼重設為系統預設值？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '重設',
      rejectLabel: '取消',
      accept: () => this.resetPassword(user)
    });
  }

  private resetPassword(user: AppUser): void {
    // Only the target userId is sent — no password/hash ever crosses the API.
    this.resetting.set(true);
    this.authService.resetPasswordToDefault(user.userId).subscribe({
      next: () => {
        this.resetting.set(false);
        this.messageService.add({ severity: 'success', summary: '重設成功', detail: `使用者「${user.userId}」的密碼已重設為預設值。` });
        this.reloadUser(user.pkid);
      },
      error: err => {
        this.resetting.set(false);
        const detail = err?.error?.message ?? '重設密碼時發生錯誤。';
        this.messageService.add({ severity: 'error', summary: '重設失敗', detail });
      }
    });
  }

  private reloadUser(pkid: number): void {
    this.service.getByPkid(pkid).subscribe({
      next: user => this.user.set(user)
    });
  }
}
